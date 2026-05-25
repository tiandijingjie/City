using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using ShieldSoldier;

namespace WarField
{
    using SKD = SkillDefines;
    using BFD = BuffDefines;

    //反击：反弹一定的伤害给非Boss单位攻击者,受到攻击时激活
    public class ShieldSoldierReflectSkill : Skill
    {
#region public parameters

#endregion

#region private parameters

        private ShieldSoldierIndividualData.SkillReflect _oriAttribute, _curAttribute;
        private (float, float, bool) _reflectionObj;
#endregion

#region private parameters' get set
        public override uint gs_skillType
        {
            get { return (uint)ShieldSoldierIndividualData.IndividualDataType.REFLECT; }
        }
#endregion

#region Unity callbacks
        private new void Awake()
        {
            base.Awake();
            _triggerType = new[] { SKD.SkillTriggerType.TIMETRIGGER, SKD.SkillTriggerType.BEATTACKTRIGGER };
        }
#endregion

#region public functions

#endregion

#region private functions
        protected override bool OnActiveSkill()
        {
            if (_oriAttribute == null)
            {
                _oriAttribute = new ShieldSoldierIndividualData.SkillReflect((_soldier.gs_oriIndividualData as ShieldSoldierIndividualData)
                    .gs_individualItems[(int)ShieldSoldierIndividualData.IndividualDataType.REFLECT]);
                _curAttribute = new ShieldSoldierIndividualData.SkillReflect(_oriAttribute);
            }
            else
            {
                _oriAttribute.ReInit((_soldier.gs_oriIndividualData as ShieldSoldierIndividualData)
                    .gs_individualItems[(int)ShieldSoldierIndividualData.IndividualDataType.REFLECT]);
                _curAttribute.ReInit(_oriAttribute);
            }
            _name = _oriAttribute.GetDescription().p_name;
            _curAttribute.p_reflectIntervalCycle = 0; //enable at begining
            _reflectionObj = (_curAttribute.p_reflectDuration, _curAttribute.p_reflectPercent, false);
            return true;
        }

        protected override void OnSkillUpdate()
        {
            if(_curAttribute.p_reflectIntervalCycle > 0)
            {
                _curAttribute.p_reflectIntervalCycle -= _timeStep;
            }
        }

        protected override float OnSkillBeAttackPre(float damage, object rival, WarFieldElements.WarEleType rivalType, bool isByPass)
        {
            if(isByPass == true) //不触发技能的伤害忽略
                return damage;

            if (_curAttribute.p_reflectIntervalCycle <= 0)
            {
                _curAttribute.p_reflectIntervalCycle = _oriAttribute.p_reflectIntervalCycle;
                _soldier.BeAffectedByBuff(BFD.SoldierBuffType.REFLECTION, in _reflectionObj);
            }
            return damage;
        }

#endregion
    }
}

