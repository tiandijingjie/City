using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Priest;

namespace WarField
{
    using SKD = SkillDefines;
    using SD = SoldierDefines;
    using GD = GlobalDefines;
    using BFD = BuffDefines;
    using WE = WarFieldElements;

    public class PriestLifeShareSkill : Skill
    {
#region public parameters

#endregion

#region private parameters

        private PriestIndividualData.SkillLifeShare _oriAttribute, _curAttribute;
        private Priest _priest;
        private List<Soldier> _parterList = new List<Soldier>();
        private System.Object _lock;

#endregion

#region private parameters' get set
        public override uint gs_skillType
        {
            get { return (uint)PriestIndividualData.IndividualDataType.LIFESHARE; }
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
                _oriAttribute = new PriestIndividualData.SkillLifeShare((_soldier.gs_oriIndividualData as PriestIndividualData)
                    .gs_individualItems[(int)PriestIndividualData.IndividualDataType.LIFESHARE]);
                _curAttribute = new PriestIndividualData.SkillLifeShare(_oriAttribute);
            }
            else
            {
                _oriAttribute.ReInit((_soldier.gs_oriIndividualData as PriestIndividualData)
                    .gs_individualItems[(int)PriestIndividualData.IndividualDataType.LIFESHARE]);
                _curAttribute.ReInit(_oriAttribute);
            }
            _name = _oriAttribute.GetDescription().p_name;
            _soldier.AddStateChange(SD.StateSoldierEffectType.SKILLCOLLIDER, _curAttribute.p_range, GD.CalDeltaType.EQUAL, out float value);
            _soldier.AddStateChange(SD.StateSoldierEffectType.HPINC, _curAttribute.p_hpIncUp, GD.CalDeltaType.ADD, out value);
            _soldier.AddStateChange(SD.StateSoldierEffectType.HEALTH, _curAttribute.p_hpMaxUp, GD.CalDeltaType.MUL, out value);
            _priest = _soldier as Priest;
            _curAttribute.p_intervalCycle = 0;
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

                bool hpAdd = false;
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
                    float cure = targetParter.BeCure(null, _soldier.gs_curState.p_health * _curAttribute.p_hpsharePercent, GD.CalDeltaType.ADD);
                    if (cure > 0)
                    {
                        _priest.ShareLife(cure);
                        OnStartSkill(); //animation not trigger anything
                        _curAttribute.p_intervalCycle = _oriAttribute.p_intervalCycle;
                    }
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
