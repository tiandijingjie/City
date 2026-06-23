using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using WarField.EffectAnim;

// CircleAreaEffectAnim — pooled IEffectAnimInfo for one-shot circular AoE VFX.
//
// Typical usage:
//   var ca = EffectCtrl.Instance.AcquireCircleAreaEffect();
//   ca.p_effectAnimId    = (uint)ED.EffectType.EXPLOSION;
//   ca.p_worldPos        = hitPos;
//   ca.p_searchRadius    = 3f;
//   ca.p_mapId           = mapId;
//   ca.p_targetFaction   = WE.FactionType.ENEMY;
//   ca.p_searchTarget    = CircleAreaEffectAnim.SearchTarget.Soldier;
//   ca.p_onTargetsFound  = HandleHits;
//   EffectHandle h = EffectCtrl.Instance.AddEffectAt(hitPos, ca);
//   ca.Activate(h);                          // launches search + animation
//
// The instance returns itself to the pool automatically when the VFX clip ends
// (IEffectAnimInfo_OnEffectAnimEvent stateId == -1).

namespace WarField
{
    using WE = WarFieldElements;

    //圆形的
    public class CircleAreaEffectAnim : IEffectAnimInfo
    {
        // ── What entity types to search for ─────────────────────────────────
        public enum SearchTarget
        {
            Soldier,            // soldiers only
            Building,           // buildings only
            SoldierAndBuilding, // both
        }

        // ── Config — set by caller before Activate() ─────────────────────────
        public uint                    p_effectAnimId;
        public Vector3                 p_worldPos;
        public float                   p_searchRadius;
        public byte                    p_mapId;

        /// <summary>Which faction's targets to hit (ENEMY = hit Oculars, FRIENDLY = hit Humans).</summary>
        public WE.FactionType p_targetFaction;
        public SearchTarget p_searchTarget = SearchTarget.Soldier;
        public float p_searchTime = 0; //一般通过GetTimeForASyncSearchCnt来设定异步查找的次数, 默认是同步查询

        /// <summary>
        /// Called with all found targets once the spatial search completes.
        /// The list is ephemeral — copy any references you want to keep.
        /// </summary>
        public Action<List<IGridNode>, CircleAreaEffectAnim> p_onTargetsFound; //同步查找
        public Action<int, CircleAreaEffectAnim> p_onEvent;

        // ── Runtime ──────────────────────────────────────────────────────────
        private EffectHandle                          _handle        = EffectHandle.Invalid;
        private readonly Action<CircleAreaEffectAnim> _returnToPool;
        private readonly SearchArea                   _searcher;

        internal CircleAreaEffectAnim(Action<CircleAreaEffectAnim> returnToPool)
        {
            _returnToPool = returnToPool;
            // SearchArea is reused across pool cycles; only conditions are rebuilt each use
            _searcher = new SearchArea(0, OnSearcherFound, GetShape, null, 0);
        }

        /// <summary>
        /// Finalise setup after AddEffectAt: stores the ECS handle and registers
        /// the spatial search.  Must be called immediately after AddEffectAt.
        /// </summary>
        public void Activate(EffectHandle handle)
        {
            _handle = handle;
            _searcher.p_duration = p_searchTime;
            // Rebuild conditions for this use (faction + target type may change)
            _searcher.p_conditions.Clear();
            _searcher.p_mapId = p_mapId;
            BuildConditions();
        }

        // ── IEffectAnimInfo ──────────────────────────────────────────────────

        public uint IEffectAnimInfo_GetEffectAnimId()
        {
            return p_effectAnimId;
        }

        /// <summary>
        /// eventId >= 0  → eventFrame callback
        /// eventId == -1 → animation finished → release ECS entity + return to pool.
        /// </summary>
        public void IEffectAnimInfo_OnEffectAnimEvent(int eventId)
        {
            if (_handle.IsValid() == false)
                return;

            p_onEvent?.Invoke(eventId, this);
            if (eventId == -1)
            {
                EffectCtrl.Instance?.ReleaseEffect(_handle);
                _handle = EffectHandle.Invalid;

                // Clear caller references before returning so GC can collect closures
                p_onTargetsFound = null;
                p_onEvent        = null;

                _returnToPool?.Invoke(this);
            }
        }

        //开始查找
        public void StartSearch()
        {
            SearchManager.Instance.RegisterSearch(_searcher);
        }

        // ── Private helpers ──────────────────────────────────────────────────

        private void OnSearcherFound(List<IGridNode> targets)
        {
            p_onTargetsFound?.Invoke(targets, this);
        }

        private SearchShapeDef GetShape()
        {
            return SearchShapeDef.CreateCircle(new float2(p_worldPos.x, p_worldPos.y), p_searchRadius);
        }

        private void BuildConditions()
        {
            switch (p_targetFaction)
            {
                case WE.FactionType.ENEMY:
                    switch (p_searchTarget)
                    {
                        case SearchTarget.Soldier:
                            SearchConditionUtil.AddEnemySoldierConditions(_searcher);
                            break;
                        case SearchTarget.Building:
                            SearchConditionUtil.AddEnemyBuildingCondition(_searcher);
                            break;
                        case SearchTarget.SoldierAndBuilding:
                            SearchConditionUtil.AddEnemySoldierAndBuildingConditions(_searcher);
                            break;
                    }
                    break;

                case WE.FactionType.FRIENDLY:
                    switch (p_searchTarget)
                    {
                        case SearchTarget.Soldier:
                            SearchConditionUtil.AddFriendlySoldierCondition(_searcher);
                            break;
                        case SearchTarget.Building:
                            SearchConditionUtil.AddFriendlyBuildingCondition(_searcher);
                            break;
                        case SearchTarget.SoldierAndBuilding:
                            SearchConditionUtil.AddFriendlySoldierCondition(_searcher);
                            SearchConditionUtil.AddFriendlyBuildingCondition(_searcher);
                            break;
                    }
                    break;
            }
        }
    }
}
