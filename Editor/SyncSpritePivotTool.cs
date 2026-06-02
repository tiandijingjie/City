using UnityEngine;
using UnityEditor;
using System.IO;

public class SyncSpritePivotTool : EditorWindow
{
    private Texture2D templateTexture;
    private DefaultAsset targetFolder;
    private bool includeSubfolders = true;

    [MenuItem("Tools/Sprite pivot同步工具")]
    public static void ShowWindow()
    {
        GetWindow<SyncSpritePivotTool>("Pivot Syncer");
    }

    void OnGUI()
    {
        GUILayout.Label("模式：根据模板图片的 Pivot，批量修改目标文件夹下的图片", EditorStyles.boldLabel);
        GUILayout.Space(10);

        // 选择标准模板图片
        templateTexture = (Texture2D)EditorGUILayout.ObjectField("模板图片 (Template):", templateTexture, typeof(Texture2D), false);

        // 选择要修改的目标文件夹
        targetFolder = (DefaultAsset)EditorGUILayout.ObjectField("目标文件夹 (Target Folder):", targetFolder, typeof(DefaultAsset), false);

        // 是否包含子文件夹
        includeSubfolders = EditorGUILayout.Toggle("包含子文件夹", includeSubfolders);

        GUILayout.Space(20);

        if (GUILayout.Button("开始同步 Pivot (Sync Pivots)"))
        {
            if (templateTexture == null || targetFolder == null)
            {
                Debug.LogError("请先拖入模板图片和目标文件夹！");
                return;
            }
            SyncPivots();
        }
    }

    void SyncPivots()
    {
        // 1. 获取模板图片的 Importer
        string templatePath = AssetDatabase.GetAssetPath(templateTexture);
        TextureImporter templateImporter = AssetImporter.GetAtPath(templatePath) as TextureImporter;

        if (templateImporter == null || templateImporter.textureType != TextureImporterType.Sprite)
        {
            Debug.LogError("模板图片必须是 Sprite (2D and UI) 类型！");
            return;
        }

        // 使用 TextureImporterSettings 正确读取模板的设置
        TextureImporterSettings templateSettings = new TextureImporterSettings();
        templateImporter.ReadTextureSettings(templateSettings);

        // 2. 遍历目标文件夹
        string folderPath = AssetDatabase.GetAssetPath(targetFolder);
        string globalPath = Path.GetFullPath(folderPath);

        SearchOption option = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        string[] allFiles = Directory.GetFiles(globalPath, "*.png", option);

        int stageCount = 0;

        foreach (string file in allFiles)
        {
            // 转为 Unity 相对路径
            string relativePath = GetRelativeAssetPath(file);

            // 跳过模板图片自身
            if (relativePath == templatePath) continue;

            TextureImporter targetImporter = AssetImporter.GetAtPath(relativePath) as TextureImporter;

            if (targetImporter != null)
            {
                // 确保目标图也是 Sprite 类型
                if (targetImporter.textureType != TextureImporterType.Sprite)
                {
                    targetImporter.textureType = TextureImporterType.Sprite;
                }

                // 读取目标图当前的 Settings
                TextureImporterSettings targetSettings = new TextureImporterSettings();
                targetImporter.ReadTextureSettings(targetSettings);

                // 将模板的对齐数据覆盖过去
                targetSettings.spriteMode = (int)SpriteImportMode.Single;
                targetSettings.spriteAlignment = templateSettings.spriteAlignment;

                // 如果模板是 Custom 模式，拷贝具体的 pivot 比例坐标(Vector2)
                if (templateSettings.spriteAlignment == (int)SpriteAlignment.Custom)
                {
                    targetSettings.spritePivot = templateSettings.spritePivot;
                }

                // 把修改后的 settings 应用回物体，并重导
                targetImporter.SetTextureSettings(targetSettings);
                targetImporter.SaveAndReimport();
                stageCount++;
            }
        }

        Debug.Log($"<b>【Pivot 同步完成】</b>已成功将 Pivot 同步到 {stageCount} 张图片上！" +
                  $"\n同步参数 -> Alignment: {(SpriteAlignment)templateSettings.spriteAlignment}, Pivot: {templateSettings.spritePivot}");

        AssetDatabase.Refresh();
    }

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
