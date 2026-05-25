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

    //自爆  英雄死亡时会爆炸对周围敌人和建筑造成大量伤害  范围:7.5  伤害:280
    public class HeroSelfDestructTalent : Talent
    {
#region public parameters

#endregion

#region private parameters
        private HeroGenericIndividualData.TalentSelfDestruct _curTalent = null;

        private SearchArea _skillSearcher;
        private SearchShapeDef _skillSearchShape;
        private Vector2 _searchCenter;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks
        protected override void Awake()
        {
            base.Awake();
            _triggerType = new[] { SKD.SkillTriggerType.DIETRIGGER };
            for (int i = 0; i < _triggerType.Length; i++)
                _soldier.RegisterTalent(this, _triggerType[i]);  //talent need to do register during awake by itself
        }
#endregion

#region public functions
        protected override bool OnActiveTalent()
        {
            if (_curTalent == null)
                _curTalent = (HeroGenericIndividualData.TalentSelfDestruct)(((Hero)_soldier).gs_genericData as HeroGenericIndividualData)
                    .gs_individualItems[(int)HeroGenericIndividualData.IndividualDataType.SELFDESTRUCT];
            _name = _curTalent.GetDescription().p_name;

            if (_skillSearcher == null)
            {
                _skillSearcher = new SearchArea(0, OnAreaTargetsFound, GetSearchShape, _soldier, _soldier.gs_mapId);
                _skillSearchShape = new SearchShapeDef { p_shapeType = SearchDefines.SearchShapeType.CIRCLE };
                SearchConditionUtil.AddEnemySoldierAndBuildingConditions(_skillSearcher);
            }

            return true;
        }

        protected override void OnTalentDie()
        {
            Vector2 pos = _soldier.gs_transform.position;
            EffectCtrl.Instance.AddEffectAt(pos, ED.EffectType.SELFEXPLOSION, _soldier.gs_mapId, 2.5f);
            _searchCenter = pos;
            _skillSearcher.p_mapId = _soldier.gs_mapId;
            SearchManager.Instance.RegisterSearch(_skillSearcher);
        }

#endregion

#region private functions

        private void OnAreaTargetsFound(List<IGridNode> targets)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] is Soldier sd)
                {
                    sd.BeAttacked(null, null, WE.WarEleType.SOLDIER, _curTalent.p_damage, false, false, out float damage);
                }
                else if (targets[i] is WarBuilding wb)
                {
                    wb.BeAttacked(null, null, WE.WarEleType.SOLDIER, _curTalent.p_damage, out float hitDamage);
                }
            }
        }

        private SearchShapeDef GetSearchShape()
        {
            float radius = _curTalent.p_range + 0.5f;
            _skillSearchShape.p_centerOrStartPos = new float2(_searchCenter.x, _searchCenter.y);
            _skillSearchShape.p_radius = radius;
            _skillSearchShape.p_radiusSq = radius * radius;
            return _skillSearchShape;
        }

#endregion
    }
}
