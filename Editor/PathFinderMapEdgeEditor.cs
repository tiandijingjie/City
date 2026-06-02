using UnityEngine;
using UnityEditor;

namespace WarField
{
    //用于在编辑器中编辑流场地图
    [CustomEditor(typeof(PathFinderMap))]
    public class PathFinderMapEdgeEditor : Editor
    {
        private void OnSceneGUI()
        {
            PathFinderMap map = (PathFinderMap)target;

            // 动态获取地图尺寸 (兼容编辑模式和运行模式)
            float originX = map.transform.position.x;
            float originY = map.transform.position.y;
            float mapHeight = 50f; // 默认高度兜底

            if (map.gs_bounds.size.y > 0)
            {
                // 运行模式：直接使用烘焙好的 Bounds
                originX = map.gs_bounds.min.x;
                originY = map.gs_bounds.min.y;
                mapHeight = map.gs_bounds.size.y;
            }
            else
            {
                // 编辑模式：遍历 Road 下所有的地砖，计算它们的总包围盒 (Total Bounds)
                Transform road = map.transform.Find("Road");
                if (road != null)
                {
                    // 获取 Road 及其所有子物体身上的 SpriteRenderer
                    SpriteRenderer[] renderers = road.GetComponentsInChildren<SpriteRenderer>();

                    if (renderers.Length > 0) //将所有的bounds合并计算
                    {
                        Bounds totalBounds = renderers[0].bounds;
                        for (int i = 1; i < renderers.Length; i++)
                        {
                            totalBounds.Encapsulate(renderers[i].bounds);
                        }
                        originX = totalBounds.min.x;
                        originY = totalBounds.min.y;
                        mapHeight = totalBounds.size.y;
                    }
                }
            }

            // 将 Z 轴强行往前推（设为 -5），防止背景 Sprite 拦截鼠标点击！
            float zOffset = -5f;

            // 绘制 己方边界 (蓝色)
            Handles.color = new Color(0, 0.5f, 1f, 0.9f);
            float friendlyWorldX = originX + map.gs_friendlyDesX;

            Vector3 friendlyBottom = new Vector3(friendlyWorldX, originY, zOffset);
            Vector3 friendlyTop = new Vector3(friendlyWorldX, originY + mapHeight, zOffset);

            // 画一条贯穿地图的粗线
            Handles.DrawLine(friendlyBottom, friendlyTop, 3f);

            // 把可拖拽的把手放在线的【正中间】
            Vector3 friendlyHandleCenter = new Vector3(friendlyWorldX, originY + mapHeight / 2f, zOffset);

            EditorGUI.BeginChangeCheck();
            float handleSize = HandleUtility.GetHandleSize(friendlyHandleCenter) * 0.3f; // 调整为精美的小方块

            // Slider 限制只能沿 X 轴滑动
            Vector3 newFriendlyPos = Handles.Slider(friendlyHandleCenter, Vector3.right, handleSize, Handles.CubeHandleCap, 0.1f);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(map, "Move Friendly Boundary");
                // 转回相对坐标
                map.gs_friendlyDesX = newFriendlyPos.x - originX;
            }


            // 绘制 敌方边界 (红色)
            Handles.color = new Color(1f, 0f, 0f, 0.9f);
            float enemyWorldX = originX + map.gs_enemyDesX;

            Vector3 enemyBottom = new Vector3(enemyWorldX, originY, zOffset);
            Vector3 enemyTop = new Vector3(enemyWorldX, originY + mapHeight, zOffset);

            Handles.DrawLine(enemyBottom, enemyTop, 3f);

            Vector3 enemyHandleCenter = new Vector3(enemyWorldX, originY + mapHeight / 2f, zOffset);

            EditorGUI.BeginChangeCheck();
            Vector3 newEnemyPos = Handles.Slider(enemyHandleCenter, Vector3.right, handleSize, Handles.CubeHandleCap, 0.1f);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(map, "Move Enemy Boundary");
                map.gs_enemyDesX = newEnemyPos.x - originX;
            }
        }
    }
}
