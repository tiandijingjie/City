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
            public long p_weaponId; //waepon的具体类型, 每种远程投掷物一个ID
            public WE.FactionType p_faction;
            public float p_baseDamage;
            public int p_mapId;
            public int p_casterEleType;
            public int p_casterGridIndex; //发射weapon的_gridIndex, 就是WarEleParent._gridIndex
            public bool p_triggerSkill; //whether call BullectHit() or ShellHit() to call SkillDoAttackPost()/BuffDoAttackPost()/TalentDoAttackPost()
        }

        //目标记录组件 (用于传给回调)
        public struct ProjectileTargetComponent : IComponentData
        {
            public int p_targetEleType;
            public int p_targetGridIndex;
        }

        // 单体攻击组件 (Bullet专属)
        public struct SingleTargetComponent : IComponentData
        {
            // 单体特定参数可以放这
        }

        // 范围攻击组件 (Shell专属)
        public struct AreaDamageComponent : IComponentData
        {
            public float p_damageRange;
            public float p_otherDamage; // 边缘衰减伤害
            public bool p_canAttackBuilding; // 是否允许伤害建筑，默认true
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

        // 穿透碰撞组件（NOTARGETBULLET 专属挂载）
        public struct LinearPenetrationComponent : IComponentData
        {
            public float p_colliderRadius;
        }

        // ECS的内存连续动态数组：用于记录这颗子弹已经伤害过哪些敌人
        // [InternalBufferCapacity(16)] 表示默认给它分配 16 个元素的连续内存，超过才会分配到堆内存
        [InternalBufferCapacity(16)]
        public struct HitRecordElement : IBufferElementData  //用来给notarget 的记录的,  因为可能在多帧里面search到相同的目标
        {
            public int p_targetEleType;
            public int p_targetGridIndex;
        }
    }
}

