using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using WarField.EffectAnim;

// Baker for EffectAnim elements (projectiles, explosions, ambient VFX).
//
// Differences from Frame2TextureArray (CharacterAnim baker):
//   • Colour-only Texture2DArray — no normal map packing.
//   • No state / variation structure: Dir_x folders sit directly under the element root.
//   • Writes to EffectAnimConf.xml with a single <clip> element per entry.
//   • isLoop / frameRate / eventFrame are configurable directly in the tool UI.
//
// Folder convention:
//   <ElementRoot>/
//       Dir_0/   0.png  1.png  ...
//       Dir_1/   0.png  1.png  ...
//       ...
// For omnidirectional effects a single Dir_0 folder is sufficient.
public class EffectFrame2TextureArray : EditorWindow
{
    private const string EFFECT_ANIM_CONF_XML_PATH = "Assets/Resources/Conf/EffectAnimConf.xml";

    // ============ UI state ============
    private DefaultAsset _targetElementFolder;

    private int   _bleedPixelsHD   = 8;
    private float _cornerCutRatio  = 0.15f;
    private string _description    = "";
    private string _lastFolderPath = "";

    // Clip parameters (edited per element in the tool, persisted to XML)
    private bool  _isLoop      = false;
    private float _frameRate   = 1.0f;
    private int   _eventFrame  = -1;
    private float _worldSize   = 1.0f;

    private const string BAKED_OUTPUT_SUBFOLDER = "BakedArrays";

    // ============ temporary scan structures ============
    private class TempDirClip
    {
        public int           p_dirIndex;
        public List<Texture2D> p_colorFrames = new List<Texture2D>();
    }

    [MenuItem("Tools/Effect Animation Baker")]
    public static void ShowWindow()
    {
        var window = GetWindow<EffectFrame2TextureArray>("Effect Animation Baker");
        window.minSize = new Vector2(520, 440);
        window.Show();
    }

    // =========================================================
    //                        OnGUI
    // =========================================================
    private void OnGUI()
    {
        GUILayout.Space(15);
        GUILayout.Label("Effect Animation Baker", EditorStyles.boldLabel);
        GUILayout.Space(8);

        // ---- Element source folder ----
        EditorGUILayout.BeginHorizontal();
        _targetElementFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            new GUIContent("待烘焙效果根目录",
                "选择包含 Dir_x 子文件夹的效果元素根目录。\n" +
                "Dir_x 中放置序列帧 PNG（无需法线图）。\n" +
                "烘焙产物写入源目录，配置写入 EffectAnimConf.xml。"),
            _targetElementFolder, typeof(DefaultAsset), false);
        if (GUILayout.Button("浏览...", GUILayout.Width(60)))
        {
            string defaultPath = _targetElementFolder != null
                ? AssetDatabase.GetAssetPath(_targetElementFolder)
                : "Assets";
            string abs = EditorUtility.OpenFolderPanel("选择效果元素根目录", defaultPath, "");
            if (!string.IsNullOrEmpty(abs))
            {
                if (abs.StartsWith(Application.dataPath))
                {
                    string rel = "Assets" + abs.Substring(Application.dataPath.Length);
                    _targetElementFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(rel);
                }
                else
                {
                    EditorUtility.DisplayDialog("路径错误", "请选择 Assets 目录内部的文件夹！", "知道了");
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        // Auto-fill description + clip params when folder changes
        string curPath = _targetElementFolder != null ? AssetDatabase.GetAssetPath(_targetElementFolder) : "";
        if (curPath != _lastFolderPath)
        {
            _lastFolderPath = curPath;
            if (!string.IsNullOrEmpty(curPath))
                TryFillFromExistingXml(Path.GetFileName(curPath));
        }

        GUILayout.Space(6);
        _description = EditorGUILayout.TextField(
            new GUIContent("描述 (description)", "写入 EffectAnimConf.xml <description>，运行时不解析。"),
            _description);

        GUILayout.Space(8);
        EditorGUILayout.LabelField("动画参数", EditorStyles.boldLabel);

        _isLoop = EditorGUILayout.Toggle(
            new GUIContent("循环播放 (isLoop)", "勾选表示循环动画（环境特效），不勾选为单次播放（投射物命中等）。"),
            _isLoop);

        _frameRate = EditorGUILayout.FloatField(
            new GUIContent("播放速率倍率 (frameRate)", "相对于全局 EffectAnimCtrl._animFPS 的倍率，1.0 = 正常速度。"),
            _frameRate);

        _eventFrame = EditorGUILayout.IntField(
            new GUIContent("事件帧 (eventFrame)", "到达该帧时触发 IEffectAnimInfo_OnEffectAnimEvent(0)。-1 = 不触发。"),
            _eventFrame);

        _worldSize = EditorGUILayout.FloatField(
            new GUIContent("世界尺寸 (worldSize)",
                "精灵的物理高度（单位：米）。写入 EffectAnimConf.xml，运行时由 EffectCtrl 传给 Shader 的 _TotalWorldSize。\n" +
                "例：1.5 表示该特效在世界中高 1.5 米。"),
            _worldSize);

        GUILayout.Space(8);
        EditorGUILayout.LabelField("烘焙参数", EditorStyles.boldLabel);

        _bleedPixelsHD = EditorGUILayout.IntSlider(
            new GUIContent("描边宽度 (HD)",
                "颜色外扩像素数（HD 512px 基准）。MD/LD 自动按比例缩减。\n" +
                "防止硬切 alpha 边缘出现暗化黑环。"),
            _bleedPixelsHD, 0, 32);

        _cornerCutRatio = EditorGUILayout.Slider(
            new GUIContent("八角切角比例",
                "对 alpha 包围盒的宽/高各切去四角的百分比。\n" +
                "0 = 不切（退化为矩形），0.10~0.15 = 典型值，最大 0.25。"),
            _cornerCutRatio, 0f, 0.25f);

        GUILayout.Space(10);

        bool canBake = _targetElementFolder != null;
        if (!canBake)
        {
            EditorGUILayout.HelpBox("请先指定效果元素根目录。", MessageType.Warning);
        }
        else
        {
            string elemPath = AssetDatabase.GetAssetPath(_targetElementFolder);
            string xmlPath  = DeriveXmlPath(elemPath);
            EditorGUILayout.HelpBox(
                $"配置文件: {EFFECT_ANIM_CONF_XML_PATH}\n" +
                $"源目录 / 输出目录: {elemPath}\n" +
                $"XML path 字段: {xmlPath}\n" +
                $"元素名: {Path.GetFileName(elemPath)}",
                MessageType.None);
        }

        GUI.enabled = canBake;
        if (GUILayout.Button("Generate", GUILayout.Height(40)))
        {
            ExecuteBakePipeline();
            AssetDatabase.Refresh();
        }
        GUI.enabled = true;
    }

    // =========================================================
    //                  Core bake pipeline
    // =========================================================
    private void ExecuteBakePipeline()
    {
        string rootPath    = AssetDatabase.GetAssetPath(_targetElementFolder);
        string elementName = Path.GetFileName(rootPath);
        string xmlPath     = DeriveXmlPath(rootPath);
        string bakedFolder = rootPath;

        // ---- 1. Scan Dir_x folders ----
        List<TempDirClip> dirs = ScanDirectionFolders(rootPath);
        if (dirs.Count == 0)
        {
            EditorUtility.DisplayDialog("错误",
                $"[{elementName}] 根目录下未发现任何 Dir_x 文件夹，或文件夹中没有 PNG 文件。\n\n" +
                "有效结构：<元素根目录>/Dir_0/0.png 1.png ...",
                "确定");
            return;
        }

        // ---- 2. Count total slices (all dirs × frames) ----
        int totalSlices = dirs.Sum(d => d.p_colorFrames.Count);
        if (totalSlices == 0)
        {
            EditorUtility.DisplayDialog("错误", $"[{elementName}] 扫描到的目录里没有有效帧！", "确定");
            return;
        }

        // ---- 3. Bake HD / MD / LD colour Texture2DArrays ----
        string[] resLayers = { "HD", "MD", "LD" };
        int[]    resSizes  = { 512, 256, 128 };

        // Global alpha bounding-box for octagon mesh (computed in HD pass)
        float globalMinX = 1f, globalMaxX = 0f, globalMinY = 1f, globalMaxY = 0f;
        bool hasAnyValidPixel = false;

        Texture2DArray[] colorArrays = new Texture2DArray[3];

        for (int layer = 0; layer < resLayers.Length; layer++)
        {
            string layerName   = resLayers[layer];
            int    targetSize  = resSizes[layer];
            int    bleedPixels = _bleedPixelsHD;
            if (layerName == "MD") bleedPixels = Mathf.CeilToInt(_bleedPixelsHD / 2f);
            if (layerName == "LD") bleedPixels = Mathf.CeilToInt(Mathf.CeilToInt(_bleedPixelsHD / 2f) / 2f);

            Texture2DArray colorArray = new Texture2DArray(targetSize, targetSize, totalSlices, TextureFormat.BC7, true);
            int sliceIdx = 0;

            foreach (TempDirClip dir in dirs)
            {
                for (int f = 0; f < dir.p_colorFrames.Count; f++)
                {
                    Texture2D srcTex = dir.p_colorFrames[f];

                    // Collect alpha bounds from HD pass for the octagon mesh
                    if (layerName == "HD" && srcTex != null)
                    {
                        string srcPath = AssetDatabase.GetAssetPath(srcTex);
                        if (!string.IsNullOrEmpty(srcPath))
                        {
                            Vector4 bound = AnalyzeAlphaBounds(srcPath, _bleedPixelsHD);
                            if (bound != new Vector4(0f, 1f, 0f, 1f))
                            {
                                if (bound.x < globalMinX) globalMinX = bound.x;
                                if (bound.y > globalMaxX) globalMaxX = bound.y;
                                if (bound.z < globalMinY) globalMinY = bound.z;
                                if (bound.w > globalMaxY) globalMaxY = bound.w;
                                hasAnyValidPixel = true;
                            }
                        }
                    }

                    if (srcTex != null)
                    {
                        Texture2D resized = ResizeTextureWithMipmaps(srcTex, targetSize, targetSize, bleedPixels);
                        EditorUtility.CompressTexture(resized, TextureFormat.BC7, TextureCompressionQuality.Best);
                        for (int mip = 0; mip < resized.mipmapCount; mip++)
                            Graphics.CopyTexture(resized, 0, mip, colorArray, sliceIdx, mip);
                        DestroyImmediate(resized);
                    }
                    else
                    {
                        // Missing frame: fill with transparent black
                        Texture2D empty = new Texture2D(targetSize, targetSize, TextureFormat.RGBA32, true);
                        empty.Apply(true, false);
                        EditorUtility.CompressTexture(empty, TextureFormat.BC7, TextureCompressionQuality.Best);
                        for (int mip = 0; mip < empty.mipmapCount; mip++)
                            Graphics.CopyTexture(empty, 0, mip, colorArray, sliceIdx, mip);
                        DestroyImmediate(empty);
                    }
                    sliceIdx++;
                }
            }

            colorArray.Apply(false, false);

            string colorPath = $"{bakedFolder}/{elementName}_{layerName}_Color.asset";
            SaveAssetPreservingMeta(colorArray, colorPath);
            RegisterAsAddressable(colorPath, $"{xmlPath}/{elementName}_{layerName}_Color", "EffectAnimTextures");
            colorArrays[layer] = AssetDatabase.LoadAssetAtPath<Texture2DArray>(colorPath);
        }

        // ---- 4. Octagon mesh ----
        if (!hasAnyValidPixel) { globalMinX = 0f; globalMaxX = 1f; globalMinY = 0f; globalMaxY = 1f; }

        float w    = globalMaxX - globalMinX;
        float h    = globalMaxY - globalMinY;
        float cutU = w * _cornerCutRatio;
        float cutV = h * _cornerCutRatio;

        Vector2[] uvs = new Vector2[8]
        {
            new Vector2(globalMinX + cutU, globalMaxY),
            new Vector2(globalMaxX - cutU, globalMaxY),
            new Vector2(globalMaxX,        globalMaxY - cutV),
            new Vector2(globalMaxX,        globalMinY + cutV),
            new Vector2(globalMaxX - cutU, globalMinY),
            new Vector2(globalMinX + cutU, globalMinY),
            new Vector2(globalMinX,        globalMinY + cutV),
            new Vector2(globalMinX,        globalMaxY - cutV),
        };

        Vector3[] vertices = new Vector3[8];
        for (int i = 0; i < 8; i++)
            vertices[i] = new Vector3(uvs[i].x - 0.5f, uvs[i].y - 0.5f, 0f);

        int[] triangles = { 0,1,2, 0,2,3, 0,3,4, 0,4,5, 0,5,6, 0,6,7 };

        Vector3[] normals = new Vector3[8];
        for (int i = 0; i < 8; i++) normals[i] = Vector3.forward;

        Mesh mesh = new Mesh
        {
            name      = $"{elementName}_OctagonMesh",
            vertices  = vertices,
            uv        = uvs,
            triangles = triangles,
            normals   = normals,
        };
        mesh.RecalculateTangents();

        string meshPath = $"{bakedFolder}/{elementName}_OctagonMesh.asset";
        SaveAssetPreservingMeta(mesh, meshPath);
        RegisterAsAddressable(meshPath, $"{xmlPath}/{elementName}_OctagonMesh", "EffectAnimMeshs");

        // ---- 5. Compute per-direction start offsets ----
        var dirOffsets = new List<int>();
        int cursor = 0;
        foreach (TempDirClip dir in dirs)
        {
            dirOffsets.Add(cursor);
            cursor += dir.p_colorFrames.Count;
        }
        int frameCount = dirs.Count > 0 ? dirs[0].p_colorFrames.Count : 0;

        // ---- 6. Write EffectAnimConf.xml ----
        UpdateEffectAnimConf(elementName, xmlPath, _description,
            frameCount, _frameRate, _eventFrame, _isLoop, _worldSize, dirOffsets);

        // ---- 7. Log ----
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"<b><color=green>Effect Animation Baker 成功！</color></b>");
        sb.AppendLine($"元素: <color=yellow>{elementName}</color>  |  输出目录: <color=white>{bakedFolder}</color>");
        sb.AppendLine($"总切片数: <color=cyan>{totalSlices}</color>  |  方向数: <color=cyan>{dirs.Count}</color>  |  每方向帧数: <color=cyan>{frameCount}</color>");
        cursor = 0;
        foreach (TempDirClip d in dirs)
        {
            sb.AppendLine($"  Dir_{d.p_dirIndex}: {d.p_colorFrames.Count} 帧, offset=<color=orange>{cursor}</color>");
            cursor += d.p_colorFrames.Count;
        }
        Debug.Log(sb.ToString());

        EditorUtility.DisplayDialog("成功",
            $"[{elementName}] 烘焙完成！\n" +
            $"方向数: {dirs.Count}   每方向帧数: {frameCount}\n" +
            $"输出目录: {bakedFolder}", "OK");
    }

    // =========================================================
    //             Folder scan: Dir_x directly under root
    // =========================================================
    private static List<TempDirClip> ScanDirectionFolders(string rootPath)
    {
        var result = new List<TempDirClip>();
        var cmp    = new NaturalStringComparer();

        string[] dirDirs = Directory.GetDirectories(rootPath, "Dir_*")
            .OrderBy(d => Path.GetFileName(d), cmp)
            .ToArray();

        foreach (string dirPath in dirDirs)
        {
            string folderName = Path.GetFileName(dirPath);
            if (!int.TryParse(folderName.Replace("Dir_", ""), out int dirIndex))
                continue;

            TempDirClip clip = new TempDirClip { p_dirIndex = dirIndex };

            string[] pngs = Directory.GetFiles(dirPath, "*.png")
                .OrderBy(f => f, cmp)
                .ToArray();

            foreach (string png in pngs)
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalizeAssetPath(png));
                if (tex != null) clip.p_colorFrames.Add(tex);
            }

            if (clip.p_colorFrames.Count > 0)
                result.Add(clip);
        }

        result = result.OrderBy(d => d.p_dirIndex).ToList();
        return result;
    }

    // =========================================================
    //          XML helpers: EffectAnimConf.xml
    // =========================================================
    private static void UpdateEffectAnimConf(
        string elementName, string xmlPath, string description,
        int frameCount, float frameRate, int eventFrame, bool isLoop, float worldSize,
        List<int> dirOffsets)
    {
        string absXmlPath = Path.GetFullPath(
            EFFECT_ANIM_CONF_XML_PATH.Replace("Assets", Application.dataPath.TrimEnd('/')));

        XmlDocument doc = new XmlDocument();
        if (File.Exists(absXmlPath))
        {
            doc.Load(absXmlPath);
        }
        else
        {
            EnsureUnityDirectory(Path.GetDirectoryName(EFFECT_ANIM_CONF_XML_PATH));
            doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-8", null));
            doc.AppendChild(doc.CreateElement("effectAnimCfgs"));
        }

        XmlElement root = doc.DocumentElement;
        if (root == null)
        {
            Debug.LogError($"[Effect Animation Baker] {EFFECT_ANIM_CONF_XML_PATH} 结构异常，根节点缺失。");
            return;
        }

        // Find existing entry
        XmlElement existingAnim = null;
        foreach (XmlNode node in root.ChildNodes)
        {
            if (node.NodeType != XmlNodeType.Element) continue;
            if (node.SelectSingleNode("name")?.InnerText?.Trim() == elementName)
            {
                existingAnim = (XmlElement)node;
                break;
            }
        }

        string offsetsStr = string.Join(",", dirOffsets);

        if (existingAnim != null)
        {
            // Update path
            SetOrCreateChild(doc, existingAnim, "path", xmlPath);

            // Update description only if user provided one
            if (!string.IsNullOrEmpty(description))
                SetOrCreateChild(doc, existingAnim, "description", description);

            // Replace <clip>
            XmlNode oldClip = existingAnim.SelectSingleNode("clip");
            if (oldClip != null) existingAnim.RemoveChild(oldClip);
            existingAnim.AppendChild(BuildClipElement(doc, frameCount, frameRate, eventFrame, isLoop, worldSize, offsetsStr));
        }
        else
        {
            XmlElement animElem = doc.CreateElement("anim");
            SetOrCreateChild(doc, animElem, "description", description ?? "");
            SetOrCreateChild(doc, animElem, "name", elementName);
            SetOrCreateChild(doc, animElem, "path", xmlPath);
            animElem.AppendChild(BuildClipElement(doc, frameCount, frameRate, eventFrame, isLoop, worldSize, offsetsStr));
            root.AppendChild(animElem);
        }

        var settings = new XmlWriterSettings { Indent = true, IndentChars = "    ", Encoding = System.Text.Encoding.UTF8 };
        using (XmlWriter writer = XmlWriter.Create(absXmlPath, settings))
            doc.Save(writer);

        AssetDatabase.ImportAsset(EFFECT_ANIM_CONF_XML_PATH, ImportAssetOptions.ForceUpdate);
        Debug.Log($"[Effect Animation Baker] EffectAnimConf.xml 已更新：{elementName} → {xmlPath}");
    }

    private static XmlElement BuildClipElement(XmlDocument doc,
        int frameCount, float frameRate, int eventFrame, bool isLoop, float worldSize, string offsets)
    {
        XmlElement e = doc.CreateElement("clip");
        e.SetAttribute("frameCount",  frameCount.ToString());
        e.SetAttribute("frameRate",   frameRate.ToString("G", System.Globalization.CultureInfo.InvariantCulture));
        e.SetAttribute("eventFrame",  eventFrame.ToString());
        e.SetAttribute("isLoop",      isLoop ? "true" : "false");
        e.SetAttribute("worldSize",   worldSize.ToString("G", System.Globalization.CultureInfo.InvariantCulture));
        e.SetAttribute("offsets",     offsets);
        return e;
    }

    private static void SetOrCreateChild(XmlDocument doc, XmlElement parent, string tagName, string value)
    {
        XmlNode existing = parent.SelectSingleNode(tagName);
        if (existing != null) { existing.InnerText = value; return; }
        XmlElement el = doc.CreateElement(tagName);
        el.InnerText = value;
        parent.AppendChild(el);
    }

    // Auto-fill UI fields from existing XML when the user switches folder
    private void TryFillFromExistingXml(string elementName)
    {
        string absXmlPath = Path.GetFullPath(
            EFFECT_ANIM_CONF_XML_PATH.Replace("Assets", Application.dataPath.TrimEnd('/')));
        if (!File.Exists(absXmlPath)) return;

        XmlDocument doc = new XmlDocument();
        doc.Load(absXmlPath);

        foreach (XmlNode node in doc.SelectNodes("effectAnimCfgs/anim") ?? new XmlDocument().ChildNodes)
        {
            if (node.NodeType != XmlNodeType.Element) continue;
            if (node.SelectSingleNode("name")?.InnerText?.Trim() != elementName) continue;

            _description = node.SelectSingleNode("description")?.InnerText?.Trim() ?? "";

            XmlNode clip = node.SelectSingleNode("clip");
            if (clip == null) return;

            if (float.TryParse(clip.Attributes?["frameRate"]?.Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float fr))
                _frameRate = fr;

            if (int.TryParse(clip.Attributes?["eventFrame"]?.Value, out int ef))
                _eventFrame = ef;

            _isLoop = string.Equals(clip.Attributes?["isLoop"]?.Value, "true",
                System.StringComparison.OrdinalIgnoreCase);

            if (float.TryParse(clip.Attributes?["worldSize"]?.Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float ws) && ws > 0f)
                _worldSize = ws;
            return;
        }
    }

    // =========================================================
    //             Addressables registration
    // =========================================================
    private static void RegisterAsAddressable(string assetPath, string address, string groupName)
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogWarning("[Effect Animation Baker] Addressable Settings 未找到，请先初始化 Addressables Groups。");
            return;
        }

        AddressableAssetGroup group = settings.FindGroup(groupName);
        if (group == null)
        {
            group = settings.CreateGroup(groupName, false, false, false,
                new List<AddressableAssetGroupSchema>
                {
                    ScriptableObject.CreateInstance<BundledAssetGroupSchema>(),
                    ScriptableObject.CreateInstance<ContentUpdateGroupSchema>(),
                });
        }

        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid))
        {
            Debug.LogWarning($"[Effect Animation Baker] 无法注册 Addressable：{assetPath} GUID 为空。");
            return;
        }

        AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, false);
        entry.address = address;
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, true);
    }

    // =========================================================
    //          Path helpers (copied from Frame2TextureArray)
    // =========================================================
    private static string DeriveXmlPath(string folderPath)
    {
        const string assetsPrefix = "Assets/";
        return folderPath.StartsWith(assetsPrefix, System.StringComparison.OrdinalIgnoreCase)
            ? folderPath.Substring(assetsPrefix.Length)
            : folderPath;
    }

    private static void EnsureUnityDirectory(string assetPath)
    {
        string abs = Path.GetFullPath(assetPath.Replace("Assets", Application.dataPath.TrimEnd('/')));
        if (!Directory.Exists(abs))
        {
            Directory.CreateDirectory(abs);
            AssetDatabase.Refresh();
        }
    }

    private static string NormalizeAssetPath(string path)
    {
        string normalized = path.Replace('\\', '/');
        string dataPath   = Application.dataPath.Replace('\\', '/');
        if (normalized.StartsWith(dataPath, System.StringComparison.OrdinalIgnoreCase))
            return "Assets" + normalized.Substring(dataPath.Length);
        return normalized;
    }

    // =========================================================
    //     Asset I/O (preserve GUID on re-bake) — identical to
    //     Frame2TextureArray.SaveAssetPreservingMeta
    // =========================================================
    private static void SaveAssetPreservingMeta(UnityEngine.Object asset, string assetPath)
    {
        string tempPath = "Assets/temp_effect_array_bake.asset";
        AssetDatabase.CreateAsset(asset, tempPath);
        AssetDatabase.SaveAssets();

        if (File.Exists(assetPath))
        {
            try { File.Copy(tempPath, assetPath, true); AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate); }
            catch (System.Exception e) { Debug.LogError($"[Effect Animation Baker] 覆盖失败: {e.Message}"); }
            AssetDatabase.DeleteAsset(tempPath);
        }
        else
        {
            AssetDatabase.MoveAsset(tempPath, assetPath);
        }

        string targetName = Path.GetFileNameWithoutExtension(assetPath);
        var saved = AssetDatabase.LoadMainAssetAtPath(assetPath);
        if (saved != null && saved.name != targetName)
        {
            saved.name = targetName;
            EditorUtility.SetDirty(saved);
            AssetDatabase.SaveAssets();
        }
    }

    // =========================================================
    //     Alpha bound analysis — identical to Frame2TextureArray
    // =========================================================
    private static Vector4 AnalyzeAlphaBounds(string assetPath, int bleedPixelsHD)
    {
        byte[] fileData = File.ReadAllBytes(assetPath);
        Texture2D raw = new Texture2D(2, 2);
        raw.LoadImage(fileData);

        int W = raw.width, H = raw.height;
        Color32[] pixels = raw.GetPixels32();

        int minX = W, maxX = 0, minY = H, maxY = 0;
        bool hasPixel = false;

        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                if (pixels[x + y * W].a > 12)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                    hasPixel = true;
                }

        DestroyImmediate(raw);
        if (!hasPixel) return new Vector4(0f, 1f, 0f, 1f);

        int pad = Mathf.Max(1, Mathf.RoundToInt(bleedPixelsHD * W / 512f));
        minX = Mathf.Max(0, minX - pad);  maxX = Mathf.Min(W - 1, maxX + pad);
        minY = Mathf.Max(0, minY - pad);  maxY = Mathf.Min(H - 1, maxY + pad);
        return new Vector4((float)minX / W, (float)maxX / W, (float)minY / H, (float)maxY / H);
    }

    // =========================================================
    //     Texture resize + mipmap + Alpha Bleed — identical to Frame2TextureArray
    // =========================================================
    private static Texture2D ResizeTextureWithMipmaps(Texture2D source, int width, int height, int bleedPixels)
    {
        Texture2D effectiveSource = source;
        Texture2D tempBled        = null;

        if (bleedPixels > 0)
        {
            tempBled = LoadAndBleedSourcePNG(source, bleedPixels, width);
            if (tempBled != null) effectiveSource = tempBled;
        }

        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        RenderTexture.active = rt;
        GL.Clear(true, true, Color.clear);
        effectiveSource.filterMode = FilterMode.Bilinear;
        Graphics.Blit(effectiveSource, rt);

        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, true);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);

        if (bleedPixels <= 0)
        {
            Color32[] px = result.GetPixels32();
            for (int i = 0; i < px.Length; i++)
                if (px[i].a == 0) { px[i].r = 0; px[i].g = 0; px[i].b = 0; }
            result.SetPixels32(px);
        }

        result.Apply(true, false);
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        if (tempBled != null) DestroyImmediate(tempBled);
        return result;
    }

    private static Texture2D LoadAndBleedSourcePNG(Texture2D source, int bleedPixels, int targetSize)
    {
        string assetPath = AssetDatabase.GetAssetPath(source);
        if (string.IsNullOrEmpty(assetPath)) return null;

        byte[] data;
        try { data = File.ReadAllBytes(assetPath); }
        catch { return null; }

        Texture2D bled = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!bled.LoadImage(data)) { DestroyImmediate(bled); return null; }

        int srcW      = bled.width;
        int thickness = Mathf.Max(1, Mathf.RoundToInt((float)bleedPixels * srcW / targetSize));
        Color32[] px  = bled.GetPixels32();
        ApplyAlphaBleed(px, srcW, bled.height, thickness);
        bled.SetPixels32(px);
        bled.Apply(false, false);
        return bled;
    }

    private static void ApplyAlphaBleed(Color32[] pixels, int W, int H, int bleedThickness)
    {
        const byte BODY_ALPHA = 128;
        int N = W * H;
        int[] distMap      = new int[N];
        int[] nearestSeed  = new int[N];
        for (int i = 0; i < N; i++) { distMap[i] = int.MaxValue; nearestSeed[i] = -1; }

        var bfs = new Queue<int>(N / 8 + 64);
        for (int i = 0; i < N; i++)
            if (pixels[i].a >= BODY_ALPHA) { distMap[i] = 0; nearestSeed[i] = i; bfs.Enqueue(i); }

        while (bfs.Count > 0)
        {
            int idx = bfs.Dequeue();
            int d   = distMap[idx];
            if (d >= bleedThickness) continue;
            int x = idx % W, y = idx / W, nd = d + 1, seed = nearestSeed[idx];

            void Try(int n) { if (nd < distMap[n]) { distMap[n] = nd; nearestSeed[n] = seed; bfs.Enqueue(n); } }
            if (x > 0)         Try(idx - 1);
            if (x < W - 1)     Try(idx + 1);
            if (y > 0)         Try(idx - W);
            if (y < H - 1)     Try(idx + W);
            if (x > 0     && y > 0)     Try(idx - W - 1);
            if (x < W - 1 && y > 0)     Try(idx - W + 1);
            if (x > 0     && y < H - 1) Try(idx + W - 1);
            if (x < W - 1 && y < H - 1) Try(idx + W + 1);
        }

        for (int i = 0; i < N; i++)
        {
            int d = distMap[i];
            if (d == 0) { pixels[i].a = 255; }
            else if (d <= bleedThickness && nearestSeed[i] >= 0)
            {
                Color32 s = pixels[nearestSeed[i]];
                pixels[i].r = s.r; pixels[i].g = s.g; pixels[i].b = s.b; pixels[i].a = 0;
            }
            else { pixels[i].a = 0; }
        }
    }
}
