using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace WarField
{
    using WE = WarFieldElements;

    public class WeaponDefines : MonoBehaviour
    {
        //投射物分类，子弹，炮弹
        public enum ProjectileTypes
        {
            MIN = 0,
            BULLET,
            SHELL,
            NOTARGETBULLET,//没有目标的投掷物
            MAX,
        }

        // 投射物基础属性（任何投射物都有）
        public struct ProjectileBaseComponent : IComponentData
        {
            public long p_configId;
            public WE.FactionType p_faction;
            public float p_baseDamage;
            public int p_casterEntityId;
        }

        // 空间与姿态（剥离 GameObject Transform 的核心）
        public struct ProjectilePositionComponent : IComponentData
        {
            public float2 p_position;
            public float p_rotationAngle; // 弧度或角度，由移动System每帧计算写入
        }

        // 渲染插槽桥梁（连接ECS逻辑与外部GameObject/未来的Matrix）
        public struct VfxRenderSlotComponent : IComponentData
        {
            public int p_slotIndex; // 在外部表现层大数组中的下标
        }

        // 贝塞尔移动组件（用于抛物线迫击炮、定点追踪）
        public struct BezierMoveComponent : IComponentData
        {
            public float2 p_startPos;
            public float2 p_endPos;
            public float p_maxHeight;
            public float p_speed;
            public float p_progress; // 0~1 插值进度
        }

        // 纯直线移动组件（用于强力击、激光）
        public struct LinearMoveComponent : IComponentData
        {
            public float2 p_direction; // 归一化的方向向量
            public float p_speed;
            public float p_maxDistance;
            public float p_movedDistance;
        }

        // 存活状态标签（用于标记销毁）
        public struct ProjectileDestroyTag : IComponentData, IEnableableComponent
        {
            // 挂上这个 Tag 的实体，会在一帧结束后被回收系统统一销毁
        }
    }
}

