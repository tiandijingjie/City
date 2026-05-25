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

    //流血：攻击范围内的敌人有一定概率受到持续伤害，不可叠加，不可作用于英雄单位
    public class CannoneerBleedingSkill : Skill
    {
#region public parameters

#endregion

#region private parameters
        private CannoneerIndividualData.SkillBleeding _oriAttribute;
        private (float, float, string, object, BFD.BuffStrategy) _damgeObj;
#endregion

#region private parameters' get set
        public override uint gs_skillType
        {
            get { return (uint)CannoneerIndividualData.IndividualDataType.BLEEDING; }
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
                _oriAttribute = new CannoneerIndividualData.SkillBleeding((_soldier.gs_oriIndividualData as CannoneerIndividualData)
                    .gs_individualItems[(int)CannoneerIndividualData.IndividualDataType.BLEEDING]);
            }
            else
            {
                _oriAttribute.ReInit((_soldier.gs_oriIndividualData as CannoneerIndividualData)
                    .gs_individualItems[(int)CannoneerIndividualData.IndividualDataType.BLEEDING]);
            }
            _name = _oriAttribute.GetDescription().p_name;
            _damgeObj = (_oriAttribute.p_damage, _oriAttribute.p_duration, "CannoneerBleedingSkill", (object)this,
                BFD.BuffStrategy.OVERRIDE);
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
                    sd.BeAffectedByBuff(BFD.SoldierBuffType.DURATIONDAMAGE, in _damgeObj);
                }
            }
        }

#endregion
    }
}
