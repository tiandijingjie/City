using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using SharpShooter;

namespace WarField
{
    using SKD = SkillDefines;

    //蓄力击：每隔一段时间蓄力一定时间后射出一箭打击一条直线上的所有敌人
    public class SharpShooterChargeStrikeSkill : Skill
    {
#region public parameters

#endregion

#region private parameters
        private SharpShooterIndividualData.SkillChargeStrike _oriAttribute, _curAttribute;
#endregion

#region private parameters' get set
        public override uint gs_skillType
        {
            get { return (uint)SharpShooterIndividualData.IndividualDataType.CHARGEDSTRIKE; }
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
                _oriAttribute = new SharpShooterIndividualData.SkillChargeStrike((_soldier.gs_oriIndividualData as SharpShooterIndividualData)
                    .gs_individualItems[(int)SharpShooterIndividualData.IndividualDataType.CHARGEDSTRIKE]);
                _curAttribute = new SharpShooterIndividualData.SkillChargeStrike(_oriAttribute);
            }
            else
            {
                _oriAttribute.ReInit((_soldier.gs_oriIndividualData as SharpShooterIndividualData)
                    .gs_individualItems[(int)SharpShooterIndividualData.IndividualDataType.CHARGEDSTRIKE]);
                _curAttribute.ReInit(_oriAttribute);
            }
            _name = _oriAttribute.GetDescription().p_name;
            _curAttribute.p_intervalCycle = 0;
            return true;
        }

        protected override void OnSkillUpdate()
        {
            if(_curAttribute.p_intervalCycle > 0)
                _curAttribute.p_intervalCycle -= _timeStep;
            else
            {
                if(_soldier.gs_curStatus != SoldierDefines.SoldierStatus.ATTACKTATGET)
                    return;
                ((SharpShooter)_soldier).SharpShootShootChargeStrike(_curAttribute.p_flyDistance, _curAttribute.p_damage);
                _curAttribute.p_intervalCycle = _oriAttribute.p_intervalCycle;
            }
        }

#endregion
    }
}

