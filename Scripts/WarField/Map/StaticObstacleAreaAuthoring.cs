using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace WarField
{
    // 强制挂载 PolygonCollider2D。
    // 只借用它的“可视化顶点编辑”功能，不使用它的物理碰撞！
    [RequireComponent(typeof(PolygonCollider2D))]
    public class StaticObstacleAreaAuthoring : MonoBehaviour
    {
#region public parameters

#endregion

#region private parameters

        [Header("Entity Properties")]
        [SerializeField] private WarFieldElements.WarEleType _eleType = WarFieldElements.WarEleType.OBSTACLE;

        [Header("Auto Subdivision")]
        [Tooltip("生成的每个微观碰撞体的半径")]
        [SerializeField] private float _pointRadius = 0.5f;

        [Tooltip("边缘拆分点的间距 (通常等于半径的 1 到 1.5 倍)")]
        [SerializeField] private float _spacing = 1.0f;

        [SerializeField] private bool _debug = true;

        private PolygonCollider2D _polygon;
        private uint _subType = 0;

#endregion

#region private parameters' get set

        public PolygonCollider2D gs_polygon
        {
            get { return _polygon; }
        }

#endregion

#region Unity callbacks

        private void OnValidate()
        {
            if (_polygon == null)
                _polygon = GetComponent<PolygonCollider2D>();

            if (_polygon != null)
                _polygon.isTrigger = true;
        }

        private void OnDrawGizmos()
        {
            if(_debug == false)
                return;

            if (_polygon == null)
                _polygon = GetComponent<PolygonCollider2D>();
            if (_polygon == null || _spacing <= 0.1f)
                return;

            Gizmos.color = Color.red;

            for (int i = 0; i < _polygon.pathCount; i++)
            {
                Vector2[] path = _polygon.GetPath(i);
                for (int j = 0; j < path.Length; j++)
                {
                    Vector2 p1 = transform.TransformPoint(path[j]);
                    Vector2 p2 = transform.TransformPoint(path[(j + 1) % path.Length]);

                    float dist = Vector2.Distance(p1, p2);
                    int steps = Mathf.Max(1, Mathf.RoundToInt(dist / _spacing));

                    for (int s = 0; s < steps; s++)
                    {
                        Gizmos.DrawWireSphere(Vector2.Lerp(p1, p2, (float)s / steps), _pointRadius);
                    }
                }
            }
        }
#endregion

#region public functions

        // 将多边形烘焙成grid中的entity
        public List<GridEntityData> BakeToEntities(byte mapId)
        {
            List<GridEntityData> entities = new List<GridEntityData>();
            if (_polygon == null)
                _polygon = GetComponent<PolygonCollider2D>();

            if (_spacing <= 0.1f)
                _spacing = 0.1f;

            // 仅仅高精度铺设一层边缘外壳（Shell），给物理排斥和DDA拉线提供最基础的边界阻挡
            for (int i = 0; i < _polygon.pathCount; i++)
            {
                Vector2[] path = _polygon.GetPath(i);
                for (int j = 0; j < path.Length; j++)
                {
                    Vector2 p1 = transform.TransformPoint(path[j]);
                    Vector2 p2 = transform.TransformPoint(path[(j + 1) % path.Length]);

                    float dist = Vector2.Distance(p1, p2);
                    int steps = Mathf.Max(1, Mathf.RoundToInt(dist / _spacing));

                    for (int s = 0; s < steps; s++)
                    {
                        Vector2 pos = Vector2.Lerp(p1, p2, (float)s / steps);
                        entities.Add(new GridEntityData {
                            p_position = new float2(pos.x, pos.y),
                            p_radius = _pointRadius,
                            p_eleType = (byte)_eleType,
                            p_subType = 0, // 干净纯粹，无任何特殊种子标记
                            p_mapId = mapId,
                            p_isDead = false
                        });
                    }
                }
            }
            return entities;
        }

#endregion

#region private functions

        private GridEntityData CreateEntity(Vector2 pos, byte mapId)
        {
            return new GridEntityData
            {
                p_position = new float2(pos.x, pos.y),
                p_radius = _pointRadius,
                p_eleType = (byte)_eleType,
                p_subType = _subType,
                p_mapId = mapId,
                p_isDead = false
            };
        }

#endregion
    }
}
