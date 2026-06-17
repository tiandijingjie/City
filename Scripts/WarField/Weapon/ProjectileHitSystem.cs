using Unity.Entities;
using UnityEngine;

namespace WarField
{
    using WD = WeaponDefines;

    // 执行顺序极其重要：在移动计算结束之后，在同步系统销毁实体之前
    [UpdateAfter(typeof(ProjectileMoveSystem))]
    [UpdateBefore(typeof(ProjectileSyncSystem))]
    public partial class ProjectileHitSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // 这里需要调用 MonoBehaviour 的接口，因此必须在主线程执行 (不加 Burst)
            foreach (var (baseComp, entity) in SystemAPI.Query<RefRO<WD.ProjectileBaseComponent>>().WithAll<WD.ProjectileDestroyTag>().WithEntityAccess())
            {
                // 如果在基础组件中设定了需要触发技能回调
                if (baseComp.ValueRO.p_triggerSkill == false)
                    continue;

                int eleType = baseComp.ValueRO.p_casterEleType;
                int gridIndex = baseComp.ValueRO.p_casterGridIndex;

                // 瞬间跨界：通过你设计的完美索引，直接拿回传统的 GameObject 脚本
                WarEleParent caster = SpatialGridManager.Instance.GetGridNode(eleType, gridIndex) as WarEleParent;

                if (caster != null)
                {
                    // 此时已经认定它到达/击中了，我们可以通过实体的 Component 来区分调哪个接口
                    if (SystemAPI.HasComponent<WD.BezierMoveComponent>(entity))
                    {
                        // 这边你可以把目标网格也传进来，或者暂时按之前的逻辑触发
                        // 伪代码：
                        // caster.BullectHit(null, baseComp.ValueRO.p_baseDamage);
                    }
                    else if (SystemAPI.HasComponent<WD.LinearMoveComponent>(entity))
                    {
                        // 伪代码：
                        // caster.ShellHit(null, baseComp.ValueRO.p_baseDamage);
                    }
                }
            }
        }
    }
}
