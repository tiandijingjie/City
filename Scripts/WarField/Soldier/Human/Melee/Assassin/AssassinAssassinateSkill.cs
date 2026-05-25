using System.Collections;
using System.Collections.Generic;
using Assassin;
using UnityEngine;

namespace WarField
{
    using SKD = SkillDefines;
    using WE = WarFieldElements;
    using BFD = BuffDefines;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //暗杀：每隔一段时间，对目标敌人造成纯粹伤害
    public class AssassinAssassinateSkill : Skill
    {
#region public parameters

#endregion

#region private parameters

        private AssassinIndividualData.SkillAssassinate _oriAttribute = null, _curAttribute = null;
        private bool _canTrigger;

#endregion

#region private parameters' get set
        public override uint gs_skillType
        {
            get { return (uint)AssassinIndividualData.IndividualDataType.ASSASSINATE; }
        }
#endregion

#region Unity callbacks
        private new void Awake()
        {
            base.Awake();
            _triggerType = new[] { SKD.SkillTriggerType.TIMETRIGGER };
        }
#endregion

#region public functions

#endregion

#region private functions
        protected override bool OnActiveSkill()
        {
            if (_oriAttribute == null)
            {
                _oriAttribute = new AssassinIndividualData.SkillAssassinate((_soldier.gs_oriIndividualData as AssassinIndividualData)
                    .gs_individualItems[(int)AssassinIndividualData.IndividualDataType.ASSASSINATE]);
                _curAttribute = new AssassinIndividualData.SkillAssassinate(_oriAttribute);
            }
            else
            {
                _oriAttribute.ReInit((_soldier.gs_oriIndividualData as AssassinIndividualData)
                    .gs_individualItems[(int)AssassinIndividualData.IndividualDataType.ASSASSINATE]);
                _curAttribute.ReInit(_oriAttribute);
            }
            _curAttribute.p_intervalCycle = 0;//enable at born
            _name = _curAttribute.GetDescription().p_name;
            return true;
        }

        protected override void OnSkillUpdate()
        {
            if (_curAttribute.p_intervalCycle <= 0)
            {
                if (_canTrigger == true)
                    return;

                if (_soldier.gs_curStatus == SD.SoldierStatus.ATTACKTATGET)
                {
                    if (_soldier.CanTriggerActiveSkill() == true)
                    {
                        _canTrigger = true;
                        OnStartSkill();
                    }
                }
            }
            else
                _curAttribute.p_intervalCycle -= _timeStep;
        }

        protected override void OnSkillAnimTakeEffect(string value)
        {
            if (_soldier.gs_rivalType == WE.WarEleType.SOLDIER)
            {
                Soldier rival = (Soldier)_soldier.gs_rivalScript;
                if (rival != null && Utils.IsEnumInRange(rival.gs_curStatus, SD.SoldierStatus.MIN, SD.SoldierStatus.DIE) == true)
                {
                    if (_soldier.gs_sdLevel != SD.SoldierLevel.BOSSLEVEL)
                    {
                        var state = rival.gs_curState;
                        float maxHp = state.p_maxHealth;
                        float hp = state.p_health;
                        if (hp <= maxHp * _curAttribute.p_executionPercent) //kill the soldier
                            rival.BeAttacked(_soldier.gameObject, this, WE.WarEleType.SOLDIER,
                                WE.MaxDamage, //1000000 just set a big value to make sure skill
                                true, true, out float hitValue);
                        else
                            rival.BeAttacked(_soldier.gameObject, this, WE.WarEleType.SOLDIER, _curAttribute.p_damage, true, true,
                                out float hitValue);
                    }
                    else //boss
                    {
                        rival.BeAttacked(_soldier.gameObject, this, WE.WarEleType.SOLDIER, _curAttribute.p_damage, true, true, out float hitValue);
                    }

                    _canTrigger = false;
                    _curAttribute.p_intervalCycle += _oriAttribute.p_intervalCycle;

                    EffectCtrl.Instance.AddEffectAt(_soldier.gs_transform.position + new Vector3(0.8f, 0.15f, 0),
                        EffectDefines.EffectType.ASSASSINSKILL,
                        _soldier.gs_mapId, AssassinIndividualData.IndividualDataType.ASSASSINATE);
                }
            }
        }

        protected override void OnSkillAnimInterrupted(SoldierDefines.SoldierAnimType interruptAnim)
        {
            _canTrigger = false;
        }

#endregion
    }
}

