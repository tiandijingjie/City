using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Sniper;

namespace WarField
{
    using SKD = SkillDefines;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //爆头：每击杀一个敌人有概率增加一定攻击力
    public class SniperSkillHeadShot : Skill
    {
#region public parameters

#endregion

#region private parameters

        private SniperIndividualData.SkillHeadShot _oriAttribute;
        private object _damageIncIndexer;

#endregion

#region private parameters' get set
        public override uint gs_skillType
        {
            get { return (uint)SniperIndividualData.IndividualDataType.HEADSHOT; }
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
                _oriAttribute = new SniperIndividualData.SkillHeadShot((_soldier.gs_oriIndividualData as SniperIndividualData)
                    .gs_individualItems[(int)SniperIndividualData.IndividualDataType.HEADSHOT]);
            }
            else
            {
                _oriAttribute.ReInit((_soldier.gs_oriIndividualData as SniperIndividualData)
                    .gs_individualItems[(int)SniperIndividualData.IndividualDataType.HEADSHOT]);
            }
            _name = _oriAttribute.GetDescription().p_name;
            _damageIncIndexer = null;
            return true;
        }

        protected override void OnSkillDoAttackPost(float hit, bool isDead, object rivalScript, WarFieldElements.WarEleType rivalType)
        {
            //building not take in account
            if (rivalType != WarFieldElements.WarEleType.SOLDIER)
                return;

            if (isDead == true)
            {
                int chance = Utils.GetRandomInt();
                if (chance < _oriAttribute.p_chance)
                {
                    if (_damageIncIndexer == null)
                        _damageIncIndexer = _soldier.AddStateChange(SD.StateSoldierEffectType.DAMAGE, _oriAttribute.p_damageInc, GD.CalDeltaType.ADD,
                            out float oriValue);
                    else
                        _soldier.ModifyStateChange(SD.StateSoldierEffectType.DAMAGE, _damageIncIndexer, false, _oriAttribute.p_damageInc,
                            GD.CalDeltaType.ADD, false);  //在之前的基础上再次增加
                }
            }
        }

        protected override void OnDeactiveSkill()
        {
            _damageIncIndexer = null; //just let GC recycle it
        }

#endregion
    }
}

