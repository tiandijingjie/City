using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WarField;

// 自定义 ShaderGUI: 让 AnimTex2dArrayCutout 在 Prefab/材质编辑器中的预览效果
// 与运行时 (SoldierCtrl 通过 MaterialPropertyBlock 注入 _FrameUVBounds) 严格一致.
//
// 核心做法: 当用户调整 _FinalSliceIndex 时, 自动按 Material.name 匹配的兵种条目
// 从烘焙资源 (GlobalSoldierAnimConfig.AllSlicesUVBounds[sliceIndex]) 读出该帧的
// 紧贴 alpha 边界, 同步写入两个属性:
//   1) _EditorUVBounds  : 非 Instancing 分支的顶点位移直接读取
//   2) _FrameUVBounds   : Instancing 分支用作 MPB 缺失时的默认兜底值
//                         (运行时 SoldierCtrl 通过 MPB 覆盖该值, 不受此处影响)
// 杜绝手填造成的"几何包含透明空白"导致角色脚底悬空的问题.
// public class AnimTex2dArrayCutoutShaderGUI : ShaderGUI
// {
//     private const string ConfigAssetPath = "Assets/Animation/Soldiers/GlobalSoldierAnimConfig.asset";
//     private const string EditorUVBoundsProp = "_EditorUVBounds";
//     private const string FrameUVBoundsProp = "_FrameUVBounds";
//     private const string FinalSliceIndexProp = "_FinalSliceIndex";
//
//     private static SoldierAnimConfig _cachedConfig;
//
//     private struct SyncResult
//     {
//         public bool success;
//         public string message;
//         public MessageType messageType;
//         public string soldierName;
//         public int sliceIndex;
//         public int sliceCount;
//         public Vector4 tightBounds;
//     }
//
//     public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
//     {
//         bool multiTarget = materialEditor.targets != null && materialEditor.targets.Length > 1;
//
//         // 1) 把 _EditorUVBounds 与 _FrameUVBounds 从默认 GUI 中剔除 (改用下面的只读面板展示),
//         //    避免用户手填出错; 其余属性保持默认 Inspector 行为
//         var filtered = new List<MaterialProperty>(properties.Length);
//         foreach (var p in properties)
//         {
//             if (p.name == EditorUVBoundsProp || p.name == FrameUVBoundsProp) continue;
//             filtered.Add(p);
//         }
//         base.OnGUI(materialEditor, filtered.ToArray());
//
//         // 2) 在用户的 _FinalSliceIndex 改动落到材质之后再 Sync 一遍紧贴矩形,
//         //    保证 scene/prefab 视图本帧渲染时材质状态自洽 (slice 与 bounds 一致)
//         SyncResult lastSync = default;
//         foreach (var t in materialEditor.targets)
//         {
//             if (t is Material m) lastSync = AutoSyncEditorUVBounds(m);
//         }
//
//         // 3) 同步状态面板 + 强制重新同步按钮
//         EditorGUILayout.Space();
//         using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
//         {
//             EditorGUILayout.LabelField("UV Bounds (烘焙紧贴自动同步)", EditorStyles.boldLabel);
//
//             if (multiTarget)
//             {
//                 EditorGUILayout.HelpBox("多个材质同时选中, 状态面板只展示最后一个; 同步逻辑已按各自材质名分别执行.", MessageType.None);
//             }
//             EditorGUILayout.HelpBox(lastSync.message ?? "(未知状态)", lastSync.messageType);
//
//             Material first = materialEditor.target as Material;
//             if (first != null && first.HasProperty(EditorUVBoundsProp) && first.HasProperty(FrameUVBoundsProp))
//             {
//                 using (new EditorGUI.DisabledScope(true))
//                 {
//                     EditorGUILayout.Vector4Field("_EditorUVBounds  (非 Instancing 分支)", first.GetVector(EditorUVBoundsProp));
//                     EditorGUILayout.Vector4Field("_FrameUVBounds   (Instancing 兜底)", first.GetVector(FrameUVBoundsProp));
//                 }
//             }
//
//             using (new EditorGUI.DisabledScope(!lastSync.success))
//             {
//                 if (GUILayout.Button("强制重新同步 Editor UV Bounds"))
//                 {
//                     foreach (var t in materialEditor.targets)
//                     {
//                         if (t is Material m) AutoSyncEditorUVBounds(m, forceWrite: true);
//                     }
//                 }
//             }
//
//             EditorGUILayout.HelpBox(
//                 "运行时 SoldierCtrl 通过 MaterialPropertyBlock 按 InstanceID 实时注入 _FrameUVBounds,\n" +
//                 "与此处的 Editor UV Bounds 互不影响; 此面板仅用于让 Prefab 编辑器预览与运行效果对齐.",
//                 MessageType.None);
//
//             if (GUILayout.Button("刷新烘焙配置缓存 (重新读 GlobalSoldierAnimConfig)"))
//             {
//                 _cachedConfig = null;
//             }
//         }
//     }
//
//     private static SyncResult AutoSyncEditorUVBounds(Material mat, bool forceWrite = false)
//     {
//         SyncResult r = new SyncResult { soldierName = mat != null ? mat.name : "" };
//
//         if (mat == null)
//         {
//             r.message = "材质引用为空";
//             r.messageType = MessageType.Error;
//             return r;
//         }
//
//         if (!mat.HasProperty(EditorUVBoundsProp) || !mat.HasProperty(FrameUVBoundsProp) || !mat.HasProperty(FinalSliceIndexProp))
//         {
//             r.message = "材质缺少 _EditorUVBounds / _FrameUVBounds / _FinalSliceIndex 属性 (Shader 不匹配)";
//             r.messageType = MessageType.Error;
//             return r;
//         }
//
//         SoldierAnimConfig config = GetConfig();
//         if (config == null)
//         {
//             r.message = $"未找到烘焙配置: {ConfigAssetPath}\n请先执行 [Tools/帧动画->Texture2DArray] 生成";
//             r.messageType = MessageType.Warning;
//             return r;
//         }
//
//         var sData = config.p_allSoldiersBakedList.Find(x => x.p_sdName == r.soldierName);
//         if (sData == null || sData.AllSlicesUVBounds == null || sData.AllSlicesUVBounds.Count == 0)
//         {
//             r.message = $"烘焙配置中找不到兵种 [{r.soldierName}] 的紧贴 UV 数据\n(材质名必须与 Soldier 烘焙名完全一致)";
//             r.messageType = MessageType.Warning;
//             return r;
//         }
//
//         r.sliceCount = sData.AllSlicesUVBounds.Count;
//         r.sliceIndex = Mathf.Clamp(Mathf.RoundToInt(mat.GetFloat(FinalSliceIndexProp)), 0, r.sliceCount - 1);
//         r.tightBounds = sData.AllSlicesUVBounds[r.sliceIndex];
//
//         // 同步两份: _EditorUVBounds 给非 Instancing 分支; _FrameUVBounds
//         // 作为 Instancing 分支在 MPB 缺失 (Prefab 编辑器预览) 时的默认兜底值.
//         // 运行时 SoldierCtrl 用 MPB 注入 _FrameUVBounds 覆盖, 不受此处影响.
//         const float epsilon = 1e-5f;
//         Vector4 curEditor = mat.GetVector(EditorUVBoundsProp);
//         Vector4 curFrame = mat.GetVector(FrameUVBoundsProp);
//         bool diffEditor = !Approx(curEditor, r.tightBounds, epsilon);
//         bool diffFrame = !Approx(curFrame, r.tightBounds, epsilon);
//
//         if (forceWrite || diffEditor || diffFrame)
//         {
//             Undo.RecordObject(mat, "Sync UV Bounds From Baked Data");
//             if (forceWrite || diffEditor) mat.SetVector(EditorUVBoundsProp, r.tightBounds);
//             if (forceWrite || diffFrame) mat.SetVector(FrameUVBoundsProp, r.tightBounds);
//             EditorUtility.SetDirty(mat);
//         }
//
//         r.success = true;
//         r.message =
//             $"兵种 [{r.soldierName}]  切片 [{r.sliceIndex} / {r.sliceCount - 1}]\n" +
//             $"烘焙紧贴 (MinU, MaxU, MinV, MaxV) = ({r.tightBounds.x:F4}, {r.tightBounds.y:F4}, {r.tightBounds.z:F4}, {r.tightBounds.w:F4})";
//         r.messageType = MessageType.Info;
//         return r;
//     }
//
//     private static SoldierAnimConfig GetConfig()
//     {
//         if (_cachedConfig == null)
//         {
//             _cachedConfig = AssetDatabase.LoadAssetAtPath<SoldierAnimConfig>(ConfigAssetPath);
//         }
//         return _cachedConfig;
//     }
//
//     private static bool Approx(Vector4 a, Vector4 b, float eps)
//     {
//         return Mathf.Abs(a.x - b.x) <= eps
//             && Mathf.Abs(a.y - b.y) <= eps
//             && Mathf.Abs(a.z - b.z) <= eps
//             && Mathf.Abs(a.w - b.w) <= eps;
//     }
// }
