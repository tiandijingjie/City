using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Priest;
using Object = System.Object;

namespace WarField
{
    using SKD = SkillDefines;
    using SD = SoldierDefines;
    using GD = GlobalDefines;
    using BFD = BuffDefines;

    //- 回春：每隔一段时间为范围内损失生命值最多的友军持续回复一段的生命，不可叠加但可以刷新持续时间
    public class PriestRejuvenationSkill : Skill
    {
#region public parameters

#endregion

#region private parameters
        private PriestIndividualData.SkillRejuvenation _oriAttribute, _curAttribute;
        private List<Soldier> _parterList = new List<Soldier>();
        private System.Object _lock;
        private (SD.StateSoldierEffectType, float, GD.CalDeltaType, float, string, BFD.BuffStrategy, object) _hpIncObj;
#endregion

#region private parameters' get set
        public override uint gs_skillType
        {
            get { return (uint)PriestIndividualData.IndividualDataType.REJUVENATION; }
        }
#endregion

#region Unity callbacks
        private new void Awake()
        {
            base.Awake();
            _triggerType = new[]
            {
                SKD.SkillTriggerType.TIMETRIGGER, SKD.SkillTriggerType.PARTERENTERSKILLRANGEDTRIGGER,
                SKD.SkillTriggerType.PARTERLEAVESKILLRANGEDTRIGGER
            };
            _lock = new object();
        }
#endregion

#region public functions

#endregion

#region private functions
        protected override bool OnActiveSkill()
        {
            if (_oriAttribute == null)
            {
                _oriAttribute = new PriestIndividualData.SkillRejuvenation((_soldier.gs_oriIndividualData as PriestIndividualData)
                    .gs_individualItems[(int)PriestIndividualData.IndividualDataType.REJUVENATION]);
                _curAttribute = new PriestIndividualData.SkillRejuvenation(_oriAttribute);
            }
            else
            {
                _oriAttribute.ReInit((_soldier.gs_oriIndividualData as PriestIndividualData)
                    .gs_individualItems[(int)PriestIndividualData.IndividualDataType.REJUVENATION]);
                _curAttribute.ReInit(_oriAttribute);
            }
            _name = _oriAttribute.GetDescription().p_name;
            _soldier.AddStateChange(SD.StateSoldierEffectType.SKILLCOLLIDER, _curAttribute.p_range, GD.CalDeltaType.EQUAL, out float value);
            _curAttribute.p_intervalCycle = 0;
            _hpIncObj = (SD.StateSoldierEffectType.HPINC, _curAttribute.p_hpIncUp, GD.CalDeltaType.ADD, _curAttribute.p_duration,
                "PriestRejuvenationSkill", BFD.BuffStrategy.OVERRIDE, (object)this);
            return true;
        }

        protected override void OnSkillUpdate()
        {
            if (_curAttribute.p_intervalCycle > 0)
            {
                _curAttribute.p_intervalCycle -= _timeStep;
            }
            else
            {
                if(_soldier.CanTriggerActiveSkill() == false)
                    return;

                int count = _parterList.Count;
                float maxHPLost = 0;
                Soldier targetParter = null;
                lock (_lock)
                {
                    for (int i = 0; i < count; i++)
                    {
                        Soldier parter = _parterList[i];
                        if (parter.gs_curState.p_health > 0) //make sure the parter not die
                        {
                            float lost = parter.gs_curState.p_maxHealth - parter.gs_curState.p_health;
                            if (lost > maxHPLost)
                            {
                                maxHPLost = lost;
                                targetParter = parter;
                            }
                        }
                    }
                }

                if (maxHPLost > 1) //make sure someone lost hp
                {
                     //add blood at once, in case after the animation the target die
                     targetParter.BeAffectedByBuff(BFD.SoldierBuffType.STATE, in _hpIncObj);
                     OnStartSkill();  //animation not trigger anything
                     _curAttribute.p_intervalCycle = _oriAttribute.p_intervalCycle;
                }
            }
        }

        protected override void OnSkillParterEnter(GameObject parter, WarFieldElements.WarEleType type)
        {
            if (type == WarFieldElements.WarEleType.SOLDIER)
            {
                lock(_lock)
                    _parterList.Add(parter.GetComponent<Soldier>());
            }
        }

        protected override void OnSkillParterLeave(GameObject parter, WarFieldElements.WarEleType type)
        {
            if (type == WarFieldElements.WarEleType.SOLDIER)
            {
                lock(_lock)
                    _parterList.Remove(parter.GetComponent<Soldier>());
            }
        }

        protected override void OnDeactiveSkill()
        {
            _parterList.Clear();
        }

#endregion
    }
}

