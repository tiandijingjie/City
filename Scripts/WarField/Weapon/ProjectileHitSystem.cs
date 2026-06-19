using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace WarField
{
    using WD = WeaponDefines;
    using WE = WarFieldElements;

    [UpdateAfter(typeof(ProjectileMoveSystem))]
    [UpdateBefore(typeof(ProjectileSyncSystem))]
    public partial class ProjectileHitSystem : SystemBase //针对bullet和shell的伤害类型
    {
        protected override void OnUpdate()
        {
            if (SpatialGridManager.Instance == null)
                return;

            // ==========================================
            // 1. 处理单体子弹 (Bullet) 的伤害与回调
            // ==========================================
            foreach (var (baseComp, targetComp, entity) in SystemAPI.Query<RefRO<WD.ProjectileBaseComponent>, RefRO<WD.ProjectileTargetComponent>>().WithAll<WD.ProjectileDestroyTag, WD.SingleTargetComponent>().WithEntityAccess())
            {
                var baseData = baseComp.ValueRO;
                var targetData = targetComp.ValueRO;

                // 还原发射者
                WarEleParent caster = SpatialGridManager.Instance.GetGridNode(baseData.p_casterEleType, baseData.p_casterGridIndex) as WarEleParent;
                // 还原受击者
                WarEleParent target = SpatialGridManager.Instance.GetGridNode(targetData.p_targetEleType, targetData.p_targetGridIndex) as WarEleParent;

                if (caster != null && target != null)
                {
                    bool isDie = false;
                    float hitValue = 0f;

                    // 调用旧有的 BeAttacked 逻辑
                    if (target is Soldier sd)
                    {
                        isDie = sd.BeAttacked(caster.gameObject, caster, caster.gs_warEleType, baseData.p_baseDamage, true, true, out hitValue);
                    }
                    else if (target is WarBuilding bd)
                    {
                        isDie = bd.BeAttacked(caster.gameObject, caster, caster.gs_warEleType, baseData.p_baseDamage, out hitValue);
                    }

                    // 触发对应的 BullectHit 回调
                    if (baseData.p_triggerSkill == true)
                    {
                        if (caster is Soldier cSd)
                            cSd.BullectHit(hitValue, isDie, target.gs_warEleType, target);
                        else if (caster is DefenceBuilding cBd)
                            cBd.BullectHit(hitValue, isDie, target.gs_warEleType, target);
                    }

                    // 目标死亡时移除仇恨
                    if (isDie == true)
                    {
                        if (caster is Soldier cSd)
                            cSd.TargetRemove(target.gs_warEleType, target.gameObject);
                        else if (caster is DefenceBuilding cBd)
                            cBd.TargetRemove(target.gs_warEleType, target.gameObject);
                    }
                }
            }

            // ==========================================
            // 2. 处理范围炮弹 (Shell) 的伤害与回调
            // ==========================================
            foreach (var (baseComp, areaComp, posComp, targetComp, entity) in SystemAPI.Query<RefRO<WD.ProjectileBaseComponent>, RefRO<WD.AreaDamageComponent>, RefRO<WD.ProjectilePositionComponent>, RefRO<WD.ProjectileTargetComponent>>().WithAll<WD.ProjectileDestroyTag>().WithEntityAccess())
            {
                var baseData = baseComp.ValueRO;
                var areaData = areaComp.ValueRO;
                var targetData = targetComp.ValueRO;

                // 【核心】：必须将坐标提取为局部变量。闭包只能捕获值，不能捕获 RefRO 指针！
                float2 hitPos = posComp.ValueRO.p_position;
                bool canAttackBuilding = areaData.p_canAttackBuilding;

                WarEleParent caster = SpatialGridManager.Instance.GetGridNode(baseData.p_casterEleType, baseData.p_casterGridIndex) as WarEleParent;
                WarEleParent mainTarget = SpatialGridManager.Instance.GetGridNode(targetData.p_targetEleType, targetData.p_targetGridIndex) as WarEleParent;

                if (caster != null)
                {
                    WE.WarEleType tType = WE.WarEleType.MIN;
                    if (mainTarget != null)
                    {
                        tType = mainTarget.gs_warEleType;
                    }

                    // 局部回调 1：提供爆炸范围参数
                    SearchShapeDef GetShape()
                    {
                        return new SearchShapeDef
                        {
                            p_shapeType = SearchDefines.SearchShapeType.CIRCLE,
                            p_centerOrStartPos = hitPos, // 使用被冻结的爆炸坐标
                            p_radius = areaData.p_damageRange,
                            p_radiusSq = areaData.p_damageRange * areaData.p_damageRange
                        };
                    }

                    // 局部回调 2：执行目标命中与技能结算
                    void OnFound(List<IGridNode> targets)
                    {
                        List<Soldier> sdList = new List<Soldier>();
                        List<WarBuilding> bdList = new List<WarBuilding>();
                        float totalHit = 0f;
                        int dieCount = 0;

                        for (int i = 0; i < targets.Count; i++)
                        {
                            if (targets[i] is Soldier sd)
                            {
                                float damage = areaData.p_otherDamage; //主目标外的范围内其他目标的攻击伤害
                                if (sd == mainTarget)
                                {
                                    damage = baseData.p_baseDamage;
                                }

                                float hitValue = 0f;
                                if (sd.BeAttacked(caster.gameObject, caster, caster.gs_warEleType, damage, true, true, out hitValue) == true)
                                {
                                    dieCount++;
                                }

                                sdList.Add(sd);

                                if (hitValue > 0)
                                {
                                    totalHit += hitValue;
                                }
                            }
                            else if (targets[i] is WarBuilding bd) // 假设默认能攻击建筑
                            {
                                float hitValue = 0f;
                                bd.BeAttacked(caster.gameObject, caster, caster.gs_warEleType, baseData.p_baseDamage, out hitValue);
                                bdList.Add(bd);

                                if (hitValue > 0)
                                {
                                    totalHit += hitValue;
                                }
                            }
                        }

                        // 触发对应的 ShellHit 回调
                        if (baseData.p_triggerSkill == true)
                        {
                            if (caster is Soldier cSd)
                            {
                                cSd.ShellHit(totalHit, dieCount, sdList, bdList, mainTarget, tType);
                            }
                            else if (caster is DefenceBuilding cBd)
                            {
                                cBd.ShellHit(totalHit, dieCount, sdList, bdList, mainTarget, tType);
                            }
                        }
                    }

                    SearchArea searcher = new SearchArea(0, OnFound, GetShape, null, baseData.p_mapId);

                    if (baseData.p_faction == WE.FactionType.FRIENDLY)
                    {
                        SearchConditionUtil.AddEnemySoldierConditions(searcher);
                        if (canAttackBuilding)
                            SearchConditionUtil.AddEnemyBuildingCondition(searcher);
                    }
                    else if (baseData.p_faction == WE.FactionType.ENEMY)
                    {
                        SearchConditionUtil.AddFriendlySoldierCondition(searcher);
                        if (canAttackBuilding)
                            SearchConditionUtil.AddFriendlyBuildingCondition(searcher);
                    }

                    // 正式抛给后端的 SearchManager 进行异步检索
                    SearchManager.Instance.RegisterSearch(searcher);
                }
            }
        }
    }

    // 专门处理线性穿透（边飞边造成伤害，且同一目标只伤害一次）
    [UpdateAfter(typeof(ProjectileMoveSystem))]
    public partial class ProjectilePenetrationSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            if (SearchManager.Instance == null || SpatialGridManager.Instance == null)
            {
                return;
            }

            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            foreach (var (baseComp, penComp, posComp, entity) in SystemAPI.Query<RefRO<WD.ProjectileBaseComponent>, RefRO<WD.LinearPenetrationComponent>, RefRO<WD.ProjectilePositionComponent>>().WithEntityAccess())
            {
                var baseData = baseComp.ValueRO;
                var penData = penComp.ValueRO;

                // 冻结当前帧的坐标、半径和实体引用
                float2 currentPos = posComp.ValueRO.p_position;
                float radius = penData.p_colliderRadius;
                Entity bulletEntity = entity;

                WarEleParent caster = SpatialGridManager.Instance.GetGridNode(baseData.p_casterEleType, baseData.p_casterGridIndex) as WarEleParent;
                if (caster == null)
                {
                    continue;
                }

                // 局部回调 1：提供检测范围
                SearchShapeDef GetShape()
                {
                    return new SearchShapeDef
                    {
                        p_shapeType = SearchDefines.SearchShapeType.CIRCLE,
                        p_centerOrStartPos = currentPos,
                        p_radius = radius,
                        p_radiusSq = radius * radius
                    };
                }

                // 局部回调 2：执行命中验证与伤害
                void OnFound(List<IGridNode> targets)
                {
                    // 异步回来时，如果这颗子弹已经因为达到最大距离被销毁，或者施法者死亡，则终止
                    if (entityManager.Exists(bulletEntity) == false)
                    {
                        return;
                    }
                    if (caster == null)
                    {
                        return;
                    }

                    // 取出这颗子弹的“记忆名单”
                    DynamicBuffer<WD.HitRecordElement> hitBuffer = entityManager.GetBuffer<WD.HitRecordElement>(bulletEntity);

                    for (int i = 0; i < targets.Count; i++)
                    {
                        IGridNode targetNode = targets[i];
                        WarEleParent targetEle = targetNode as WarEleParent;
                        if (targetEle == null)
                        {
                            continue;
                        }

                        int tEleType = (int)targetEle.gs_warEleType;
                        int tGridIndex = targetEle.gs_gridIndex;

                        // 核心：查重！判断这个人是不是已经被这颗子弹打过了
                        bool alreadyHit = false;
                        for (int j = 0; j < hitBuffer.Length; j++)
                        {
                            if (hitBuffer[j].p_targetEleType == tEleType && hitBuffer[j].p_targetGridIndex == tGridIndex)
                            {
                                alreadyHit = true;
                                break;
                            }
                        }

                        // 如果已经被打过，直接跳过他，检测下一个人
                        if (alreadyHit == true)
                        {
                            continue;
                        }

                        // 如果是新目标，造成伤害并把他的 ID 记入名单
                        if (targetEle is Soldier sd)
                        {
                            sd.BeAttacked(caster.gameObject, caster, caster.gs_warEleType, baseData.p_baseDamage, true, true, out _);

                            hitBuffer.Add(new WD.HitRecordElement
                            {
                                p_targetEleType = tEleType,
                                p_targetGridIndex = tGridIndex
                            });
                        }
                        else if (targetEle is WarBuilding bd)
                        {
                            bd.BeAttacked(caster.gameObject, caster, caster.gs_warEleType, baseData.p_baseDamage, out _);

                            hitBuffer.Add(new WD.HitRecordElement
                            {
                                p_targetEleType = tEleType,
                                p_targetGridIndex = tGridIndex
                            });
                        }
                    }
                }

                SearchArea searcher = new SearchArea(0, OnFound, GetShape, null, baseData.p_mapId);

                if (baseData.p_faction == WE.FactionType.FRIENDLY)
                {
                    SearchConditionUtil.AddEnemySoldierConditions(searcher);
                    SearchConditionUtil.AddEnemyBuildingCondition(searcher);
                }
                else if (baseData.p_faction == WE.FactionType.ENEMY)
                {
                    SearchConditionUtil.AddFriendlySoldierCondition(searcher);
                    SearchConditionUtil.AddFriendlyBuildingCondition(searcher);
                }

                SearchManager.Instance.RegisterSearch(searcher);
            }
        }
    }
}
