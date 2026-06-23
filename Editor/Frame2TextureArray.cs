using System;
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
using WarField.Anim;

// 借用 Windows 自然字符串排序 API (StrCmpLogicalW), 保证 0.png, 2.png, 10.png 按数字大小排序,
// 而不是按字典序排成 0.png, 10.png, 2.png 错位
public class NaturalStringComparer : IComparer<string>
{
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int StrCmpLogicalW(string x, string y);

    public int Compare(string x, string y) => StrCmpLogicalW(x, y);
}

// 万能解耦帧动画烘焙机. 接管原版 Frame2TextureArray 的 HD/MD/LD BC7 渲染管线与八角 Mesh 生成.
// 烘焙产物（Texture2DArray / OctagonMesh.asset）写入源目录。状态数据全量写入 CharacterAnimConf.xml。
public class Frame2TextureArray : EditorWindow
{
    // CharacterAnimConf.xml 的完整 Assets 路径（位于 Resources 下，运行时可用 Resources.Load 读取）
    private const string ANIM_CONF_XML_PATH = "Assets/Resources/Conf/CharacterAnimConf.xml";

    // ============== UI 状态 ==============
    private DefaultAsset targetElementFolder;

    // Alpha Bleed (颜色外扩) 宽度: HD (512²) 档位下把身体最近邻 RGB 向透明区外扩多少像素.
    // MD/LD 自动按分辨率比例缩减. 设为 0 关闭外扩, 回退到 alpha=0 → RGB 洗黑的旧行为
    // (会带回 cutout 暗边问题, 仅作 debug 对照). 推荐 HD 8 px 起步.
    private int _bleedPixelsHD = 8;

    // 八角 Mesh 切角比例：对 alpha 包围盒的宽/高各切去四角的百分比（0 = 不切角，即退化为矩形）。
    //
    // 【作用】角色贴图四角通常是纯透明区域，用矩形 Quad 会让 GPU 白跑这些片元的
    //         纹理采样 + 光照计算（只在 clip 时才丢弃）。切掉四角可减少约 20%~35% Overdraw，
    //         上千个单位同屏时收益显著。
    //
    // 【风险】切角是在"全帧、全方向 alpha 包围盒"的基础上再裁剪四角。
    //         若某帧某方向的武器/肢体像素恰好落在对角线位置（例如斜向大幅挥刀），
    //         该像素的 UV 可能同时接近 maxX 和 maxY，会被几何硬切而不是 clip 丢弃，
    //         表现为那一块身体像素直接消失（不是噪点，是一块面缺失）。
    //
    // 【调整建议】
    //   - 默认 0.10（10%）是保守值，绝大多数人形角色不会触发切角问题。
    //   - 如需更激进地削减 Overdraw，可逐步调大到 0.15（15%），但必须在编辑器里
    //     将八角 Mesh 赋给 Prefab，播放所有动画帧目视验证四角无身体像素被裁。
    //   - 如果某个动作（攻击/技能）有斜向大幅外伸的武器，建议设为 0.05 或直接 0。
    //   - 设为 0 时八角 Mesh 退化为与包围盒等大的矩形，完全无切角风险，
    //     但相对于完整 Quad 仍然有收益（包围盒已比 0~1 Quad 小很多）。
    private float _cornerCutRatio = 0.15f;

    // description 字段：美术/策划对此动画元素的备注说明，写入 CharacterAnimConf.xml。
    // 运行时不解析，仅供人工阅读和版本审查使用。
    private string _description = "";

    // 用于检测 targetElementFolder 变化后自动从 XML 回填 description
    private string _lastFolderPath = "";

    // 烘焙输出子目录名 (放在元素根目录下), 扫描时必须显式跳过这个名字防止把自己当成 state
    private const string BAKED_OUTPUT_SUBFOLDER = "BakedArrays";

    // 美术法线命名约定: 与 color 帧同名 + "_normal.png" 后缀, 同目录共存
    private const string NORMAL_SUFFIX = "_normal.png";

    // ============== 树形临时收集结构 ==============
    private class TempDirectionClip
    {
        public int p_dirIndex;
        public List<Texture2D> p_colorFrames = new List<Texture2D>();
        public List<Texture2D> p_normalFrames = new List<Texture2D>();
    }

    private class TempVariationData
    {
        public int p_varIndex;
        public List<TempDirectionClip> p_directions = new List<TempDirectionClip>();
    }

    private class TempStateData
    {
        public string p_stateName;
        public List<TempVariationData> p_variations = new List<TempVariationData>();
    }

    [MenuItem("Tools/Animation Baker")]
    public static void ShowWindow()
    {
        Frame2TextureArray window = GetWindow<Frame2TextureArray>("Animation Baker");
        window.minSize = new Vector2(520, 420);
        window.Show();
    }

    // =========================================================
    //                       面板 OnGUI
    // =========================================================
    private void OnGUI()
    {
        GUILayout.Space(15);
        GUILayout.Label("Animation Baker", EditorStyles.boldLabel);
        GUILayout.Space(10);

        // ---- 元素源目录 ----
        EditorGUILayout.BeginHorizontal();
        targetElementFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            new GUIContent("待烘焙元素根目录",
                "选择包含动画帧 PNG 的元素根目录（如 Assets/Animation/Soldiers/Human/Melee/Infantry）。\n" +
                "烘焙产物（Texture2DArray / OctagonMesh / AnimData.json）将写入 Assets/Resources/ 下的同名路径。"),
            targetElementFolder, typeof(DefaultAsset), false);
        if (GUILayout.Button("浏览...", GUILayout.Width(60)))
        {
            string defaultPath = targetElementFolder != null
                ? AssetDatabase.GetAssetPath(targetElementFolder)
                : "Assets";
            string absolutePath = EditorUtility.OpenFolderPanel("选择元素根目录", defaultPath, "");
            if (!string.IsNullOrEmpty(absolutePath))
            {
                if (absolutePath.StartsWith(Application.dataPath))
                {
                    string relativePath = "Assets" + absolutePath.Substring(Application.dataPath.Length);
                    targetElementFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(relativePath);
                }
                else
                {
                    EditorUtility.DisplayDialog("路径错误", "请选择 Assets 目录内部的文件夹！", "知道了");
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        // ---- 当 folder 变化时从 XML 回填 description ----
        string curFolderPath = targetElementFolder != null ? AssetDatabase.GetAssetPath(targetElementFolder) : "";
        if (curFolderPath != _lastFolderPath)
        {
            _lastFolderPath = curFolderPath;
            if (!string.IsNullOrEmpty(curFolderPath))
                _description = TryGetExistingDescription(Path.GetFileName(curFolderPath));
        }

        // ---- description（不参与运行时解析，仅写入 XML 供人工阅读）----
        GUILayout.Space(6);
        _description = EditorGUILayout.TextField(
            new GUIContent("描述 (description)",
                "对该动画元素的备注说明，写入 CharacterAnimConf.xml 的 <description> 节点。\n" +
                "运行时不解析，仅供美术/策划在版本审查时阅读。\n" +
                "切换元素目录时会自动回填 XML 中已有的描述内容。"),
            _description);

        GUILayout.Space(10);
        _bleedPixelsHD = EditorGUILayout.IntSlider(
            new GUIContent(
                "描边宽度",
                "在 alpha 掩膜外圈用 8-邻接 BFS 把【最近身体像素的 RGB】拷贝出去, alpha 仍为 0.\n" +
                "原理: 让 bilinear 采样 / 各级 mip 下采样在 RGB 上始终读到身体颜色,\n" +
                "      硬切 alpha test 只会切掉 alpha 不够的羽边, 永远不会出现暗化黑环.\n" +
                "      同时 BC7 端点不再需要拟合【彩色 → 黑】高对比, 块噪声大幅降低.\n" +
                "MD/LD 按照一半的一半规则自动向上取整缩减外扩像素数."),
            _bleedPixelsHD, 0, 32);

        GUILayout.Space(6);
        _cornerCutRatio = EditorGUILayout.Slider(
            new GUIContent(
                "八角切角比例",
                "生成八角 Mesh 时，对 alpha 包围盒的宽/高各切去四角的百分比。\n\n" +
                "【作用】\n" +
                "角色贴图四角通常是纯透明区域，用矩形 Quad 会让 GPU 对这些片元\n" +
                "跑完完整的纹理采样和光照计算后才在 clip 处丢弃（Overdraw）。\n" +
                "切掉四角可减少约 20%~35% 的 Overdraw，上千单位同屏时收益显著。\n\n" +
                "【风险】\n" +
                "切角基于全帧、全方向 alpha 包围盒，若某帧某方向的武器/肢体像素\n" +
                "同时接近包围盒的 maxX 和 maxY（如斜向大幅挥刀），会被几何硬切：\n" +
                "不是噪点，而是那块身体面直接消失。\n\n" +
                "【推荐值】\n" +
                "• 0.10（默认）：保守值，绝大多数人形角色安全。\n" +
                "• 0.15：需目视验证所有攻击/技能帧四角无身体像素被裁。\n" +
                "• 0.05 或 0：有斜向大外伸武器时使用；0 退化为矩形包围盒，无切角风险。\n\n" +
                "修改后点 Generate 重新烘焙，新 Mesh 自动保存到元素的 Resources 目录。"),
            _cornerCutRatio, 0f, 0.25f);

        GUILayout.Space(10);

        bool canBake = targetElementFolder != null;
        if (!canBake)
        {
            EditorGUILayout.HelpBox(
                "请先指定元素根目录。\n" +
                "烘焙产物（Texture2DArray / OctagonMesh）写入源目录；状态数据全量写入 CharacterAnimConf.xml。",
                MessageType.Warning);
        }
        else
        {
            string elemPath  = AssetDatabase.GetAssetPath(targetElementFolder);
            string xmlPath   = DeriveXmlPath(elemPath);
            EditorGUILayout.HelpBox(
                $"配置文件: {ANIM_CONF_XML_PATH}\n" +
                $"源目录 / 输出目录: {elemPath}\n" +
                $"XML path 字段: {xmlPath}\n" +
                $"元素名（XML key）: {Path.GetFileName(elemPath)}",
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
    //                    核心烘焙主管线
    // =========================================================
    private void ExecuteBakePipeline()
    {
        string rootPath    = AssetDatabase.GetAssetPath(targetElementFolder);
        string elementName = Path.GetFileName(rootPath);

        // 烘焙产物直接输出到源目录（与拖入的文件夹相同）。
        // XML <path> = 源目录去掉 "Assets/" 前缀，AnimCtrl 在编辑器运行时用 AssetDatabase 重建完整路径。
        string xmlPath    = DeriveXmlPath(rootPath);
        string bakedFolder = rootPath;

        // ---- 1. 树形目录扫描 (变种 + 方向 + 帧) ----
        List<TempStateData> scannedData = ScanElementAnimationFolder(rootPath);
        if (scannedData.Count == 0)
        {
            EditorUtility.DisplayDialog("错误",
                $"[{elementName}] 根目录下未发现任何符合规范的动画目录！\n\n" +
                "有效结构:\n" +
                "  <动画名>/Dir_x/...\n" +
                "  <动画名>/<1>/Dir_x/...\n" +
                "  <动画名>/<1/2/3...>/Dir_x/...",
                "确定");
            return;
        }

        // ---- 2. 预计算总切片数 (HD/MD/LD 三档共用同一布局) ----
        int singleBlockTotalFrames = 0;
        foreach (var st in scannedData)
        foreach (var v in st.p_variations)
        foreach (var d in v.p_directions)
            singleBlockTotalFrames += d.p_colorFrames.Count;

        if (singleBlockTotalFrames == 0)
        {
            EditorUtility.DisplayDialog("错误", $"[{elementName}] 扫描到的目录里没有提取到任何有效色帧！", "确定");
            return;
        }

        // ---- 2.5 法线完整性 pre-bake 校验 ----
        //   在启动任何 BC7 烘焙、写盘动作之前, 把"色帧 vs 法线帧"全部对一遍.
        //   只要发现哪怕一处 (state, var, dir, frame) 缺法线, 立刻把所有缺失
        //   位置一次性聚合到一个对话框里抛出, 然后退出, 绝不开始烘焙.
        //   理由: 烘到一半才弹会留下"半残的 .asset 文件 + GPU 上半个 array",
        //         美术看到的体验是"一会儿白一会儿黑", 不如全打回去一次性补.
        if (!ValidateNormalCompletenessOrAbort(scannedData, elementName))
        {
            return;
        }

        // ---- 3. 回溯老配置（从 CharacterAnimConf.xml 已有条目读取，用于继承 isLoop/eventFrame/frameRate）----
        ElementAnimBakedData oldElementData = LoadOldElementDataFromXml(elementName);

        ElementAnimBakedData newBakedData = new ElementAnimBakedData
        {
            p_elementName = elementName
        };

        // ---- 5. 八角 Mesh 用的全局 alpha 包围盒, 只在 HD 烘焙阶段内联收集一次 ----
        //   八角 Mesh UV 必须包住"alpha 掩膜 + bleed 外扩环"两部分,
        //   否则外扩出去的边缘色会被几何体直接裁切掉, alpha 通道下采样产生的
        //   抗锯齿羽边也无处可去.
        //   不再单独跑 pre-pass: 避免对每张源 PNG 多做一遍 CPU 读取.
        float globalMinX = 1f, globalMaxX = 0f, globalMinY = 1f, globalMaxY = 0f;
        bool hasAnyValidPixel = false;

        // ---- 6. 三档清晰度 BC7 烘焙 (与原版 Frame2TextureArray 严格一致) ----
        string[] resLayers = { "HD", "MD", "LD" };
        int[] resSizes = { 512, 256, 128 };

        Texture2DArray[] persistentColorArrays = new Texture2DArray[3];
        Texture2DArray[] persistentNormalArrays = new Texture2DArray[3];

        for (int i = 0; i < resLayers.Length; i++)
        {
            string currentLayer = resLayers[i];
            int targetSize = resSizes[i];

            // bleed 像素数按层级缩减: HD → MD (/2 上取整) → LD (再 /2 上取整), 与原版完全一致
            int currentBleedPixels = _bleedPixelsHD;
            if (currentLayer == "MD")
            {
                currentBleedPixels = Mathf.CeilToInt(_bleedPixelsHD / 2f);
            }
            else if (currentLayer == "LD")
            {
                int mdPixels = Mathf.CeilToInt(_bleedPixelsHD / 2f);
                currentBleedPixels = Mathf.CeilToInt(mdPixels / 2f);
            }

            Texture2DArray colorTexArray = new Texture2DArray(targetSize, targetSize, singleBlockTotalFrames, TextureFormat.BC7, true);
            Texture2DArray normalTexArray = new Texture2DArray(targetSize, targetSize, singleBlockTotalFrames, TextureFormat.BC7, true);

            int currentSliceIndex = 0;
            foreach (var st in scannedData)
            {
                foreach (var v in st.p_variations)
                {
                    foreach (var d in v.p_directions)
                    {
                        int frameCount = d.p_colorFrames.Count;
                        for (int f = 0; f < frameCount; f++)
                        {
                            Texture2D srcColorTex = d.p_colorFrames[f];
                            Texture2D srcNormalTex = (f < d.p_normalFrames.Count) ? d.p_normalFrames[f] : null;

                            // 只在 HD 档位顺路扫一次源 PNG 的 alpha 包围盒, 用作八角 Mesh 顶点;
                            // MD/LD 复用同一份结果, 杜绝重复 IO/解码.
                            if (currentLayer == "HD" && srcColorTex != null)
                            {
                                string srcPath = AssetDatabase.GetAssetPath(srcColorTex);
                                if (!string.IsNullOrEmpty(srcPath))
                                {
                                    Vector4 frameBound = AnalyzeAlphaBounds(srcPath, _bleedPixelsHD);
                                    if (frameBound != new Vector4(0f, 1f, 0f, 1f))
                                    {
                                        if (frameBound.x < globalMinX) globalMinX = frameBound.x;
                                        if (frameBound.y > globalMaxX) globalMaxX = frameBound.y;
                                        if (frameBound.z < globalMinY) globalMinY = frameBound.z;
                                        if (frameBound.w > globalMaxY) globalMaxY = frameBound.w;
                                        hasAnyValidPixel = true;
                                    }
                                }
                            }

                            // ---------- 写入 Color 切片页 ----------
                            if (srcColorTex != null)
                            {
                                // 传入当前层级计算出来的 bleed 外扩像素数 currentBleedPixels
                                Texture2D resizedTex = ResizeTextureWithMipmaps(srcColorTex, targetSize, targetSize, false, currentBleedPixels);

                                EditorUtility.CompressTexture(resizedTex, TextureFormat.BC7, TextureCompressionQuality.Best);

                                // 使用 GPU 级的 CopyTexture, 把主图及所有 Mipmap 层级强行精准拷贝进对应的切片页中
                                for (int mip = 0; mip < resizedTex.mipmapCount; mip++)
                                {
                                    Graphics.CopyTexture(resizedTex, 0, mip, colorTexArray, currentSliceIndex, mip);
                                }

                                DestroyImmediate(resizedTex);
                            }
                            else
                            {
                                // Color 缺帧透明填补
                                Texture2D emptyTex = new Texture2D(targetSize, targetSize, TextureFormat.RGBA32, true);
                                Color32[] emptyPixels = new Color32[targetSize * targetSize];
                                emptyTex.SetPixels32(emptyPixels);
                                emptyTex.Apply(true, false);
                                EditorUtility.CompressTexture(emptyTex, TextureFormat.BC7, TextureCompressionQuality.Best);
                                for (int mip = 0; mip < emptyTex.mipmapCount; mip++)
                                {
                                    Graphics.CopyTexture(emptyTex, 0, mip, colorTexArray, currentSliceIndex, mip);
                                }

                                DestroyImmediate(emptyTex);
                            }

                            // ---------- 写入 Normal 切片页 (法线分支走标准蓝清空 + alpha=0 处校准, 不做 bleed) ----------
                            // pre-bake 校验已保证此处 srcNormalTex 不为 null. 若仍为 null 说明扫描后
                            // 美术在烘焙过程中删了文件, 这是异常路径, 立即中断.
                            if (srcNormalTex == null)
                            {
                                EditorUtility.DisplayDialog("双轨打包中断！",
                                    $"【运行期法线丢失】\n\n" +
                                    $"元素: {elementName} / 状态: {st.p_stateName} / 变种: {v.p_varIndex} / 方向: Dir_{d.p_dirIndex} / 帧: {f}\n\n" +
                                    $"扫描时存在的法线在烘焙过程中突然消失, 请检查是否有第三方进程或美术正在删除文件.",
                                    "知道了");
                                DestroyImmediate(colorTexArray);
                                DestroyImmediate(normalTexArray);
                                return;
                            }

                            Texture2D resizedNormal = ResizeTextureWithMipmaps(srcNormalTex, targetSize, targetSize, true, 0);
                            EditorUtility.CompressTexture(resizedNormal, TextureFormat.BC7, TextureCompressionQuality.Best);
                            for (int mip = 0; mip < resizedNormal.mipmapCount; mip++)
                            {
                                Graphics.CopyTexture(resizedNormal, 0, mip, normalTexArray, currentSliceIndex, mip);
                            }

                            DestroyImmediate(resizedNormal);

                            currentSliceIndex++;
                        }
                    }
                }
            }

            // 因为是用 Graphics.CopyTexture 动态打入 GPU 的, 此处直接应用, 无需重新计算 Mips
            colorTexArray.Apply(false, false);
            normalTexArray.Apply(false, false);

            string colorAssetPath = $"{bakedFolder}/{elementName}_{currentLayer}_Color.asset";
            string normalAssetPath = $"{bakedFolder}/{elementName}_{currentLayer}_Normal.asset";

            // GUID 保护: 重新烘焙不会让 Material 的 Texture2DArray 引用断掉
            SaveAssetPreservingMeta(colorTexArray, colorAssetPath);
            SaveAssetPreservingMeta(normalTexArray, normalAssetPath);

            // 注册 Addressable 地址，供运行时通过 Addressables.LoadAssetAsync 加载
            RegisterAsAddressable(colorAssetPath,  $"{xmlPath}/{elementName}_{currentLayer}_Color");
            RegisterAsAddressable(normalAssetPath, $"{xmlPath}/{elementName}_{currentLayer}_Normal");

            persistentColorArrays[i] = AssetDatabase.LoadAssetAtPath<Texture2DArray>(colorAssetPath);
            persistentNormalArrays[i] = AssetDatabase.LoadAssetAtPath<Texture2DArray>(normalAssetPath);
        }

        newBakedData.p_hdColorArray = persistentColorArrays[0];
        newBakedData.p_mdColorArray = persistentColorArrays[1];
        newBakedData.p_ldColorArray = persistentColorArrays[2];
        newBakedData.p_hdNormalArray = persistentNormalArrays[0];
        newBakedData.p_mdNormalArray = persistentNormalArrays[1];
        newBakedData.p_ldNormalArray = persistentNormalArrays[2];

        // ---- 7. 根据全局最大包围盒生成八角形 Mesh (与原版严格一致) ----
        if (!hasAnyValidPixel)
        {
            globalMinX = 0f;
            globalMaxX = 1f;
            globalMinY = 0f;
            globalMaxY = 1f;
        }

        float w = globalMaxX - globalMinX;
        float h = globalMaxY - globalMinY;
        // 切角量由面板 "八角切角比例" (_cornerCutRatio) 控制，默认 0.10（10%）。
        // 设为 0 时退化为矩形包围盒，无切角风险；设为 0.15 时可多减约 10% Overdraw，
        // 但需目视验证攻击/技能帧的斜向外伸像素不会被几何裁掉。详见面板 Tooltip。
        float cutU = w * _cornerCutRatio;
        float cutV = h * _cornerCutRatio;

        Vector2[] uvs = new Vector2[8];
        uvs[0] = new Vector2(globalMinX + cutU, globalMaxY); // 顶左
        uvs[1] = new Vector2(globalMaxX - cutU, globalMaxY); // 顶右
        uvs[2] = new Vector2(globalMaxX, globalMaxY - cutV); // 右顶
        uvs[3] = new Vector2(globalMaxX, globalMinY + cutV); // 右底
        uvs[4] = new Vector2(globalMaxX - cutU, globalMinY); // 底右
        uvs[5] = new Vector2(globalMinX + cutU, globalMinY); // 底左
        uvs[6] = new Vector2(globalMinX, globalMinY + cutV); // 左底
        uvs[7] = new Vector2(globalMinX, globalMaxY - cutV); // 左顶

        Vector3[] vertices = new Vector3[8];
        // 八角 mesh 必须与 Unity 默认 Quad (vertex [-0.5,0.5]) 同源映射:
        //   vertex = UV - 0.5
        // 这样烘焙后的八角等价于"裁掉透明边的 Quad". Prefab 编辑期对 Quad 调好的
        // anim 节点 localPosition.y (用于让脚贴到根节点) 在运行时换成八角 mesh
        // 之后仍然零差异地继续生效, 不需要任何运行时校正.
        // 切勿改成 (UV.y - globalMinY): 那会把"全帧最低 alpha 像素 (常是影子)"当成 0 点,
        // 让脚在 mesh 中段而不是底端, 导致运行时角色明显比 prefab 编辑时高出一截.
        for (int vIdx = 0; vIdx < 8; vIdx++)
        {
            vertices[vIdx] = new Vector3(uvs[vIdx].x - 0.5f, uvs[vIdx].y - 0.5f, 0f);
        }

        int[] triangles = new int[]
        {
            0, 1, 2,
            0, 2, 3,
            0, 3, 4,
            0, 4, 5,
            0, 5, 6,
            0, 6, 7
        };

        Vector3[] normals = new Vector3[8];
        for (int nIdx = 0; nIdx < 8; nIdx++) normals[nIdx] = Vector3.forward;

        Mesh octagonMesh = new Mesh();
        octagonMesh.name = $"{elementName}_OctagonMesh";
        octagonMesh.vertices = vertices;
        octagonMesh.uv = uvs;
        octagonMesh.triangles = triangles;
        octagonMesh.normals = normals;
        octagonMesh.RecalculateTangents();

        // 将八角 Mesh 保存为独立 .asset 文件（与 Texture2DArray 同目录，保留 GUID）
        string meshAssetPath = $"{bakedFolder}/{elementName}_OctagonMesh.asset";
        SaveAssetPreservingMeta(octagonMesh, meshAssetPath);
        RegisterAsAddressable(meshAssetPath, $"{xmlPath}/{elementName}_OctagonMesh", "AnimationMeshs");
        newBakedData.p_bakedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshAssetPath);

        // ---- 8. 按扫描顺序回填 state/var/dir clip 偏移 ----
        // 结构说明 (与 AnimDefines.cs 同步):
        //   StateAnimData.p_isLoop              : 循环标志在状态级别, 所有变体共享.
        //   VariationAnimData.p_frameRate       : 播放速率倍率, 变体级别, 所有方向共享. 1.0 = 正常速度, 与 AnimCtrl._animFPS 配合使用.
        //   VariationAnimData.p_animFrameCount  : 每方向帧数, 变体内所有方向相同.
        //   VariationAnimData.p_eventFrame      : 关键帧, 变体级别; 帧数变化时自动重置为 -1.
        //   VariationAnimData.p_animStartOffset : List<int>, 每个方向在 Texture2DArray 中的绝对起始切片.
        int absoluteSliceOffset = 0;
        foreach (var tempState in scannedData)
        {
            // ── 状态级继承 ──────────────────────────────────────────────────
            //  p_isLoop: 永远继承老值; 首次烘焙时对 IDLE/MOVE/STUN 自动设 true.
            bool inheritedStateIsLoop = false;
            bool foundOldState = false;
            StateAnimData oldStateData = null;

            if (oldElementData != null)
            {
                oldStateData = oldElementData.p_stateAnim.FirstOrDefault(
                    s => s != null && s.p_stateName == tempState.p_stateName);
                if (oldStateData != null)
                {
                    foundOldState = true;
                    inheritedStateIsLoop = oldStateData.p_isLoop;
                }
            }

            bool defaultIsLoop = false; //默认不loop

            StateAnimData stateConfig = new StateAnimData
            {
                p_stateName = tempState.p_stateName,
                p_isLoop = foundOldState ? inheritedStateIsLoop : defaultIsLoop
            };

            foreach (var tempVar in tempState.p_variations)
            {
                // ── 变体级继承 ──────────────────────────────────────────────
                //  p_eventFrame: 帧数不变时继承, 否则重置为 -1.
                //  p_frameRate:  继承老值, 默认 1f (速率倍率, 1.0 = 正常速度).
                float inheritedFrameRate = 1f;
                int inheritedEventFrame = -1;
                bool foundOldVar = false;

                if (oldStateData != null && tempVar.p_varIndex < oldStateData.p_variations.Count)
                {
                    var oldVar = oldStateData.p_variations[tempVar.p_varIndex];
                    if (oldVar != null)
                    {
                        foundOldVar = true;
                        inheritedFrameRate = oldVar.p_frameRate;
                        inheritedEventFrame = oldVar.p_eventFrame;

                        // 帧数变化时 eventFrame 失效
                        if (inheritedEventFrame != -1 && tempVar.p_directions.Count > 0)
                        {
                            int newFrameCount = tempVar.p_directions[0].p_colorFrames.Count;
                            if (oldVar.p_animFrameCount != newFrameCount)
                                inheritedEventFrame = -1;
                        }
                    }
                }

                // 变体内所有方向使用相同帧数; 以第一个方向为准
                int frameCount = tempVar.p_directions.Count > 0
                    ? tempVar.p_directions[0].p_colorFrames.Count
                    : 0;

                VariationAnimData varConfig = new VariationAnimData
                {
                    p_frameRate = foundOldVar ? inheritedFrameRate : 1f,
                    p_animFrameCount = frameCount,
                    p_eventFrame = inheritedEventFrame,
                };

                // 每个方向记录一个绝对起始偏移
                foreach (var tempDir in tempVar.p_directions)
                {
                    varConfig.p_animStartOffset.Add(absoluteSliceOffset);
                    absoluteSliceOffset += tempDir.p_colorFrames.Count;
                }

                stateConfig.p_variations.Add(varConfig);
            }

            newBakedData.p_stateAnim.Add(stateConfig);
        }

        // ---- 9. 全量写入 CharacterAnimConf.xml（增量：首次追加，重复运行更新 path + states）----
        UpdateCharacterAnimConf(elementName, xmlPath, _description, newBakedData.p_stateAnim);

        // ---- 10. 输出日志 ----
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"<b><color=green>BC7 高压三档阵列打包成功！</color></b>");
        sb.AppendLine($"元素名称 (寻址 key): <color=yellow>{elementName}</color>");
        sb.AppendLine($"源目录 / 输出目录: <color=white>{bakedFolder}</color>");
        sb.AppendLine($"XML path 字段: <color=white>{xmlPath}</color>");
        sb.AppendLine($"<b>单 Block 总切片数: <color=cyan>{singleBlockTotalFrames}</color></b>");
        int sliceCursor = 0;
        foreach (var st in scannedData)
        {
            foreach (var v in st.p_variations)
            {
                foreach (var d in v.p_directions)
                {
                    sb.AppendLine($"  -> {st.p_stateName} | var={v.p_varIndex} | Dir_{d.p_dirIndex} | 帧 {d.p_colorFrames.Count} | offset <color=orange>{sliceCursor}</color>");
                    sliceCursor += d.p_colorFrames.Count;
                }
            }
        }

        Debug.Log(sb.ToString());

        EditorUtility.DisplayDialog("成功",
            $"[{elementName}] 的 HD/MD/LD 三档 BC7 阵列 + 八角 Mesh 已成功输出！\n" +
            $"输出目录: {bakedFolder}\n" +
            $"切片总量: {singleBlockTotalFrames}\np_eventFrame 已自动继承.",
            "OK");
    }

    // =========================================================
    //         XML / 路径辅助方法
    // =========================================================

    // =========================================================
    //         Addressables 注册
    // =========================================================

    // 将 assetPath 对应的资产注册（或更新）到指定 Addressables 组，并设置地址为 address。
    // groupName 默认为 "AnimationTextures"（贴图），Mesh 传入 "AnimationMeshs"。
    // AnimCtrl 运行时用相同的 address 字符串通过 Addressables.LoadAssetAsync 加载。
    private static void RegisterAsAddressable(string assetPath, string address,
                                              string groupName = "AnimationTextures")
    {
        AddressableAssetSettings aaSettings = AddressableAssetSettingsDefaultObject.Settings;
        if (aaSettings == null)
        {
            Debug.LogWarning("[Animation Baker] 未找到 Addressable Settings。" +
                             "请先打开 Window > Asset Management > Addressables > Groups 初始化。");
            return;
        }

        // 找到或创建指定组
        AddressableAssetGroup group = aaSettings.FindGroup(groupName);
        if (group == null)
        {
            group = aaSettings.CreateGroup(groupName, false, false, false,
                new List<AddressableAssetGroupSchema>
                {
                    ScriptableObject.CreateInstance<BundledAssetGroupSchema>(),
                    ScriptableObject.CreateInstance<ContentUpdateGroupSchema>()
                });
        }

        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid))
        {
            Debug.LogWarning($"[Animation Baker] 无法注册 Addressable：{assetPath} 的 GUID 为空，请先 AssetDatabase.Refresh()。");
            return;
        }

        AddressableAssetEntry entry = aaSettings.CreateOrMoveEntry(guid, group, false, false);
        entry.address = address;
        aaSettings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, true);
    }

    // 把 Assets-relative 路径规范化为去掉"Assets/"前缀的路径，用作 XML <path> 字段。
    // AnimCtrl 在编辑器运行时用 "Assets/" + path 重建 AssetDatabase 完整路径。
    private static string DeriveXmlPath(string folderPath)
    {
        const string assetsPrefix = "Assets/";
        return folderPath.StartsWith(assetsPrefix, StringComparison.OrdinalIgnoreCase)
            ? folderPath.Substring(assetsPrefix.Length)
            : folderPath;
    }

    // 确保 assetPath（Assets 相对）对应的目录在磁盘存在
    private static void EnsureUnityDirectory(string assetPath)
    {
        string absPath = Path.GetFullPath(
            assetPath.Replace("Assets", Application.dataPath.TrimEnd('/')));
        if (!Directory.Exists(absPath))
        {
            Directory.CreateDirectory(absPath);
            AssetDatabase.Refresh();
        }
    }

    // 从 CharacterAnimConf.xml 读取指定元素的旧状态数据，用于继承 isLoop/eventFrame/frameRate。
    private static ElementAnimBakedData LoadOldElementDataFromXml(string elementName)
    {
        string absXmlPath = Path.GetFullPath(
            ANIM_CONF_XML_PATH.Replace("Assets", Application.dataPath.TrimEnd('/')));
        if (!File.Exists(absXmlPath)) return null;

        XmlDocument doc = new XmlDocument();
        doc.Load(absXmlPath);

        XmlNodeList nodes = doc.SelectNodes("characterAnimCfgs/anim");
        if (nodes == null) return null;

        foreach (XmlNode node in nodes)
        {
            if (node.NodeType != XmlNodeType.Element) continue;
            string name = node.SelectSingleNode("name")?.InnerText?.Trim();
            if (name != elementName) continue;

            return new ElementAnimBakedData
            {
                p_elementName = elementName,
                p_stateAnim   = ParseStatesFromXmlNode(node.SelectSingleNode("states"))
            };
        }
        return null;
    }

    // 在 CharacterAnimConf.xml 中增量写入或更新一条 <anim> 记录（含完整状态数据）。
    // 若 <name> 已存在：更新 <path> 和 <states>；<description> 仅在用户填了内容时覆盖。
    // 若 <name> 不存在：追加新节点。
    private static void UpdateCharacterAnimConf(
        string elementName, string xmlPath, string description, List<StateAnimData> stateAnim)
    {
        string absXmlPath = Path.GetFullPath(
            ANIM_CONF_XML_PATH.Replace("Assets", Application.dataPath.TrimEnd('/')));

        XmlDocument doc = new XmlDocument();
        if (File.Exists(absXmlPath))
        {
            doc.Load(absXmlPath);
        }
        else
        {
            EnsureUnityDirectory(System.IO.Path.GetDirectoryName(ANIM_CONF_XML_PATH));
            doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-8", null));
            doc.AppendChild(doc.CreateElement("characterAnimCfgs"));
        }

        XmlElement root = doc.DocumentElement;
        if (root == null)
        {
            Debug.LogError($"[Animation Baker] {ANIM_CONF_XML_PATH} 结构异常，根节点缺失。");
            return;
        }

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

        if (existingAnim != null)
        {
            // 更新 <path>
            XmlNode pathNode = existingAnim.SelectSingleNode("path");
            if (pathNode != null) pathNode.InnerText = xmlPath;
            else
            {
                var pe = doc.CreateElement("path"); pe.InnerText = xmlPath;
                existingAnim.AppendChild(pe);
            }

            // 更新 <description>（仅在用户输入了内容时覆盖，否则保留原值）
            if (!string.IsNullOrEmpty(description))
            {
                XmlNode descNode = existingAnim.SelectSingleNode("description");
                if (descNode != null) descNode.InnerText = description;
                else
                {
                    var de = doc.CreateElement("description"); de.InnerText = description;
                    existingAnim.InsertBefore(de, existingAnim.FirstChild);
                }
            }

            // 全量替换 <states>
            XmlNode oldStates = existingAnim.SelectSingleNode("states");
            if (oldStates != null) existingAnim.RemoveChild(oldStates);
            existingAnim.AppendChild(BuildStatesXml(doc, stateAnim));
        }
        else
        {
            // 追加新 <anim> 节点
            XmlElement animElem = doc.CreateElement("anim");

            var de = doc.CreateElement("description"); de.InnerText = description ?? "";
            animElem.AppendChild(de);
            var ne = doc.CreateElement("name"); ne.InnerText = elementName;
            animElem.AppendChild(ne);
            var pe = doc.CreateElement("path"); pe.InnerText = xmlPath;
            animElem.AppendChild(pe);
            animElem.AppendChild(BuildStatesXml(doc, stateAnim));

            root.AppendChild(animElem);
        }

        var settings = new XmlWriterSettings { Indent = true, IndentChars = "    ", Encoding = Encoding.UTF8 };
        using (XmlWriter writer = XmlWriter.Create(absXmlPath, settings))
            doc.Save(writer);

        AssetDatabase.ImportAsset(ANIM_CONF_XML_PATH, ImportAssetOptions.ForceUpdate);
        Debug.Log($"[Animation Baker] CharacterAnimConf.xml 已更新：{elementName} → {xmlPath}（{stateAnim?.Count ?? 0} states）");
    }

    // 将 List<StateAnimData> 序列化为 <states>/<state>/<variation> XML 节点树
    private static XmlElement BuildStatesXml(XmlDocument doc, List<StateAnimData> stateAnim)
    {
        XmlElement statesElem = doc.CreateElement("states");
        if (stateAnim == null) return statesElem;

        foreach (var state in stateAnim)
        {
            XmlElement stateElem = doc.CreateElement("state");
            stateElem.SetAttribute("name",   state.p_stateName ?? "");
            stateElem.SetAttribute("isLoop", state.p_isLoop ? "true" : "false");

            for (int vi = 0; vi < state.p_variations.Count; vi++)
            {
                var v = state.p_variations[vi];
                XmlElement varElem = doc.CreateElement("variation");
                varElem.SetAttribute("index",      vi.ToString());
                varElem.SetAttribute("frameCount", v.p_animFrameCount.ToString());
                varElem.SetAttribute("frameRate",
                    v.p_frameRate.ToString("G", System.Globalization.CultureInfo.InvariantCulture));
                varElem.SetAttribute("eventFrame", v.p_eventFrame.ToString());
                varElem.SetAttribute("offsets",    string.Join(",", v.p_animStartOffset));
                stateElem.AppendChild(varElem);
            }

            statesElem.AppendChild(stateElem);
        }
        return statesElem;
    }

    // 将 <states> XML 节点树解析为 List<StateAnimData>（与 AnimCtrl.ParseStatesFromXml 逻辑一致）
    private static List<StateAnimData> ParseStatesFromXmlNode(XmlNode statesNode)
    {
        var result = new List<StateAnimData>();
        if (statesNode == null) return result;

        foreach (XmlNode stateNode in statesNode.ChildNodes)
        {
            if (stateNode.NodeType != XmlNodeType.Element) continue;

            string stateName = stateNode.Attributes?["name"]?.Value ?? "";
            bool   isLoop    = string.Equals(stateNode.Attributes?["isLoop"]?.Value, "true",
                                   StringComparison.OrdinalIgnoreCase);
            var stateData = new StateAnimData { p_stateName = stateName, p_isLoop = isLoop };

            foreach (XmlNode varNode in stateNode.ChildNodes)
            {
                if (varNode.NodeType != XmlNodeType.Element) continue;

                int.TryParse(varNode.Attributes?["frameCount"]?.Value, out int fc);
                float.TryParse(varNode.Attributes?["frameRate"]?.Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float fr);
                if (fr == 0f) fr = 1f;
                int.TryParse(varNode.Attributes?["eventFrame"]?.Value, out int ef);
                if (!int.TryParse(varNode.Attributes?["eventFrame"]?.Value, out _)) ef = -1;

                string offsetsStr = varNode.Attributes?["offsets"]?.Value ?? "";
                var varData = new VariationAnimData
                    { p_animFrameCount = fc, p_frameRate = fr, p_eventFrame = ef };

                foreach (string part in offsetsStr.Split(','))
                    if (int.TryParse(part.Trim(), out int off))
                        varData.p_animStartOffset.Add(off);

                stateData.p_variations.Add(varData);
            }
            result.Add(stateData);
        }
        return result;
    }

    // 从 XML 读取某元素已有的 description（切换目录时回填 UI 输入框用）
    private static string TryGetExistingDescription(string elementName)
    {
        string absXmlPath = Path.GetFullPath(
            ANIM_CONF_XML_PATH.Replace("Assets", Application.dataPath.TrimEnd('/')));
        if (!File.Exists(absXmlPath)) return "";

        XmlDocument doc = new XmlDocument();
        doc.Load(absXmlPath);

        XmlNodeList animNodes = doc.SelectNodes("characterAnimCfgs/anim");
        if (animNodes == null) return "";

        foreach (XmlNode node in animNodes)
        {
            if (node.NodeType != XmlNodeType.Element) continue;
            if (node.SelectSingleNode("name")?.InnerText?.Trim() != elementName) continue;
            return node.SelectSingleNode("description")?.InnerText?.Trim() ?? "";
        }
        return "";
    }

    // =========================================================
    //         法线完整性 pre-bake 校验 (一次性硬卡)
    // =========================================================
    // 烘焙启动前彻底扫一遍 (state, var, dir, frame): 任何色帧没有配对的法线就
    // 直接抛对话框 + return. 把所有缺失点一次性聚合, 美术只要补一轮就够,
    // 不会出现"烘到第 N 帧才报第 1 个缺帧, 改完再跑又报第 2 个"的循环.
    private static bool ValidateNormalCompletenessOrAbort(List<TempStateData> scannedData, string elementName)
    {
        List<string> missing = new List<string>();
        foreach (var st in scannedData)
        {
            foreach (var v in st.p_variations)
            {
                foreach (var d in v.p_directions)
                {
                    int colorCount = d.p_colorFrames.Count;
                    // 双轨索引必须一一对应; 任一边缺失或长度对不齐都算缺
                    for (int f = 0; f < colorCount; f++)
                    {
                        bool normalMissing =
                            (f >= d.p_normalFrames.Count) || (d.p_normalFrames[f] == null);
                        if (normalMissing)
                        {
                            missing.Add($"{st.p_stateName} / var={v.p_varIndex} / Dir_{d.p_dirIndex} / frame {f}");
                        }
                    }
                }
            }
        }

        if (missing.Count == 0) return true;

        // 聚合时只展示前 30 条, 其余汇总
        const int SHOW_LIMIT = 30;
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"【法线缺失, 拒绝烘焙】元素 [{elementName}] 共发现 {missing.Count} 处色帧没有配对的 _normal.png:\n");
        for (int i = 0; i < Mathf.Min(SHOW_LIMIT, missing.Count); i++)
        {
            sb.AppendLine($"  - {missing[i]}");
        }

        if (missing.Count > SHOW_LIMIT)
        {
            sb.AppendLine($"  ... 另外还有 {missing.Count - SHOW_LIMIT} 处, 详情见 Console.");
        }

        sb.AppendLine("\n美术约定: 法线图必须与色帧同目录, 文件名为 <色帧 basename>_normal.png.");
        sb.AppendLine("请补齐后重新点击烘焙. 在补齐之前, 不会生成任何 .asset 文件.");

        string dialog = sb.ToString();
        Debug.LogError(dialog);
        EditorUtility.DisplayDialog("法线缺失, 烘焙拒绝启动", dialog, "知道了");
        return false;
    }

    // =========================================================
    //              美术目录树扫描 (变种归一化)
    // =========================================================
    private List<TempStateData> ScanElementAnimationFolder(string rootPath)
    {
        List<TempStateData> stateList = new List<TempStateData>();
        var cmp = new NaturalStringComparer();

        // 状态目录: 自然顺序稳定排序, 防止 NTFS MFT 顺序漂移导致 offset 抖动
        string[] stateDirs = Directory.GetDirectories(rootPath)
            .OrderBy(d => Path.GetFileName(d), cmp)
            .ToArray();

        foreach (var stateDir in stateDirs)
        {
            string stateName = Path.GetFileName(stateDir);

            // 显式跳过自身烘焙输出目录, 防止"上次烘焙的 BakedArrays 被当成下一次的 State"
            if (string.Equals(stateName, BAKED_OUTPUT_SUBFOLDER, StringComparison.OrdinalIgnoreCase))
                continue;
            // 兼容原版 Frame2TextureArray 的输出目录命名
            if (string.Equals(stateName, "Packed_Arrays", StringComparison.OrdinalIgnoreCase))
                continue;

            TempStateData stateData = new TempStateData
            {
                p_stateName = stateName
            };

            string[] subDirs = Directory.GetDirectories(stateDir);
            bool hasDirectDirectionFolders = subDirs.Any(IsDirectionFolder);
            var numericSubDirs = subDirs
                .Where(d => int.TryParse(Path.GetFileName(d), out _))
                .OrderBy(d => int.Parse(Path.GetFileName(d)))
                .ToList();

            if (hasDirectDirectionFolders)
            {
                // <state>/Dir_x: 单一具体动画, 归一为 variation 0.
                TempVariationData defaultVar = new TempVariationData { p_varIndex = stateData.p_variations.Count };
                CollectDirectionFrames(stateDir, defaultVar);
                if (defaultVar.p_directions.Count > 0) stateData.p_variations.Add(defaultVar);
            }

            for (int i = 0; i < numericSubDirs.Count; i++)
            {
                // <state>/<1/2/3...>/Dir_x: 一个动画名下的多个具体动画, 按目录数字稳定排序.
                TempVariationData varData = new TempVariationData { p_varIndex = stateData.p_variations.Count };
                CollectDirectionFrames(numericSubDirs[i], varData);
                if (varData.p_directions.Count > 0) stateData.p_variations.Add(varData);
            }

            var ignoredSubDirs = subDirs
                .Where(d => !IsDirectionFolder(d) && !int.TryParse(Path.GetFileName(d), out _))
                .Where(d => !string.Equals(Path.GetFileName(d), BAKED_OUTPUT_SUBFOLDER, StringComparison.OrdinalIgnoreCase))
                .Where(d => !string.Equals(Path.GetFileName(d), "Packed_Arrays", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (ignoredSubDirs.Count > 0)
            {
                StringBuilder warn = new StringBuilder();
                warn.Append($"[Frame2TextureArray] 动画 [{stateName}] 下存在无法识别的子目录, 未参与烘焙:\n");
                foreach (var ignored in ignoredSubDirs)
                    warn.AppendLine($"    - {ignored}");
                warn.AppendLine("有效结构: <动画名>/Dir_x 或 <动画名>/<1/2/3...>/Dir_x.");
                Debug.LogWarning(warn.ToString());
            }

            if (stateData.p_variations.Count > 0) stateList.Add(stateData);
        }

        return stateList;
    }

    private void CollectDirectionFrames(string path, TempVariationData varData)
    {
        var cmp = new NaturalStringComparer();
        string[] dirDirs = Directory.GetDirectories(path, "Dir_*")
            .OrderBy(d => Path.GetFileName(d), cmp)
            .ToArray();

        foreach (var dirDir in dirDirs)
        {
            string folderName = Path.GetFileName(dirDir);
            if (!int.TryParse(folderName.Replace("Dir_", ""), out int dirIndex))
                continue;

            TempDirectionClip dirClip = new TempDirectionClip { p_dirIndex = dirIndex };
            CollectDirectionPngFrames(dirDir, dirClip, cmp);

            if (dirClip.p_colorFrames.Count > 0)
                varData.p_directions.Add(dirClip);
        }

        // 方向列表按 dirIndex 升序, 保证 packing 顺序完全确定 (Dir_0, Dir_1, ..., Dir_7)
        varData.p_directions = varData.p_directions.OrderBy(d => d.p_dirIndex).ToList();
    }

    private static bool IsDirectionFolder(string path)
    {
        string folderName = Path.GetFileName(path);
        return folderName.StartsWith("Dir_", StringComparison.OrdinalIgnoreCase) &&
               int.TryParse(folderName.Substring(4), out _);
    }

    private static void CollectDirectionPngFrames(string dirDir, TempDirectionClip dirClip, NaturalStringComparer cmp)
    {
        string[] directColorFiles = Directory.GetFiles(dirDir, "*.png")
            .Where(f => !f.EndsWith(NORMAL_SUFFIX, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, cmp)
            .ToArray();

        if (directColorFiles.Length > 0)
        {
            foreach (var file in directColorFiles)
            {
                AddColorAndAdjacentNormal(file, dirClip);
            }
            return;
        }

        string hdColorDir = Path.Combine(dirDir, "HD", "Color");
        string hdNormalDir = Path.Combine(dirDir, "HD", "Normal");
        if (!Directory.Exists(hdNormalDir))
            hdNormalDir = Path.Combine(dirDir, "HD", "Normals");
        if (!Directory.Exists(hdColorDir))
            return;

        string[] hdColorFiles = Directory.GetFiles(hdColorDir, "*.png")
            .OrderBy(f => f, cmp)
            .ToArray();

        foreach (var file in hdColorFiles)
        {
            Texture2D colorTex = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalizeAssetPath(file));
            if (colorTex == null) continue;

            dirClip.p_colorFrames.Add(colorTex);

            string normalPath = Path.Combine(hdNormalDir, Path.GetFileName(file));
            Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalizeAssetPath(normalPath));
            dirClip.p_normalFrames.Add(normalTex);
        }
    }

    private static void AddColorAndAdjacentNormal(string file, TempDirectionClip dirClip)
    {
        Texture2D colorTex = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalizeAssetPath(file));
        if (colorTex == null) return;

        dirClip.p_colorFrames.Add(colorTex);

        string dir = Path.GetDirectoryName(file);
        string baseNameNoExt = Path.GetFileNameWithoutExtension(file);
        string normalPath = Path.Combine(dir, $"{baseNameNoExt}{NORMAL_SUFFIX}");
        Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalizeAssetPath(normalPath));
        dirClip.p_normalFrames.Add(normalTex);
    }

    private static string NormalizeAssetPath(string path)
    {
        string normalized = path.Replace('\\', '/');
        string dataPath = Application.dataPath.Replace('\\', '/');
        if (normalized.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
            return "Assets" + normalized.Substring(dataPath.Length);
        return normalized;
    }

    // =========================================================
    //   下面这一段全部复刻自原版 Frame2TextureArray, 一行不改:
    //   - SaveAssetPreservingMeta (GUID 保护)
    //   - AnalyzeAlphaBounds      (A 通道边界 + bleed padding)
    //   - ResizeTextureWithMipmaps (源分辨率 bleed → Blit 下采样 → mipmap 链)
    //   - LoadAndBleedSourcePNG    (源 PNG 上做颜色外扩)
    //   - ApplyAlphaBleed          (8-邻接 BFS 多源膨胀)
    // =========================================================

    // 强制保护原有 .meta 文件
    private static void SaveAssetPreservingMeta(UnityEngine.Object asset, string assetPath)
    {
        // 注意：不在 CreateAsset 之前设置 asset.name——Unity 的 CreateAsset 会把对象名
        //       强制覆写为临时文件名（"temp_array_bake"），提前设置无效。
        //       正确做法：在文件落到最终路径后再 Load 回来改名（见步骤 4）。
        string tempPath = "Assets/temp_array_bake.asset";

        // 1. 先将新生成的贴图阵列资产创建到临时的隐藏路径
        AssetDatabase.CreateAsset(asset, tempPath);
        AssetDatabase.SaveAssets();

        // 2. 判断目标路径是否已经存在老资产（覆盖式重新生成）
        if (File.Exists(assetPath))
        {
            try
            {
                // 【核心外挂操作】仅复制二进制数据文件，不碰 .meta，保留原有 GUID
                File.Copy(tempPath, assetPath, true);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[分身替换失败] 覆盖物理文件时发生错误: {e.Message}");
            }

            AssetDatabase.DeleteAsset(tempPath);
        }
        else
        {
            AssetDatabase.MoveAsset(tempPath, assetPath);
        }

        // 3. 修正内部对象名：CreateAsset 将名字锁定为 "temp_array_bake"，
        //    在资产落到目标路径后 Load 回来改名，让内部名称与文件名一致。
        string targetName = Path.GetFileNameWithoutExtension(assetPath);
        var saved = AssetDatabase.LoadMainAssetAtPath(assetPath);
        if (saved != null && saved.name != targetName)
        {
            saved.name = targetName;
            EditorUtility.SetDirty(saved);
            AssetDatabase.SaveAssets();
        }
    }

    // 高效率图片 A 通道边缘检测
    private static Vector4 AnalyzeAlphaBounds(string assetPath, int bleedPixelsHD)
    {
        byte[] fileData = File.ReadAllBytes(assetPath);
        Texture2D rawTex = new Texture2D(2, 2);
        rawTex.LoadImage(fileData); // 绕过 Unity 的导入只读限制, 直接解开二进制像素

        int w = rawTex.width;
        int h = rawTex.height;
        Color32[] pixels = rawTex.GetPixels32();

        int minX = w, maxX = 0, minY = h, maxY = 0;
        bool hasPixel = false;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // 如果当前像素的 Alpha 大于 12 (约 0.05 阈值), 判定为有效身体像素
                if (pixels[x + y * w].a > 12)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                    hasPixel = true;
                }
            }
        }

        DestroyImmediate(rawTex);

        if (!hasPixel) return new Vector4(0f, 1f, 0f, 1f); // 纯透明空图保底

        // padding 与 bleed 外扩宽度对齐: HD 烘焙 (512) 用 bleedPixelsHD 像素的颜色外扩,
        // 把它换算到当前源 PNG 分辨率 (通常是 1024+), 让八角 Mesh 完整覆盖
        // [身体 + bleed 外扩环] 整体. 至少保留 1 像素的物理防抖羽化边.
        int padding = Mathf.Max(1, Mathf.RoundToInt(bleedPixelsHD * w / 512f));
        minX = Mathf.Max(0, minX - padding);
        maxX = Mathf.Min(w - 1, maxX + padding);
        minY = Mathf.Max(0, minY - padding);
        maxY = Mathf.Min(h - 1, maxY + padding);

        // 换算为 Shader 顶点着色器需要的 0.0 ~ 1.0 的标准归一化比例矩形
        return new Vector4((float)minX / w, (float)maxX / w, (float)minY / h, (float)maxY / h);
    }

    private static Texture2D ResizeTextureWithMipmaps(Texture2D source, int width, int height, bool isNormal, int bleedPixels)
    {
        // 1. 关键: 颜色贴图开启 bleed 时, 在【源 PNG 原始分辨率】上先把"最近身体像素的 RGB"
        //    向透明区外扩 (alpha 仍保持 0), 再让 Blit 做硬件双线性下采样.
        //    这样不论是 bilinear 采样还是后续 mipmap 链下采样, 在身体外圈读到的 RGB 始终是
        //    身体色 (而不是黑), 硬切 alpha test 只会切掉 alpha 不够的羽边, 边缘永远不会暗化.
        //    BC7 也只需拟合身体内外平滑过渡的 RGB, 不再有"彩色 ↔ 黑"的高对比块, 噪点根治.
        Texture2D effectiveSource = source;
        Texture2D tempBleedSource = null;
        if (!isNormal && bleedPixels > 0)
        {
            // 将目标贴图实际宽高 (width) 传入烘焙方法, 作为映射缩放基准
            tempBleedSource = LoadAndBleedSourcePNG(source, bleedPixels, width);
            if (tempBleedSource != null)
                effectiveSource = tempBleedSource;
        }

        // 2. 获取临时渲染纹理并激活配置
        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        RenderTexture.active = rt;

        // 【智能防线 1: 根据类型清空 RenderTexture 底色】
        // 法线贴图必须用标准法线蓝 (0.5, 0.5, 1.0, 0.0) 填充底色, 防止 GPU 硬件插值发黑.
        // 颜色贴图也用法线蓝以外的中性透明色清空, 但因为 bleed 已经把身体色扩到很远, 这里
        // 清成什么颜色实际上都不会影响身体外圈的视觉 —— 远端透明区由 bleed 的尾端覆盖.
        Color clearColor = isNormal ? new Color(0.5f, 0.5f, 1.0f, 0.0f) : new Color(0f, 0f, 0f, 0f);
        GL.Clear(true, true, clearColor);

        effectiveSource.filterMode = FilterMode.Bilinear;
        Graphics.Blit(effectiveSource, rt);

        // 3. 将缩放后的像素读入未压缩的标准 RGBA32 纹理中
        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, true);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);

        // 4. 像素级最终清洗
        //    - 法线贴图: 把完全透明的外部像素 RGB 校准为标准法线蓝, 防止 mip 出现黑圈.
        //    - 颜色贴图: 【绝对不能再清 0】. bleed 把身体色外扩出去就是为了让透明区也保留身体色,
        //                这里若再把 alpha=0 的 RGB 强制清 0, 等于把 bleed 工作完全擦回去,
        //                bilinear / mipmap 又会读到 (色 ↔ 黑) 的高对比, 黑环回归.
        Color32[] pixels = result.GetPixels32();

        if (isNormal)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a == 0)
                {
                    pixels[i].r = 128;
                    pixels[i].g = 128;
                    pixels[i].b = 255;
                }
            }

            result.SetPixels32(pixels);
        }
        else if (bleedPixels <= 0)
        {
            // 【debug 回退路径】 bleed 关闭时, 兜底把 alpha=0 像素 RGB 清 0, 行为与历史版本一致.
            // 注意此路径会带回 cutout 边缘暗化问题, 仅做对照用.
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a == 0)
                {
                    pixels[i].r = 0;
                    pixels[i].g = 0;
                    pixels[i].b = 0;
                }
            }

            result.SetPixels32(pixels);
        }
        // bleed > 0 时直接保留 ReadPixels 的原始数据, 不做任何 RGB 改写.

        // 5. 基于彻底清洗干净、两轨各自完美的底层 RGB 色彩, 强行重新建立未压缩状态下的所有 Mipmap 层级链
        result.Apply(true, false);

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        if (tempBleedSource != null) DestroyImmediate(tempBleedSource);
        return result;
    }

    // 在源 PNG 原始分辨率上做 Alpha Bleed (颜色外扩), 返回新的可读 Texture2D.
    // 之所以必须在源分辨率而不是输出分辨率做, 是因为:
    // Blit 下采样会先把源的 AA 羽边 (alpha 0~127, RGB=身体色) 压成输出端的"低 alpha 半透矩形",
    // 此时再用 alpha>=128 阈值二值化会把这段"本来属于身体可见区域的羽边"误判为外圈
    // → 身体可见轮廓被吃掉一圈.
    // 改在源分辨率做 bleed 后, 经过 Blit 下采样的是 [身体色 alpha=255] 与 [外扩身体色 alpha=0]
    // 在 RGB 上完全连续, 在 alpha 上单调下降, 身体颜色完整保留, 边缘干净.
    private static Texture2D LoadAndBleedSourcePNG(Texture2D source, int bleedPixels, int targetSize)
    {
        string assetPath = AssetDatabase.GetAssetPath(source);
        if (string.IsNullOrEmpty(assetPath))
            return null;

        byte[] fileData;
        try
        {
            fileData = File.ReadAllBytes(assetPath);
        }
        catch
        {
            return null;
        }

        Texture2D bled = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!bled.LoadImage(fileData))
        {
            DestroyImmediate(bled);
            return null;
        }

        int srcW = bled.width;
        int srcH = bled.height;
        // 把 HD (512) 基线的 bleed 像素数, 按比例放大到当前源分辨率
        // (源通常是 1024+, 这样 HD 烘焙时下采样会自然得到约 bleedPixels 像素的有效 bleed)
        int srcBleedThickness = Mathf.Max(1, Mathf.RoundToInt((float)bleedPixels * srcW / targetSize));

        Color32[] srcPixels = bled.GetPixels32();
        ApplyAlphaBleed(srcPixels, srcW, srcH, srcBleedThickness);
        bled.SetPixels32(srcPixels);
        bled.Apply(false, false);
        return bled;
    }

    // Alpha Bleed (颜色外扩): 以 alpha >= 128 的像素为"身体种子",
    // 用 8-邻接多源 BFS 向外膨胀 bleedThickness 像素, 每个外部像素拷贝【最近身体像素的 RGB】,
    // alpha 仍保持 0. 同时把身体内部 alpha 强制二值化为 255 (抹掉 Blender 原始抗锯齿羽边).
    //
    // 这样的好处:
    // 1) bilinear 采样 / mipmap 链下采样, 在身体外圈读到的 RGB 始终是身体色, 不会暗化.
    // 2) 硬切 alpha test 只切 alpha 不够的羽边, 边缘永远不会出现黑环.
    // 3) BC7 端点不再需要拟合"彩色 ↔ 黑"的高对比, RGB 在身体内外都是连续色块, 块噪声大幅降低.
    // 4) 边缘的视觉抗锯齿由 alpha 通道下采样时自然产生 (255 ↔ 0 平均), 与原始 PNG 羽边等价.
    private static void ApplyAlphaBleed(Color32[] pixels, int W, int H, int bleedThickness)
    {
        const byte BODY_ALPHA_THRESHOLD = 128;
        int N = W * H;

        // distMap[i] = 0          → 身体内部 (种子)
        // distMap[i] ∈ (0, T]     → bleed 外扩环 (含原始 0 < alpha < 128 的抗锯齿羽边)
        // distMap[i] = MaxValue   → 远离身体的纯外部 (alpha 已是 0, RGB 不动也无所谓)
        // nearestSeed[i] = 该像素 BFS 路径上的最近身体种子像素索引, 用来拷贝 RGB
        int[] distMap = new int[N];
        int[] nearestSeed = new int[N];
        for (int i = 0; i < N; i++)
        {
            distMap[i] = int.MaxValue;
            nearestSeed[i] = -1;
        }

        Queue<int> bfs = new Queue<int>(N / 8 + 64);
        for (int i = 0; i < N; i++)
        {
            if (pixels[i].a >= BODY_ALPHA_THRESHOLD)
            {
                distMap[i] = 0;
                nearestSeed[i] = i;
                bfs.Enqueue(i);
            }
        }

        // 8-邻接 BFS: 外扩形状趋近圆形, 比 4-邻接的"菱形"更自然
        while (bfs.Count > 0)
        {
            int idx = bfs.Dequeue();
            int d = distMap[idx];
            if (d >= bleedThickness) continue;
            int x = idx % W;
            int y = idx / W;
            int nd = d + 1;
            int seed = nearestSeed[idx];

            // 展开 8 个方向, 避免分支表 / array 索引开销
            if (x > 0)
            {
                int n = idx - 1;
                if (nd < distMap[n])
                {
                    distMap[n] = nd;
                    nearestSeed[n] = seed;
                    bfs.Enqueue(n);
                }
            }

            if (x < W - 1)
            {
                int n = idx + 1;
                if (nd < distMap[n])
                {
                    distMap[n] = nd;
                    nearestSeed[n] = seed;
                    bfs.Enqueue(n);
                }
            }

            if (y > 0)
            {
                int n = idx - W;
                if (nd < distMap[n])
                {
                    distMap[n] = nd;
                    nearestSeed[n] = seed;
                    bfs.Enqueue(n);
                }
            }

            if (y < H - 1)
            {
                int n = idx + W;
                if (nd < distMap[n])
                {
                    distMap[n] = nd;
                    nearestSeed[n] = seed;
                    bfs.Enqueue(n);
                }
            }

            if (x > 0 && y > 0)
            {
                int n = idx - W - 1;
                if (nd < distMap[n])
                {
                    distMap[n] = nd;
                    nearestSeed[n] = seed;
                    bfs.Enqueue(n);
                }
            }

            if (x < W - 1 && y > 0)
            {
                int n = idx - W + 1;
                if (nd < distMap[n])
                {
                    distMap[n] = nd;
                    nearestSeed[n] = seed;
                    bfs.Enqueue(n);
                }
            }

            if (x > 0 && y < H - 1)
            {
                int n = idx + W - 1;
                if (nd < distMap[n])
                {
                    distMap[n] = nd;
                    nearestSeed[n] = seed;
                    bfs.Enqueue(n);
                }
            }

            if (x < W - 1 && y < H - 1)
            {
                int n = idx + W + 1;
                if (nd < distMap[n])
                {
                    distMap[n] = nd;
                    nearestSeed[n] = seed;
                    bfs.Enqueue(n);
                }
            }
        }

        // 三段式色彩重写:
        //   身体内部     → alpha 二值化为 255, RGB 保留原色
        //   bleed 外扩环 → RGB 拷贝最近身体种子的颜色, alpha 强制为 0
        //   远外纯透明区 → 直接 alpha=0, RGB 不动 (反正不会被采样到, 即便被采样到也是远端 mip,
        //                  在那个尺度上身体已经被 alpha=0 的 mip 平均稀释掉了)
        for (int i = 0; i < N; i++)
        {
            int d = distMap[i];
            if (d == 0)
            {
                pixels[i].a = 255;
            }
            else if (d <= bleedThickness && nearestSeed[i] >= 0)
            {
                Color32 seedColor = pixels[nearestSeed[i]];
                pixels[i].r = seedColor.r;
                pixels[i].g = seedColor.g;
                pixels[i].b = seedColor.b;
                pixels[i].a = 0;
            }
            else
            {
                pixels[i].a = 0;
            }
        }
    }
}
