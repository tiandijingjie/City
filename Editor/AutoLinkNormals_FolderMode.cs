using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

//关联文件下的图片和法线图
public class AutoLinkNormals_FolderMode : EditorWindow
{
    // 只需填写一个根文件夹
    private DefaultAsset rootFolder;

    [MenuItem("Tools/法线关联")]
    public static void ShowWindow()
    {
        GetWindow<AutoLinkNormals_FolderMode>("Normal Linker");
    }

    void OnGUI()
    {
        GUILayout.Label("模式：自动遍历子文件夹，匹配同级 Color 和 Normal 文件夹", EditorStyles.boldLabel);
        GUILayout.Space(10);

        // 绘制输入框让用户拖入根文件夹
        rootFolder = (DefaultAsset)EditorGUILayout.ObjectField("根文件夹 (Root):", rootFolder, typeof(DefaultAsset), false);

        GUILayout.Space(20);

        if (GUILayout.Button("开始遍历并关联 (Link All Matches)"))
        {
            if (rootFolder == null)
            {
                Debug.LogError("请先将根文件夹拖入框中！");
                return;
            }
            LinkFoldersRecursive();
        }
    }

    void LinkFoldersRecursive()
    {
        // 获取根文件夹的绝对系统路径
        string rootAssetPath = AssetDatabase.GetAssetPath(rootFolder);
        string globalRootPath = Path.GetFullPath(rootAssetPath);

        if (!Directory.Exists(globalRootPath))
        {
            Debug.LogError($"根文件夹路径不存在: {globalRootPath}");
            return;
        }

        // 获取该根目录下所有的子文件夹（包括根目录本身）
        List<string> allDirs = new List<string> { globalRootPath };
        allDirs.AddRange(Directory.GetDirectories(globalRootPath, "*", SearchOption.AllDirectories));

        List<string> successParents = new List<string>();
        int totalMatchCount = 0;

        foreach (string dir in allDirs)
        {
            string colorSubDir = Path.Combine(dir, "Color");
            string normalSubDir = Path.Combine(dir, "Normal");

            bool hasColor = Directory.Exists(colorSubDir);
            bool hasNormal = Directory.Exists(normalSubDir);

            // 如果触发了其中任何一个文件夹的存在，就进行配对检查
            if (hasColor || hasNormal)
            {
                string relativeParentPath = GetRelativeAssetPath(dir);

                // 错误处理：文件夹对应不上
                if (hasColor && !hasNormal)
                {
                    Debug.LogError($"<b>【文件夹不对应】</b>在路径 [{relativeParentPath}] 下找到了 Color 文件夹，但缺失对应的 Normal 文件夹！");
                    continue;
                }
                if (!hasColor && hasNormal)
                {
                    Debug.LogError($"<b>【文件夹不对应】</b>在路径 [{relativeParentPath}] 下找到了 Normal 文件夹，但缺失对应的 Color 文件夹！");
                    continue;
                }

                // 两者都存在，开始校验内部的 png 文件并关联
                int matchCount = ProcessPair(dir, colorSubDir, normalSubDir, successParents);
                totalMatchCount += matchCount;
            }
        }

        // 打印所有完全配对成功的父文件夹路径
        if (successParents.Count > 0)
        {
            string successLog = string.Join("\n", successParents);
            Debug.Log($"<b>【关联完成】共成功匹配 {totalMatchCount} 对图片。以下父文件夹完全配对成功：</b>\n{successLog}");
        }
        else
        {
            Debug.LogWarning("未发现完全配对成功的文件夹组合，或过程中存在错误。");
        }

        AssetDatabase.Refresh();
    }

    int ProcessPair(string parentDir, string colorDir, string normalDir, List<string> successParents)
    {
        string relativeParent = GetRelativeAssetPath(parentDir);

        // 获取单层目录下的所有 png 文件
        string[] colorFiles = Directory.GetFiles(colorDir, "*.png", SearchOption.TopDirectoryOnly);
        string[] normalFiles = Directory.GetFiles(normalDir, "*.png", SearchOption.TopDirectoryOnly);

        HashSet<string> colorFileNames = new HashSet<string>(colorFiles.Select(Path.GetFileName));
        HashSet<string> normalFileNames = new HashSet<string>(normalFiles.Select(Path.GetFileName));

        bool hasError = false;

        // 错误处理：Color 里的 png 在 Normal 里找不到
        foreach (string cFile in colorFileNames)
        {
            if (!normalFileNames.Contains(cFile))
            {
                Debug.LogError($"<b>【PNG图片不匹配】</b>目录 [{relativeParent}/Color] 中的图片 <b>{cFile}</b> 在同级 Normal 文件夹中未找到对应法线图！");
                hasError = true;
            }
        }

        // 错误处理：Normal 里的 png 在 Color 里找不到
        foreach (string nFile in normalFileNames)
        {
            if (!colorFileNames.Contains(nFile))
            {
                Debug.LogError($"<b>【PNG图片不匹配】</b>目录 [{relativeParent}/Normal] 中的法线图 <b>{nFile}</b> 在同级 Color 文件夹中未找到对应彩色图！");
                hasError = true;
            }
        }

        int localMatchCount = 0;

        // 开始对齐关联
        foreach (string fileName in colorFileNames)
        {
            if (normalFileNames.Contains(fileName))
            {
                string absoluteColorFilePath = Path.Combine(colorDir, fileName);
                string absoluteNormalFilePath = Path.Combine(normalDir, fileName);

                string colorAssetPath = GetRelativeAssetPath(absoluteColorFilePath);
                string normalAssetPath = GetRelativeAssetPath(absoluteNormalFilePath);

                Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalAssetPath);
                if (normalTex != null)
                {
                    TextureImporter importer = AssetImporter.GetAtPath(colorAssetPath) as TextureImporter;
                    if (importer != null)
                    {
                        bool alreadyLinked = false;
                        if (importer.secondarySpriteTextures != null)
                        {
                            foreach (var st in importer.secondarySpriteTextures)
                            {
                                if (st.name == "_NormalMap" && st.texture == normalTex)
                                    alreadyLinked = true;
                            }
                        }

                        if (!alreadyLinked)
                        {
                            var newSheet = new SecondarySpriteTexture[]
                            {
                                new SecondarySpriteTexture { name = "_NormalMap", texture = normalTex }
                            };
                            importer.secondarySpriteTextures = newSheet;
                            importer.SaveAndReimport();
                        }
                        localMatchCount++;
                    }
                }
            }
        }

        // 只有当该组合没有任何图片不匹配，且内部确实存在有效图片时，才算作“完全配对成功”
        if (!hasError && localMatchCount > 0)
        {
            successParents.Add(relativeParent);
        }

        return localMatchCount;
    }

    // 辅助函数：将绝对系统路径转回 Unity 的 Assets/ 相对路径
    string GetRelativeAssetPath(string absolutePath)
    {
        string projectRoot = Directory.GetCurrentDirectory();
        string relativePath = absolutePath.Replace(projectRoot, "").Replace("\\", "/");
        if (relativePath.StartsWith("/"))
        {
            relativePath = relativePath.Substring(1);
        }
        return relativePath;
    }
}
