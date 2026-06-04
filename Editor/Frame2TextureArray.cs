using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEditor;
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

// 万能解耦帧动画烘焙机. 接管原版 Frame2TextureArray 的 HD/MD/LD BC7 渲染管线与八角 Mesh 生成,
// 但业务层从 Soldier 升级到通用 Element, 烘焙写入 GlobalAnimConfig.
public class Frame2TextureArray : EditorWindow
{
    private const string GLOBAL_CONFIG_FOLDER = "Assets/Animation";
    private const string GLOBAL_CONFIG_PATH = GLOBAL_CONFIG_FOLDER + "/GlobalAnimConfig.asset";

    // ============== UI 状态 ==============
    private GlobalAnimConfig p_globalConfig;
    private DefaultAsset targetElementFolder;

    // Alpha Bleed (颜色外扩) 宽度: HD (512²) 档位下把身体最近邻 RGB 向透明区外扩多少像素.
    // MD/LD 自动按分辨率比例缩减. 设为 0 关闭外扩, 回退到 alpha=0 → RGB 洗黑的旧行为
    // (会带回 cutout 暗边问题, 仅作 debug 对照). 推荐 HD 8 px 起步.
    private int _bleedPixelsHD = 8;

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

        p_globalConfig = AssetDatabase.LoadAssetAtPath<GlobalAnimConfig>(GLOBAL_CONFIG_PATH);
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.TextField("GlobalAnimConfig", GLOBAL_CONFIG_PATH);
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.BeginHorizontal();
        targetElementFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "待烘焙元素根目录:", targetElementFolder, typeof(DefaultAsset), false);
        if (GUILayout.Button("浏览...", GUILayout.Width(60)))
        {
            string defaultPath = targetElementFolder != null
                ? AssetDatabase.GetAssetPath(targetElementFolder)
                : "Assets";
            string absolutePath = EditorUtility.OpenFolderPanel("弹出对话框：选择元素根目录", defaultPath, "");
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

        GUILayout.Space(10);

        bool canBake = targetElementFolder != null;
        if (!canBake)
        {
            EditorGUILayout.HelpBox("请先指派元素根目录。GlobalAnimConfig 会在烘焙时自动创建或更新。", MessageType.Warning);
        }
        else
        {
            string elemPath = AssetDatabase.GetAssetPath(targetElementFolder);
            string configStatus = p_globalConfig == null ? "烘焙时自动创建" : "烘焙时更新现有配置";
            EditorGUILayout.HelpBox($"GlobalAnimConfig: {GLOBAL_CONFIG_PATH} ({configStatus})\n" +
                                    $"目标元素路径: {elemPath}\n" +
                                    $"元素名 (烘焙后用于 GlobalAnimConfig.GetElementData 寻址): {Path.GetFileName(elemPath)}",
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

    private static GlobalAnimConfig GetOrCreateGlobalConfig()
    {
        GlobalAnimConfig config = AssetDatabase.LoadAssetAtPath<GlobalAnimConfig>(GLOBAL_CONFIG_PATH);
        if (config != null)
            return config;

        UnityEngine.Object brokenAsset = AssetDatabase.LoadMainAssetAtPath(GLOBAL_CONFIG_PATH);
        if (brokenAsset != null)
        {
            Debug.LogWarning($"[Frame2TextureArray] {GLOBAL_CONFIG_PATH} is not a valid GlobalAnimConfig asset. Recreating it.");
            AssetDatabase.DeleteAsset(GLOBAL_CONFIG_PATH);
        }

        if (!AssetDatabase.IsValidFolder(GLOBAL_CONFIG_FOLDER))
        {
            AssetDatabase.CreateFolder("Assets", "Animation");
        }

        config = CreateInstance<GlobalAnimConfig>();
        AssetDatabase.CreateAsset(config, GLOBAL_CONFIG_PATH);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(GLOBAL_CONFIG_PATH, ImportAssetOptions.ForceUpdate);
        return AssetDatabase.LoadAssetAtPath<GlobalAnimConfig>(GLOBAL_CONFIG_PATH);
    }

    // =========================================================
    //                    核心烘焙主管线
    // =========================================================
    private void ExecuteBakePipeline()
    {
        p_globalConfig = GetOrCreateGlobalConfig();
        if (p_globalConfig == null)
        {
            EditorUtility.DisplayDialog("错误", $"无法创建或读取 {GLOBAL_CONFIG_PATH}", "确定");
            return;
        }

        string rootPath = AssetDatabase.GetAssetPath(targetElementFolder);
        string elementName = Path.GetFileName(rootPath);

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

        // ---- 3. 回溯老配置 (按 elementName 命中, 不再依赖任何业务侧 ID) ----
        ElementAnimBakedData oldElementData = p_globalConfig.GetElementData(elementName);

        ElementAnimBakedData newBakedData = new ElementAnimBakedData
        {
            p_elementName = elementName
        };

        // ---- 4. 创建烘焙输出子目录 (扫描阶段已主动跳过同名目录) ----
        string bakedFolder = rootPath;

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
        float cutU = w * 0.15f; // 切除四个顶角各 15% 的透明浪费区, 削减 Overdraw
        float cutV = h * 0.15f;

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

        // 安全清理残留旧子资产 mesh (挂在 GlobalAnimConfig 上的 subasset)
        if (oldElementData != null && oldElementData.p_bakedMesh != null)
        {
            AssetDatabase.RemoveObjectFromAsset(oldElementData.p_bakedMesh);
            DestroyImmediate(oldElementData.p_bakedMesh, true);
        }

        newBakedData.p_bakedMesh = octagonMesh;
        AssetDatabase.AddObjectToAsset(octagonMesh, p_globalConfig);

        // ---- 8. 按扫描顺序回填 state/var/dir clip 偏移 ----
        // 结构说明 (与 AnimDefines.cs 同步):
        //   StateAnimData.p_isLoop              : 循环标志在状态级别, 所有变体共享.
        //   VariationAnimData.p_frameRate       : 帧率, 变体级别, 所有方向共享.
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
                //  p_frameRate:  继承老值, 默认 12f.
                float inheritedFrameRate = 12f;
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
                    p_frameRate = foundOldVar ? inheritedFrameRate : 12f,
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

        // ---- 9. 替换列表中老条目, 落盘 ----
        if (oldElementData != null)
            p_globalConfig.p_allElementsBakedList.Remove(oldElementData);
        p_globalConfig.p_allElementsBakedList.Add(newBakedData);

        EditorUtility.SetDirty(p_globalConfig);
        AssetDatabase.SaveAssets();

        // ---- 10. 输出日志 ----
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"<b><color=green>BC7 高压三档阵列打包成功！</color></b>");
        sb.AppendLine($"元素名称 (寻址 key): <color=yellow>{elementName}</color>");
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
            $"切片总量: {singleBlockTotalFrames}\np_eventFrame 已自动继承.",
            "OK");
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
        // 定义一个安全的临时中转路径
        string tempPath = "Assets/temp_array_bake.asset";

        // 1. 先将新生成的贴图阵列资产创建到临时的隐藏路径
        AssetDatabase.CreateAsset(asset, tempPath);
        AssetDatabase.SaveAssets(); // 确保强推内存数据落地到磁盘物理文件

        // 2. 判断目标路径是否已经存在老资产 (说明是覆盖式重新生成)
        if (File.Exists(assetPath))
        {
            try
            {
                // 【核心外挂操作】仅仅复制二进制实体数据文件 (.asset), 绝对不碰、不删除原有的 .meta 文件！
                // 这样原有的老 GUID 在磁盘和材质球的引用中都会完好无损地保留下来
                File.Copy(tempPath, assetPath, true);

                // 3. 强行通知 Unity 重新导入目标路径, 让它基于旧的 GUID 去读取刚刚被偷梁换柱的二进制新资产
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[分身替换失败] 覆盖物理文件时发生错误: {e.Message}");
            }

            // 4. 功成身退, 卸磨杀驴. 安全擦除临时资产及配套生成的临时 meta
            AssetDatabase.DeleteAsset(tempPath);
        }
        else
        {
            // 如果目标路径本来就是空的 (比如新加的元素), 直接通过 Unity 正常移动过去
            AssetDatabase.MoveAsset(tempPath, assetPath);
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
