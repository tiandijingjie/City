using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Cannoneer;

namespace WarField
{
    using SKD = SkillDefines;
    using WE = WarFieldElements;

    //- 攻城：如果主目标是建筑的时候普通攻击带有一定额外伤害
    public class CannoneerSiegeSkill : Skill
    {
#region public parameters

#endregion

#region private parameters
        private CannoneerIndividualData.SkillSiege _oriAttribute;
#endregion

#region private parameters' get set
        public override uint gs_skillType
        {
            get { return (uint)CannoneerIndividualData.IndividualDataType.SIEGE; }
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
                _oriAttribute = new CannoneerIndividualData.SkillSiege((_soldier.gs_oriIndividualData as CannoneerIndividualData)
                    .gs_individualItems[(int)CannoneerIndividualData.IndividualDataType.SIEGE]);
            }
            else
            {
                _oriAttribute.ReInit((_soldier.gs_oriIndividualData as CannoneerIndividualData)
                    .gs_individualItems[(int)CannoneerIndividualData.IndividualDataType.SIEGE]);
            }
            _name = _oriAttribute.GetDescription().p_name;
            return true;
        }

        protected override bool OnSkillDoAttackPre(float hit, MonoBehaviour rivalScript, WarFieldElements.WarEleType rivalType, out float damage)
        {
            if (rivalType == WE.WarEleType.BUILDING)
                damage = hit * _oriAttribute.p_damageUp;
            else
                damage = hit;
            return true;
        }

#endregion
    }
}
