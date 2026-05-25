using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace WarField
{
    using WE = WarFieldElements;

    //设置body的大小和位置
    //用于生成单个物体的entity数据
    public class BodyAuthoring : MonoBehaviour
    {
#region public parameters

#endregion

#region private parameters

        [Header("Entity Properties")]
        [SerializeField] private WE.WarEleType _eleType = WE.WarEleType.OBSTACLE;

        [Header("Manual Collision Settings")]
        [Tooltip("碰撞体半径")]
        [SerializeField] private float _radius = 0.5f;

        [SerializeField] private bool _debug = true;

        private Transform _transform;
        private uint _subType = 0;
#endregion

#region private parameters' get set

        public WE.WarEleType gs_eleType
        {
            get { return _eleType; }
        }

        public virtual bool gs_writeToFlowField
        {
            get { return false; }
        }

        public float gs_radius
        {
            get { return _radius; }
        }

#endregion

#region Unity callbacks

        private void Awake()
        {
            _transform = transform;
        }

        private void OnDrawGizmos()
        {
            if(_debug == false)
                return;

            Vector3 realCenter = transform.position ;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(realCenter, _radius);

            // 画一个黄色的十字准星，在拖拽 offset 时能清晰看到圆心在哪
            Gizmos.color = Color.yellow;
            float crossSize = 0.1f;
            Gizmos.DrawLine(realCenter - Vector3.up * crossSize, realCenter + Vector3.up * crossSize);
            Gizmos.DrawLine(realCenter - Vector3.right * crossSize, realCenter + Vector3.right * crossSize);
        }

        private void OnDrawGizmosSelected()
        {
            if(_debug == true)
                return;

            Vector3 realCenter = transform.position ;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(realCenter, _radius);

            // 画一个黄色的十字准星，在拖拽 offset 时能清晰看到圆心在哪
            Gizmos.color = Color.yellow;
            float crossSize = 0.1f;
            Gizmos.DrawLine(realCenter - Vector3.up * crossSize, realCenter + Vector3.up * crossSize);
            Gizmos.DrawLine(realCenter - Vector3.right * crossSize, realCenter + Vector3.right * crossSize);
        }

#endregion

#region public functions

        public virtual void InitType(WE.WarEleType eleType, uint subType)
        {
            _eleType = eleType;
            _subType = subType;
        }

        //生成对应的entity数据
        public GridEntityData BakeToEntitie(byte mapId, byte specData)
        {
            if (_eleType == WE.WarEleType.BUILDING && _subType == 0)
            {
                GameLogger.LogError($"Not set the subtype for building ");
                return default;
            }
            else if (_eleType == WE.WarEleType.SOLDIER && _subType == 0)
            {
                GameLogger.LogError($"Not set the subtype for soldier ");
                return default;
            }

            return new GridEntityData
            {
                p_position = (Vector2)_transform.position,
                p_radius = _radius,
                p_eleType = (byte)_eleType,
                p_subType = _subType,
                p_spec = specData,
                p_mapId = mapId,
                p_isDead = false
            };
        }
#endregion

#region private functions

#endregion
    }
}
