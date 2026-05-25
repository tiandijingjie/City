using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Cannoneer;

namespace WarField
{
    using SKD = SkillDefines;
    using BFD = BuffDefines;
    using SD = SoldierDefines;
    using GD = GlobalDefines;
    using WE = WarFieldElements;

    //- 震慑：普通攻击一定的概率对攻击范围内的所有敌人造成移动速度，攻击速度的降低，不可作用于英雄单位
    public class CannoneerIntimidateSkill : Skill
    {
#region public parameters

#endregion

#region private parameters

        private CannoneerIndividualData.SkillIntimidate _oriAttribute;
        private (SD.StateSoldierEffectType, float, GD.CalDeltaType, float, string, BFD.BuffStrategy, object) _moveObj, _atkSpeedObj;

#endregion

#region private parameters' get set
        public override uint gs_skillType
        {
            get { return (uint)CannoneerIndividualData.IndividualDataType.INTIMIDATE; }
        }
#endregion

#region Unity callbacks
        private new void Awake()
        {
            base.Awake();
            _triggerType = new[] { SKD.SkillTriggerType.ATTACKTRIGGER };
        }
#endregion

#region public functions

#endregion

#region private functions
        protected override bool OnActiveSkill()
        {
            if (_oriAttribute == null)
            {
                _oriAttribute = new CannoneerIndividualData.SkillIntimidate((_soldier.gs_oriIndividualData as CannoneerIndividualData)
                    .gs_individualItems[(int)CannoneerIndividualData.IndividualDataType.INTIMIDATE]);
            }
            else
            {
                _oriAttribute.ReInit((_soldier.gs_oriIndividualData as CannoneerIndividualData)
                    .gs_individualItems[(int)CannoneerIndividualData.IndividualDataType.INTIMIDATE]);
            }
            _name = _oriAttribute.GetDescription().p_name;
            _moveObj = (SD.StateSoldierEffectType.MOVESPEED, _oriAttribute.p_moveDown, GD.CalDeltaType.MUL, _oriAttribute.p_duration,
                "CannoneerIntimidateSkill", BFD.BuffStrategy.OVERRIDE, (object)this);
            _atkSpeedObj = (SD.StateSoldierEffectType.ATTACKSPEED, _oriAttribute.p_atkSpeedDown, GD.CalDeltaType.MUL, _oriAttribute.p_duration,
                "CannoneerIntimidateSkill", BFD.BuffStrategy.OVERRIDE, (object)this);
            return true;
        }

        protected override void OnSkillDoAttackPost(float hit, int dieCount, List<Soldier> rivalSdList, List<WarBuilding> rivalBdList,
            MonoBehaviour target, WE.WarEleType type)
        {
            int chance = Utils.GetRandomInt();
            if (chance < _oriAttribute.p_chance)
            {
                int count = rivalSdList.Count;
                for (int i = 0; i < count; i++)
                {
                    Soldier sd = rivalSdList[i];
                    if (sd.gs_sdLevel == SoldierDefines.SoldierLevel.BOSSLEVEL)
                        continue;
                    sd.BeAffectedByBuff(BFD.SoldierBuffType.STATE, in _moveObj);
                    sd.BeAffectedByBuff(BFD.SoldierBuffType.STATE, in _atkSpeedObj);
                }
            }
        }

#endregion
    }
}
