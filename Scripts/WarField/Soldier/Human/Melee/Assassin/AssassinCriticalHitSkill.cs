using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Assassin;

namespace WarField
{
    using SKD = SkillDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //刺客技能
    //暴击：普通攻击有概率对士兵造成额外伤害
    public class AssassinCriticalHitSkill : Skill
    {
#region public parameters

#endregion

#region private parameters
        private AssassinIndividualData.SkillCrit _oriAttribute = null;

#endregion

#region private parameters' get set

        public override uint gs_skillType
        {
            get { return (uint)AssassinIndividualData.IndividualDataType.CRIT; }
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
            if(_oriAttribute == null)
                _oriAttribute = new AssassinIndividualData.SkillCrit((_soldier.gs_oriIndividualData as AssassinIndividualData)
                    .gs_individualItems[(int)AssassinIndividualData.IndividualDataType.CRIT]);
            else
                _oriAttribute.ReInit((_soldier.gs_oriIndividualData as AssassinIndividualData)
                    .gs_individualItems[(int)AssassinIndividualData.IndividualDataType.CRIT]);
            _name = _oriAttribute.GetDescription().p_name;
            return true;
        }

        protected override bool OnSkillDoAttackPre(float hit, MonoBehaviour rivalScript, WarFieldElements.WarEleType rivalType, out float damage)
        {
            if (rivalType == WE.WarEleType.SOLDIER)
            {
                if (hit > 0)
                {
                    int chance = Utils.GetRandomInt();
                    if (chance < _oriAttribute.p_criticalChance)
                        hit = hit * _oriAttribute.p_critTimes;
                }
            }

            damage = hit;
            return true;
        }

#endregion

    }
}

