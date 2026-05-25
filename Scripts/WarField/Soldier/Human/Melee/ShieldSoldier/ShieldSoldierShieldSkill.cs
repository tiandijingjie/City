using System.Collections.Generic;
using UnityEngine;

using ShieldSoldier;

namespace WarField
{
    using SKD = SkillDefines;
    using BFD = BuffDefines;
    using SD = SoldierDefines;
    using GD = GlobalDefines;
    using WE = WarFieldElements;

    //护盾：获取充能型护盾吸收伤害，护盾被打破时对周围造成伤害
    public class ShieldSoldierShieldSkill : Skill
    {
#region public parameters

#endregion

#region private parameters
        private ShieldSoldierIndividualData.SkillShield _oriAttribute;

        private SearchArea _skillSearcher;
        private SearchShapeDef _skillSearchShape;
#endregion

#region private parameters' get set
        public override uint gs_skillType
        {
            get { return (uint)ShieldSoldierIndividualData.IndividualDataType.SHIELD; }
        }
#endregion

#region Unity callbacks
        private new void Awake()
        {
            base.Awake();
            _triggerType = new[]
            {
                SKD.SkillTriggerType.ACTIVETRIGGER,
                SKD.SkillTriggerType.MAPCHANGE
            };
        }
#endregion

#region public functions

#endregion

#region private functions
        protected unsafe override bool OnActiveSkill()
        {
            if (_oriAttribute == null)
            {
                _oriAttribute = new ShieldSoldierIndividualData.SkillShield((_soldier.gs_oriIndividualData as ShieldSoldierIndividualData)
                    .gs_individualItems[(int)ShieldSoldierIndividualData.IndividualDataType.SHIELD]);
            }
            else
            {
                _oriAttribute.ReInit((_soldier.gs_oriIndividualData as ShieldSoldierIndividualData)
                    .gs_individualItems[(int)ShieldSoldierIndividualData.IndividualDataType.SHIELD]);
            }
            _name = _oriAttribute.GetDescription().p_name;
            //skill collider will be disabled during the soldier's deinit
            _soldier.AddStateChange(SD.StateSoldierEffectType.SKILLCOLLIDER, _oriAttribute.p_damageRange, GD.CalDeltaType.EQUAL, out float ori);

            if (_skillSearcher == null)
            {
                _skillSearcher = new SearchArea(0, OnAreaTargetsFound, GetSearchShape, _soldier, _soldier.gs_mapId); //同步查找
                _skillSearchShape = new SearchShapeDef();
                _skillSearchShape.p_shapeType = SearchDefines.SearchShapeType.CIRCLE;
                SearchConditionUtil.AddEnemySoldierAndBuildingConditions(_skillSearcher);
            }
            else
            {
                _skillSearcher.p_mapId = _soldier.gs_mapId;
            }

            var value = (BFD.ShieldBuffType.CHARGEABLE, _oriAttribute.p_shieldAbsorb, _oriAttribute.p_shieldBrokenRecoverTime, _oriAttribute.p_shieldPeaceRecoverTime);
            _soldier.BeAffectedByBuff(BFD.SoldierBuffType.SHIELD, in value, ShieldBrokenCallback);
            return true;
        }

        protected override void OnSkillMapChange(int fromMap, int toMap)
        {
            _skillSearcher.p_mapId = toMap;
        }

        //护盾破裂回调
        protected unsafe void ShieldBrokenCallback(BFD.BuffCallBackEventType type, void* value)
        {
            _skillSearcher.p_mapId = _soldier.gs_mapId;
            SearchManager.Instance.RegisterSearch(_skillSearcher);
        }

        private void OnAreaTargetsFound(List<IGridNode> targets)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                Soldier sd = targets[i] as Soldier;
                if (sd == null)
                    continue;

                sd.BeAttacked(_soldier.gameObject, _soldier, WE.WarEleType.SOLDIER, _oriAttribute.p_brokenDamage, false, false, out float damage);
            }
        }

        private SearchShapeDef GetSearchShape()
        {
            _skillSearchShape.p_centerOrStartPos = (Vector2)_soldier.gs_transform.position;
            float radius = _soldier.gs_skillRadius;
            if (Mathf.Abs(radius - _skillSearchShape.p_radius) > 0.1f)
            {
                _skillSearchShape.p_radius = radius;
                _skillSearchShape.p_radiusSq = Mathf.Pow(radius, 2);
            }
            return _skillSearchShape;
        }
#endregion
    }
}
