using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using HeroGeneral;

namespace WarField
{
    using SKD = SkillDefines;
    using BFD = BuffDefines;
    using ED = EffectDefines;

    //英雄通用技能 无敌
    public class HeroInvincibleSkill : Skill
    {
#region public parameters

#endregion

#region private parameters

        private HeroGenericIndividualData.SkillInvincible _curAttribute;

        private float _intervalCycle;
        private HeroSkillEffectInvincible _effect;

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
                _curAttribute =
                    (HeroGenericIndividualData.SkillInvincible)((Hero)_soldier).gs_genericData.gs_individualItems[
                        (int)HeroGenericIndividualData.IndividualDataType.INVINCIBLE];
            }
            _name = _curAttribute.GetDescription().p_name;
            _intervalCycle = 0;
            return true;
        }

        protected override void OnSkillUpdate()
        {
            if (_intervalCycle > 0)
            {
                _intervalCycle -= _timeStep;
            }
        }

        protected unsafe override void OnSkillActivatedTrigger()
        {
            if (_intervalCycle > 0)
            {
                GameLogger.LogDebug($"Skill {name} duplicated trigger!");
                return;
            }

            _intervalCycle = _curAttribute.p_intervalCycle;
            var value = (BFD.ShieldBuffType.NORMAL, _curAttribute.p_duration, -1.0f);
            _soldier.BeAffectedByBuff(BFD.SoldierBuffType.SHIELD, in value, InvicibleFinishCallBack);
            _effect = (HeroSkillEffectInvincible)EffectCtrl.Instance.AddEffectAt(_soldier.gs_transform.position+ new Vector3(0, 0.8f, 0),  ED
                .EffectType.HEROINVINCIBLE, _soldier.gs_mapId);
            if (_effect != null)
            {
                _effect.EffectFollow(_soldier.gs_transform, new Vector3(0, 0.8f, 0));
            }
        }

        protected unsafe void InvicibleFinishCallBack(BFD.BuffCallBackEventType type, void* value)
        {
            if(_effect != null)
                EffectCtrl.Instance.ReleaseEffect(_effect, ED.EffectType.HEROINVINCIBLE, true);
        }

#endregion
    }
}
