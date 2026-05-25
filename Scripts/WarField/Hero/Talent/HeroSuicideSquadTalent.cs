using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

using HeroGeneral;

namespace WarField
{
    using SKD = SkillDefines;
    using SD = SoldierDefines;
    using GD = GlobalDefines;
    using WE = WarFieldElements;
    using ED = EffectDefines;

    //全部己方士兵在死亡时有一定概率会自爆,无法攻击建筑  自爆范围：3.5  同类型士兵概率：18%  不同类型士兵概率：8%  伤害：40
    public class HeroSuicideSquadTalent : Talent, ISoldierDieNotify
    {
#region public parameters

#endregion

#region private parameters
        private HeroGenericIndividualData.TalentSuicideSquad _curTalent = null;

        private SD.TroopType _troop;
        private object _lock;
        private SearchArea _skillSearcher;
        private SearchShapeDef _skillSearchShape;
        private Vector2 _explosionCenter;
        private byte _explosionMapId;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks
        protected override void Awake()
        {
            base.Awake();
        }
#endregion

#region public functions

        public void SoldierDieIntf(WarFieldElements.FactionType faction, SoldierDefines.TroopType troop, Soldier soldier)
        {
            lock (_lock)
            {
                int targetChance = 0;
                if (troop == _troop)
                    targetChance = _curTalent.p_sameTroopChance;
                else
                    targetChance = _curTalent.p_otherTroopChance;

                if (Utils.GetRandomInt() < targetChance)
                {
                    Vector2 pos = soldier.gs_transform.position;
                    EffectCtrl.Instance.AddEffectAt(pos, ED.EffectType.SELFEXPLOSION, soldier.gs_mapId, 1.0f);
                    _explosionCenter = pos;
                    _explosionMapId = soldier.gs_mapId;
                    _skillSearcher.p_mapId = _explosionMapId;
                    SearchManager.Instance.RegisterSearch(_skillSearcher);
                }
            }
        }
#endregion

#region private functions
        protected override bool OnActiveTalent()
        {
            if (_curTalent == null)
                _curTalent = (HeroGenericIndividualData.TalentSuicideSquad)(((Hero)_soldier).gs_genericData as HeroGenericIndividualData)
                    .gs_individualItems[(int)HeroGenericIndividualData.IndividualDataType.SUICIDESQUAD];
            _name = _curTalent.GetDescription().p_name;
            _troop = ((Hero)_soldier).gs_troopType;
            for (int i = 1; i < (int)SD.TroopType.MAX; i++)
                SoldierCtrl.Instance.RegisterSoliderDieNotify(this, WE.FactionType.FRIENDLY, (SD.TroopType)i);
            _lock = new object();

            if (_skillSearcher == null)
            {
                _skillSearcher = new SearchArea(0, OnAreaTargetsFound, GetSearchShape, null, _soldier.gs_mapId);
                _skillSearchShape = new SearchShapeDef { p_shapeType = SearchDefines.SearchShapeType.CIRCLE };
                SearchConditionUtil.AddEnemySoldierConditions(_skillSearcher);
            }

            return true;
        }

        private void OnAreaTargetsFound(List<IGridNode> targets)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                Soldier sd = targets[i] as Soldier;
                if (sd == null)
                    continue;
                sd.BeAttacked(null, null, WE.WarEleType.SOLDIER, _curTalent.p_damage, false, false,
                    out float damage);
            }
        }

        private SearchShapeDef GetSearchShape()
        {
            float radius = _curTalent.p_range + 0.3f;
            _skillSearchShape.p_centerOrStartPos = new float2(_explosionCenter.x, _explosionCenter.y);
            _skillSearchShape.p_radius = radius;
            _skillSearchShape.p_radiusSq = radius * radius;
            return _skillSearchShape;
        }
#endregion
    }
}
