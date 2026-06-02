using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class Frame2TextureArray : EditorWindow
{
    private DefaultAsset targetSoldierFolder;

    // Alpha Bleed (颜色外扩) 宽度: HD (512²) 档位下把身体最近邻 RGB 向透明区外扩多少像素.
    // MD/LD 自动按分辨率比例缩减. 设为 0 关闭外扩, 回退到 alpha=0 → RGB 洗黑的旧行为
    // (会带回 cutout 暗边问题, 仅作 debug 对照).
    // 推荐 HD 8 px 起步: 既覆盖 bilinear / 各级 mip 的采样邻域, 又能让 BC7 端点不再两端拟合.
    private int _bleedPixelsHD = 8;

    [MenuItem("Tools/帧动画->Texture2DArray")]
    public static void ShowWindow()
    {
        Frame2TextureArray window = GetWindow<Frame2TextureArray>("Texture2DArray Generator");
        window.minSize = new Vector2(450, 240);
        window.Show();
    }

    private void OnGUI()
    {
        GUILayout.Space(15);
        GUILayout.Label("Texture2DArray Generator(BC7)", EditorStyles.boldLabel);
        GUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        targetSoldierFolder = (DefaultAsset)EditorGUILayout.ObjectField("目录:", targetSoldierFolder, typeof(DefaultAsset), false);

        if (GUILayout.Button("浏览...", GUILayout.Width(60)))
        {
            string defaultPath = targetSoldierFolder != null ? AssetDatabase.GetAssetPath(targetSoldierFolder) : "Assets";
            string absolutePath = EditorUtility.OpenFolderPanel("弹出对话框：选择士兵根目录", defaultPath, "");
            if (!string.IsNullOrEmpty(absolutePath))
            {
                if (absolutePath.StartsWith(Application.dataPath))
                {
                    string relativePath = "Assets" + absolutePath.Substring(Application.dataPath.Length);
                    targetSoldierFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(relativePath);
                }
                else
                {
                    EditorUtility.DisplayDialog("路径错误", "请选择 Assets 目录内部的文件夹！", "知道了");
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(15);
        _bleedPixelsHD = EditorGUILayout.IntSlider(
            new GUIContent(
                "Alpha Bleed 外扩宽度 (HD 像素, 0=关闭)",
                "在 alpha 掩膜外圈用 8-邻接 BFS 把【最近身体像素的 RGB】拷贝出去, alpha 仍为 0.\n" +
                "原理: 让 bilinear 采样 / 各级 mip 下采样在 RGB 上始终读到身体颜色,\n" +
                "      硬切 alpha test 只会切掉 alpha 不够的羽边, 永远不会出现暗化黑环.\n" +
                "      同时 BC7 端点不再需要拟合【彩色 → 黑】高对比, 块噪声大幅降低.\n" +
                "MD/LD 按照一半的一半规则自动向上取整缩减外扩像素数."),
            _bleedPixelsHD, 0, 32);

        GUILayout.Space(10);

        if (targetSoldierFolder == null)
        {
            EditorGUILayout.HelpBox("请先拖入或者点击【浏览...】按钮选中包含动画的根目录。", MessageType.Info);
            GUI.enabled = false;
        }
        else
        {
            string currentPath = AssetDatabase.GetAssetPath(targetSoldierFolder);
            EditorGUILayout.HelpBox($"当前目标路径: {currentPath}", MessageType.None);
            GUI.enabled = true;
        }

        if (GUILayout.Button("生成Texture2DArray (BC7压缩)", GUILayout.Height(40)))
        {
            string soldierPath = AssetDatabase.GetAssetPath(targetSoldierFolder);
            ProcessSingleSoldier(soldierPath, targetSoldierFolder.name, _bleedPixelsHD);
            AssetDatabase.Refresh();
        }

        GUI.enabled = true;
    }

    private static void ProcessSingleSoldier(string soldierPath, string soldierName, int bleedPixelsHD)
    {
        string[] resLayers = { "HD", "MD", "LD" };
        int[] resSizes = { 512, 256, 128 };
        string[] directions = { "Dir_0", "Dir_1", "Dir_2", "Dir_3", "Dir_4", "Dir_5", "Dir_6", "Dir_7" };

        string[] animDirs = Directory.GetDirectories(soldierPath);
        //按照SoldierAnimType里面的枚举顺序进行打包, 这样在读取的时候就能一一对应上
        List<string> animNames = animDirs.Select(p => Path.GetFileName(p))
                                        .Where(n => n != "Packed_Arrays")
                                        .Select(n => {  // 尝试将文件夹名称转换为对应的枚举值
                                            bool success = System.Enum.TryParse<WarField.SoldierDefines.SoldierAnimType>(n, true, out var type);
                                            return new { Name = n, Success = success, Value = type };
                                        })
                                        // 过滤掉不在规范定义内、或者无效的动作文件夹
                                        .Where(x => x.Success && x.Value != WarField.SoldierDefines.SoldierAnimType.MIN && x.Value != WarField.SoldierDefines.SoldierAnimType.MAX)  //严格按照枚举定义的整型大小（0, 1, 2, 3...）进行物理队列重组
                                        .OrderBy(x => (int)x.Value)
                                        .Select(x => x.Name)
                                        .ToList();

        if (animNames.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", $"在 [{soldierName}] 目录下没有找到任何动画文件夹！", "确定");
            return;
        }

        Dictionary<string, (int frameCount, int offset)> animMetaTracker = new Dictionary<string, (int, int)>();
        int singleBlockTotalFrames = 0; //总帧数

        foreach (var anim in animNames)
        {
            string testPath = $"{soldierPath}/{anim}/Dir_0/HD/Color";
            int frameCount = GetFrameCountFromFolder(testPath);
            animMetaTracker.Add(anim, (frameCount, singleBlockTotalFrames));
            singleBlockTotalFrames += frameCount * 8;
        }

        if (singleBlockTotalFrames == 0)
        {
            EditorUtility.DisplayDialog("错误", $"未在 [{soldierName}] 的 HD/Color 目录中检测到任何图片！", "确定");
            return;
        }

        float globalMinX = 1f;
        float globalMaxX = 0f;
        float globalMinY = 1f;
        float globalMaxY = 0f;
        bool hasAnyValidPixel = false;

        string configAssetPath = "Assets/Animation/Soldiers/GlobalSoldierAnimConfig.asset";
        WarField.SoldierAnimConfig configObj = AssetDatabase.LoadAssetAtPath<WarField.SoldierAnimConfig>(configAssetPath);

        if (configObj == null)
        {
            configObj = ScriptableObject.CreateInstance<WarField.SoldierAnimConfig>();
            AssetDatabase.CreateAsset(configObj, configAssetPath);
        }
        var sData = configObj.p_allSoldiersBakedList.Find(x => x.p_sdName == soldierName);
        if (sData == null)
        {
            sData = new WarField.SoldierDefines.SingleSoldierAnimBakedData
            {
                p_sdName = soldierName
            };
            configObj.p_allSoldiersBakedList.Add(sData);
        }
        sData.p_animTypes.Clear();
        sData.p_clipOffsets.Clear();

        // 2. 核心遍历生成
        for (int i = 0; i < resLayers.Length; i++)
        {
            string currentLayer = resLayers[i];
            int targetSize = resSizes[i];

            // TextureFormat.BC7压缩
            int currentBleedPixels = bleedPixelsHD;
            if (currentLayer == "MD")
            {
                currentBleedPixels = Mathf.CeilToInt(bleedPixelsHD / 2f);
            }
            else if (currentLayer == "LD")
            {
                int mdPixels = Mathf.CeilToInt(bleedPixelsHD / 2f);
                currentBleedPixels = Mathf.CeilToInt(mdPixels / 2f);
            }
            Texture2DArray colorTexArray = new Texture2DArray(targetSize, targetSize, singleBlockTotalFrames, TextureFormat.BC7, true);
            Texture2DArray normalTexArray = new Texture2DArray(targetSize, targetSize, singleBlockTotalFrames, TextureFormat.BC7, true);

            int currentSliceIndex = 0;

            foreach (var anim in animNames)
            {
                foreach (var dir in directions)
                {
                    // Color
                    string colorFolderPath = $"{soldierPath}/{anim}/{dir}/{currentLayer}/Color";
                    List<Texture2D> colorFrames = LoadTexturesFromFolder(colorFolderPath);

                    // Normal
                    string normalFolderPath = $"{soldierPath}/{anim}/{dir}/{currentLayer}/Normal";
                    if (!Directory.Exists(normalFolderPath))
						normalFolderPath = $"{soldierPath}/{anim}/{dir}/{currentLayer}/Normals";

                    List<Texture2D> normalFrames = LoadTexturesFromFolder(normalFolderPath);
                    int expectedCount = animMetaTracker[anim].frameCount;

                    for (int f = 0; f < expectedCount; f++)
                    {
                        // 提取 Color 单帧
                        Texture2D srcColorTex = (f < colorFrames.Count) ? colorFrames[f] : (colorFrames.Count > 0 ? colorFrames.Last() : null);
                        // 提取 Normal 单帧
                        Texture2D srcNormalTex = (f < normalFrames.Count) ? normalFrames[f] : (normalFrames.Count > 0 ? normalFrames.Last() : null);

                        //只在扫描高清 HD 档位时进行全自动像素 Alpha 边界盘查
                        if (currentLayer == "HD" && srcColorTex != null)
                        {
                            // 八角 Mesh UV 必须包住"alpha 掩膜 + bleed 外扩环"两部分,
                            // 否则外扩出去的边缘色会被几何体直接裁切掉, alpha 通道下采样产生的
                            // 抗锯齿羽边也无处可去
                            Vector4 frameBound = AnalyzeAlphaBounds(AssetDatabase.GetAssetPath(srcColorTex), bleedPixelsHD);
                            if (frameBound != new Vector4(0f, 1f, 0f, 1f))
                            {
                                if (frameBound.x < globalMinX) globalMinX = frameBound.x;
                                if (frameBound.y > globalMaxX) globalMaxX = frameBound.y;
                                if (frameBound.z < globalMinY) globalMinY = frameBound.z;
                                if (frameBound.w > globalMaxY) globalMaxY = frameBound.w;
                                hasAnyValidPixel = true;
                            }
                        }

                        // 写入 Color 切片页
                        if (srcColorTex != null)
                        {
                            // 传入当前层级计算出来的 bleed 外扩像素数 currentBleedPixels
                            Texture2D resizedTex = ResizeTextureWithMipmaps(srcColorTex, targetSize, targetSize, false, currentBleedPixels);

                            // 压缩为 BC7 格式
                            EditorUtility.CompressTexture(resizedTex, TextureFormat.BC7, TextureCompressionQuality.Best);

                            // 使用 GPU 级的 CopyTexture，把主图及所有 Mipmap 层级强行精准拷贝进对应的切片页中
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

                        // 写入 Normal 硬件切片页 (法线分支走标准蓝清空 + alpha=0 处校准, 不做 bleed)
                        if (srcNormalTex != null)
                        {
                            Texture2D resizedNormal = ResizeTextureWithMipmaps(srcNormalTex, targetSize, targetSize, true, 0);
                            EditorUtility.CompressTexture(resizedNormal, TextureFormat.BC7, TextureCompressionQuality.Best);
                            for (int mip = 0; mip < resizedNormal.mipmapCount; mip++)
                            {
                                Graphics.CopyTexture(resizedNormal, 0, mip, normalTexArray, currentSliceIndex, mip);
                            }
                            DestroyImmediate(resizedNormal);
                        }
                        else
                        {
                            // 弹出报错对话框，退出打包逻辑
                            EditorUtility.DisplayDialog("双轨打包中断！",
                                $"【缺失法线资产资产】\n\n" +
                                $"兵种名称: {soldierName}\n" +
                                $"动作名称: {anim}\n" +
                                $"方向索引: {dir}\n" +
                                $"清晰度档位: {currentLayer}\n" +
                                $"当前帧数索引: 第 {f} 帧\n\n" +
                                $"预设法线图片路径: {normalFolderPath}\n\n" +
                                $"请让美术补齐该动作方向对应的 Normal 法线序列帧后再行重新生成阵列！", "知道了");
                            return; // 强行中断退出
                        }
                        currentSliceIndex++;
                    }
                }
            }

            // 因为是用 Graphics.CopyTexture 动态打入 GPU 的，此处直接应用，无需重新计算 Mips
            colorTexArray.Apply(false, false);
            normalTexArray.Apply(false, false);

            string colorAssetPath = $"{soldierPath}/{soldierName}_{currentLayer}_Array.asset";
            string normalAssetPath = $"{soldierPath}/{soldierName}_{currentLayer}_Normal_Array.asset";

            //GUID
            SaveAssetPreservingMeta(colorTexArray, colorAssetPath);
            SaveAssetPreservingMeta(normalTexArray, normalAssetPath);

        	//将每个动画的寻址细节保存到GlobalSoldierAnimConfig.asset这个文件中, 后续运行时能够解析这个文件获取寻址细节
            Texture2DArray persistentColor = AssetDatabase.LoadAssetAtPath<Texture2DArray>(colorAssetPath);
            Texture2DArray persistentNormal = AssetDatabase.LoadAssetAtPath<Texture2DArray>(normalAssetPath);

            if (currentLayer == "HD")
            {
                sData.p_hdColorArray = persistentColor;
                sData.p_hdNormalArray = persistentNormal;
            }
            else if (currentLayer == "MD")
            {
                sData.p_mdColorArray = persistentColor;
                sData.p_mdNormalArray = persistentNormal;
            }
            else if (currentLayer == "LD")
            {
                sData.p_ldColorArray = persistentColor;
                sData.p_ldNormalArray = persistentNormal;
            }

        }

        // 根据全局最大包围盒创建固定八角形 Mesh
        if (!hasAnyValidPixel)
        {
            globalMinX = 0f; globalMaxX = 1f; globalMinY = 0f; globalMaxY = 1f;
        }

        float w = globalMaxX - globalMinX;
        float h = globalMaxY - globalMinY;
        float cutU = w * 0.15f; // 切除四个顶角各 15% 的透明浪费区，削减 Overdraw
        float cutV = h * 0.15f;

        Vector2[] uvs = new Vector2[8];
        uvs[0] = new Vector2(globalMinX + cutU, globalMaxY);         // 顶左
        uvs[1] = new Vector2(globalMaxX - cutU, globalMaxY);         // 顶右
        uvs[2] = new Vector2(globalMaxX, globalMaxY - cutV);         // 右顶
        uvs[3] = new Vector2(globalMaxX, globalMinY + cutV);         // 右底
        uvs[4] = new Vector2(globalMaxX - cutU, globalMinY);         // 底右
        uvs[5] = new Vector2(globalMinX + cutU, globalMinY);         // 底左
        uvs[6] = new Vector2(globalMinX, globalMinY + cutV);         // 左底
        uvs[7] = new Vector2(globalMinX, globalMaxY - cutV);         // 左顶

        Vector3[] vertices = new Vector3[8];
        // 八角 mesh 必须与 Unity 默认 Quad (vertex [-0.5,0.5]) 同源映射:
        //   vertex = UV - 0.5
        // 这样烘焙后的八角等价于"裁掉透明边的 Quad". Prefab 编辑期对 Quad 调好的
        // SoldierAnim.localPosition.y (用于让脚贴到根节点) 在运行时换成八角 mesh
        // 之后仍然零差异地继续生效, 不需要任何运行时校正.
        // 切勿改成 (UV.y - globalMinY): 那会把"全帧最低 alpha 像素 (常是影子)"当成 0 点,
        // 让脚在 mesh 中段而不是底端, 导致运行时角色明显比 prefab 编辑时高出一截.
        for (int vIdx = 0; vIdx < 8; vIdx++)
        {
            vertices[vIdx] = new Vector3(
                uvs[vIdx].x - 0.5f,
                uvs[vIdx].y - 0.5f,
                0f
            );
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
        octagonMesh.name = $"{soldierName}_OctagonMesh";
        octagonMesh.vertices = vertices;
        octagonMesh.uv = uvs;
        octagonMesh.triangles = triangles;
        octagonMesh.normals = normals;
        octagonMesh.RecalculateTangents();

        // 安全清理残留旧子资产
        if (sData.p_bakedMesh != null)
        {
            AssetDatabase.RemoveObjectFromAsset(sData.p_bakedMesh);
            DestroyImmediate(sData.p_bakedMesh, true);
        }
        sData.p_bakedMesh = octagonMesh;
        AssetDatabase.AddObjectToAsset(octagonMesh, configObj);

        foreach (var kvp in animMetaTracker)
        {
            if (System.Enum.TryParse<WarField.SoldierDefines.SoldierAnimType>(kvp.Key, true, out var aType))
            {
                sData.p_animTypes.Add(aType);
                sData.p_clipOffsets.Add(new WarField.SoldierDefines.FrameAnimClipOffsets
                {
                    p_animStartOffset = kvp.Value.offset,
                    p_animFrameCount = kvp.Value.frameCount,
                    p_frameRate = 12f,
                    p_isLoop = (aType == WarField.SoldierDefines.SoldierAnimType.MOVE || aType == WarField.SoldierDefines.SoldierAnimType.IDLE || aType == WarField
                    .SoldierDefines.SoldierAnimType.STUN),
                    p_eventFrame = -1
                });
            }
        }

        EditorUtility.SetDirty(configObj);
        AssetDatabase.SaveAssets();

        // 3. 输出日志
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"<b><color=green>BC7高压阵列打包成功！</color></b>");
        sb.AppendLine($"士兵名称: <color=yellow>{soldierName}</color>");
        sb.AppendLine($"<b>单Block总帧数 (SingleBlockTotalFrames): <color=cyan>{singleBlockTotalFrames}</color> 帧</b>");
        sb.AppendLine("ECS/BlobAsset 寻址配置表:");
        foreach (var kvp in animMetaTracker)
        {
            sb.AppendLine($"  -> 动画: [ {kvp.Key} ] | 单向帧数: {kvp.Value.frameCount} | 块内起始 Offset: <color=orange>{kvp.Value.offset}</color>");
        }
        Debug.Log(sb.ToString());

        EditorUtility.DisplayDialog("", $"[{soldierName}] 的 BC7 高压阵列资产已成功输出！", "OK");
    }

    // 强制保护原有 .meta 文件
	private static void SaveAssetPreservingMeta(Object asset, string assetPath)
	{
	    // 定义一个安全的临时中转路径
	    string tempPath = "Assets/temp_array_bake.asset";

	    // 1. 先将新生成的贴图阵列资产创建到临时的隐藏路径
	    AssetDatabase.CreateAsset(asset, tempPath);
	    AssetDatabase.SaveAssets(); // 确保强推内存数据落地到磁盘物理文件

	    // 2. 判断目标路径是否已经存在老资产（说明是覆盖式重新生成）
	    if (File.Exists(assetPath))
	    {
	        try
	        {
	            // 【核心外挂操作】仅仅复制二进制实体数据文件 (.asset)，绝对不碰、不删除原有的 .meta 文件！
	            // 这样原有的老 GUID 在磁盘和材质球的引用中都会完好无损地保留下来
	            File.Copy(tempPath, assetPath, true);

	            // 3. 强行通知 Unity 重新导入目标路径，让它基于旧的 GUID 去读取刚刚被偷梁换柱的二进制新资产
	            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
	        }
	        catch (System.Exception e)
	        {
	            Debug.LogError($"[分身替换失败] 覆盖物理文件时发生错误: {e.Message}");
	        }

	        // 4. 功成身退，卸磨杀驴。安全擦除临时资产及配套生成的临时 meta
	        AssetDatabase.DeleteAsset(tempPath);
	    }
	    else
	    {
	        // 如果目标路径本来就是空的（比如新加的兵种），直接通过 Unity 正常移动过去
	        AssetDatabase.MoveAsset(tempPath, assetPath);
	    }
	}

    //高效率图片 A 通道边缘检测
    private static Vector4 AnalyzeAlphaBounds(string assetPath, int bleedPixelsHD)
    {
        byte[] fileData = File.ReadAllBytes(assetPath);
        Texture2D rawTex = new Texture2D(2, 2);
        rawTex.LoadImage(fileData); // 绕过 Unity 的导入只读限制，直接解开二进制像素

        int w = rawTex.width;
        int h = rawTex.height;
        Color32[] pixels = rawTex.GetPixels32();

        int minX = w, maxX = 0, minY = h, maxY = 0;
        bool hasPixel = false;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // 如果当前像素的 Alpha 大于 12（约 0.05 阈值），判定为有效身体像素
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

    private static int GetFrameCountFromFolder(string path)
    {
        if (!Directory.Exists(path))
			return 0;
        return Directory.GetFiles(path, "*.png").Length;
    }

    private static List<Texture2D> LoadTexturesFromFolder(string path)
    {
        List<Texture2D> list = new List<Texture2D>();
        if (!Directory.Exists(path))
			return list;

        string[] files = Directory.GetFiles(path, "*.png")
                                  .OrderBy(f => f, System.StringComparer.OrdinalIgnoreCase)
                                  .ToArray();

        foreach (var file in files)
        {
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(file);
            if (tex != null) list.Add(tex);
        }
        return list;
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
            // 将目标贴图实际宽高 (width) 传入烘焙方法，作为映射缩放基准
            tempBleedSource = LoadAndBleedSourcePNG(source, bleedPixels, width);
            if (tempBleedSource != null)
                effectiveSource = tempBleedSource;
        }

        // 2. 获取临时渲染纹理并激活配置
        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        RenderTexture.active = rt;

        // 【智能防线 1：根据类型清空 RenderTexture 底色】
        // 法线贴图必须用标准法线蓝 (0.5, 0.5, 1.0, 0.0) 填充底色，防止 GPU 硬件插值发黑.
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

        // 5. 基于彻底清洗干净、两轨各自完美的底层 RGB 色彩，强行重新建立未压缩状态下的所有 Mipmap 层级链
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
        try { fileData = File.ReadAllBytes(assetPath); }
        catch { return null; }

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
            if (x > 0)                    { int n = idx - 1;     if (nd < distMap[n]) { distMap[n] = nd; nearestSeed[n] = seed; bfs.Enqueue(n); } }
            if (x < W - 1)                { int n = idx + 1;     if (nd < distMap[n]) { distMap[n] = nd; nearestSeed[n] = seed; bfs.Enqueue(n); } }
            if (y > 0)                    { int n = idx - W;     if (nd < distMap[n]) { distMap[n] = nd; nearestSeed[n] = seed; bfs.Enqueue(n); } }
            if (y < H - 1)                { int n = idx + W;     if (nd < distMap[n]) { distMap[n] = nd; nearestSeed[n] = seed; bfs.Enqueue(n); } }
            if (x > 0 && y > 0)           { int n = idx - W - 1; if (nd < distMap[n]) { distMap[n] = nd; nearestSeed[n] = seed; bfs.Enqueue(n); } }
            if (x < W - 1 && y > 0)       { int n = idx - W + 1; if (nd < distMap[n]) { distMap[n] = nd; nearestSeed[n] = seed; bfs.Enqueue(n); } }
            if (x > 0 && y < H - 1)       { int n = idx + W - 1; if (nd < distMap[n]) { distMap[n] = nd; nearestSeed[n] = seed; bfs.Enqueue(n); } }
            if (x < W - 1 && y < H - 1)   { int n = idx + W + 1; if (nd < distMap[n]) { distMap[n] = nd; nearestSeed[n] = seed; bfs.Enqueue(n); } }
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
