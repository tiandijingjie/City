using System.Collections.Generic;
using UnityEngine;

using Shaman;

namespace WarField
{
    using SKD = SkillDefines;
    using GD = GlobalDefines;
    using WE = WarFieldElements;
    using BFD = BuffDefines;
    using SD = SoldierDefines;

    //- 荆棘术：召唤荆棘困住一个敌人，敌人困住期间无法攻击，同时受到持续伤害，无法作用于英雄单位
    public class ShamanThronSnareSkill : Skill
    {
#region public parameters

#endregion

#region private parameters
        private ShamanIndividualData.SkillThronSnare _oriAttribute, _curAttribute;
        private (float, float) _value;

        private SearchArea _skillSearcher;
        private SearchShapeDef _skillSearchShape;
#endregion

#region private parameters' get set
        public override uint gs_skillType
        {
            get { return (uint)ShamanIndividualData.IndividualDataType.THRONSNARE; }
        }
#endregion

#region Unity callbacks
        private new void Awake()
        {
            base.Awake();
            _triggerType = new[]
            {
                SKD.SkillTriggerType.TIMETRIGGER,
                SKD.SkillTriggerType.MAPCHANGE
            };
        }
#endregion

#region public functions

#endregion

#region private functions
        protected override bool OnActiveSkill()
        {
            if (_oriAttribute == null)
            {
                _oriAttribute = new ShamanIndividualData.SkillThronSnare((_soldier.gs_oriIndividualData as ShamanIndividualData)
                    .gs_individualItems[(int)ShamanIndividualData.IndividualDataType.THRONSNARE]);
                _curAttribute = new ShamanIndividualData.SkillThronSnare(_oriAttribute);
            }
            else
            {
                _oriAttribute.ReInit((_soldier.gs_oriIndividualData as ShamanIndividualData)
                    .gs_individualItems[(int)ShamanIndividualData.IndividualDataType.THRONSNARE]);
                _curAttribute.ReInit(_oriAttribute);
            }
            _name = _oriAttribute.GetDescription().p_name;
            _soldier.AddStateChange(SD.StateSoldierEffectType.SKILLCOLLIDER, _curAttribute.p_range, GD.CalDeltaType.EQUAL, out float value);
            _curAttribute.p_intervalCycle = 0;
            _value = (_curAttribute.p_duration, _curAttribute.p_damagePerSecond);

            if (_skillSearcher == null)
            {
                _skillSearcher = new SearchArea(0, OnAreaTargetsFound, GetSearchShape, _soldier, _soldier.gs_mapId);
                _skillSearchShape = new SearchShapeDef();
                _skillSearchShape.p_shapeType = SearchDefines.SearchShapeType.CIRCLE;
                SearchConditionUtil.AddEnemySoldierAndBuildingConditions(_skillSearcher);
            }
            else
            {
                _skillSearcher.p_mapId = _soldier.gs_mapId;
            }
            return true;
        }

        protected override void OnSkillUpdate()
        {
            if (_curAttribute.p_intervalCycle > 0)
            {
                _curAttribute.p_intervalCycle -= _timeStep;
                return;
            }

            if (_soldier.CanTriggerActiveSkill() == false)
                return;

            SearchManager.Instance.RegisterSearch(_skillSearcher);
        }

        protected override void OnSkillMapChange(int fromMap, int toMap)
        {
            _skillSearcher.p_mapId = toMap;
        }

        private void OnAreaTargetsFound(List<IGridNode> targets)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                Soldier sd = targets[i] as Soldier;
                if (sd == null)
                    continue;
                if (sd.gs_sdLevel == SD.SoldierLevel.BOSSLEVEL)
                    continue;

                if (sd.CanAddBuff(BFD.SoldierBuffType.STUCK) == false)
                    continue;

                sd.BeAffectedByBuff(BFD.SoldierBuffType.STUCK, in _value);
                OnStartSkill();
                _curAttribute.p_intervalCycle = _oriAttribute.p_intervalCycle;
                return;
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
