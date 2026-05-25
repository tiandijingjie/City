using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using HeroGeneral;

namespace WarField
{
    using SKD = SkillDefines;
    using BFD = BuffDefines;
    using SD = SoldierDefines;
    using GD = GlobalDefines;
    using WE = WarFieldElements;

    //英雄通用技能 加速奔跑
    public class HeroHasteSkill : Skill
    {
#region public parameters

#endregion

#region private parameters

        private HeroGenericIndividualData.SkillHaste _curAttribute;

        private float _intervalCycle;
        private (SD.StateSoldierEffectType, float, GD.CalDeltaType, float, string, BFD.BuffStrategy, object) _moveObj;

#endregion

#region private parameters' get set

        public override uint gs_skillType
        {
            get { return (uint)HeroGenericIndividualData.IndividualDataType.INVINCIBLE; }
        }

#endregion

#region Unity callbacks

        private new void Awake()
        {
            base.Awake();
            _triggerType = new[] { SKD.SkillTriggerType.TIMETRIGGER, SKD.SkillTriggerType.BEATTACKTRIGGER, SKD.SkillTriggerType.ACTIVETRIGGER };
        }

#endregion

#region public functions

#endregion

#region private functions

        protected override bool OnActiveSkill()
        {
            if (_curAttribute == null)
            {
                _curAttribute = (HeroGenericIndividualData.SkillHaste)((Hero)_soldier).gs_genericData.gs_individualItems[(int)
                    HeroGenericIndividualData.IndividualDataType.HASTE];
            }

            _name = _curAttribute.GetDescription().p_name;
            _intervalCycle = 0;
            _moveObj = (SD.StateSoldierEffectType.MOVESPEED, _curAttribute.p_moveUp, GD.CalDeltaType.MUL, _curAttribute.p_duration, "HasteSkill",
                BFD.BuffStrategy.OVERRIDE, (object)this);
            return true;
        }

        protected override void OnSkillUpdate()
        {
            if (_intervalCycle > 0)
            {
                _intervalCycle -= _timeStep;
            }
        }

        protected override void OnSkillActivatedTrigger()
        {
            if (_intervalCycle > 0)
            {
                GameLogger.LogDebug($"Skill {name} duplicated trigger!");
                return;
            }

            _intervalCycle = _curAttribute.p_intervalCycle;
            _soldier.BeAffectedByBuff(BFD.SoldierBuffType.STATE, in _moveObj);
        }

#endregion
    }
}
