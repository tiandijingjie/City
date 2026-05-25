using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using SharpShooter;

namespace WarField
{
    using SKD = SkillDefines;

    //要害打击：普通攻击会造成攻速相关倍数的伤害
    public class SharpShooterVitalStrikeSkill : Skill
    {
#region public parameters

#endregion

#region private parameters

        private SharpShooterIndividualData.SkillVitalStrike _oriAttribute, _curAttribute;

#endregion

#region private parameters' get set
        public override uint gs_skillType
        {
            get { return (uint)SharpShooterIndividualData.IndividualDataType.VITALSTRIKE; }
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
                _oriAttribute = new SharpShooterIndividualData.SkillVitalStrike((_soldier.gs_oriIndividualData as SharpShooterIndividualData)
                    .gs_individualItems[(int)SharpShooterIndividualData.IndividualDataType.VITALSTRIKE]);
                _curAttribute = new SharpShooterIndividualData.SkillVitalStrike(_oriAttribute);
            }
            else
            {
                _oriAttribute.ReInit((_soldier.gs_oriIndividualData as SharpShooterIndividualData)
                    .gs_individualItems[(int)SharpShooterIndividualData.IndividualDataType.VITALSTRIKE]);
                _curAttribute.ReInit(_oriAttribute);
            }
            _name = _oriAttribute.GetDescription().p_name;
            return true;
        }

        protected override bool OnSkillDoAttackPre(float hit, MonoBehaviour rivalScript, WarFieldElements.WarEleType rivalType, out float damage)
        {
            if (_curAttribute.p_atkGap > 0)
            {
                _curAttribute.p_atkGap--;
                damage = hit;
            }
            else
            {
                damage = hit * (1 + _soldier.gs_curState.p_attackSpeed * _curAttribute.p_damageUp);
                _curAttribute.p_atkGap = _oriAttribute.p_atkGap;
            }
            return true;
        }

#endregion
    }
}

