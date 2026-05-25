using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using SharpShooter;

namespace WarField
{
    using SKD = SkillDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using BFD = BuffDefines;
    using GD = GlobalDefines;

    //连击：普通攻击时有概率缩短接下来多次攻击的攻击间隔
    public class SharpShooterBarrageSkill : Skill
    {
#region public parameters

#endregion

#region private parameters

        private SharpShooterIndividualData.SkillBarrage _oriAttribute;
        private bool _duringTrigger;
        private (SD.StateSoldierEffectType, float, GD.CalDeltaType, float, string, BFD.BuffStrategy, object) _atkSpdObj;
#endregion

#region private parameters' get set
        public override uint gs_skillType
        {
            get { return (uint)SharpShooterIndividualData.IndividualDataType.BARRAGE; }
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
                _oriAttribute = new SharpShooterIndividualData.SkillBarrage((_soldier.gs_oriIndividualData as SharpShooterIndividualData)
                    .gs_individualItems[(int)SharpShooterIndividualData.IndividualDataType.BARRAGE]);
            }
            else
            {
                _oriAttribute.ReInit((_soldier.gs_oriIndividualData as SharpShooterIndividualData)
                    .gs_individualItems[(int)SharpShooterIndividualData.IndividualDataType.BARRAGE]);
            }
            _name = _oriAttribute.GetDescription().p_name;
            _duringTrigger = false;
            _atkSpdObj = (SD.StateSoldierEffectType.ATTACKSPEED, _oriAttribute.p_timeDown, GD.CalDeltaType.MUL, _oriAttribute.p_duration,
                "SharpShooterBarrageSkill", BFD.BuffStrategy.UNIQUE, (object)this);
            return true;
        }

        protected unsafe override void OnSkillDoAttackPost(float hit, bool isDead, object rivalScript, WarFieldElements.WarEleType rivalType)
        {
            if (_duringTrigger == false)
            {
                int chance = Utils.GetRandomInt();
                if (chance < _oriAttribute.p_chance)
                {
                    if (_soldier.BeAffectedByBuff(BFD.SoldierBuffType.STATE, in _atkSpdObj, BarrageCallBack) == true)
                    {
                        _duringTrigger = true;
                    }
                }
            }
        }

        protected unsafe void BarrageCallBack(BFD.BuffCallBackEventType type, void* value)
        {
            if (type == BFD.BuffCallBackEventType.FINISH)
            {
                _duringTrigger = false;
            }
        }
#endregion
    }
}

