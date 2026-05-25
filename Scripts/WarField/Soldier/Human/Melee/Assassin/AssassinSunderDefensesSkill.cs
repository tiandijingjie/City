using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Assassin;

namespace WarField
{
    using SKD = SkillDefines;
    using WE = WarFieldElements;
    using BFD = BuffDefines;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //破甲：普通攻击可以永久降低敌人护甲和魔抗，不可叠加
    public class AssassinSunderDefence : Skill
    {
#region public parameters

#endregion

#region private parameters

        private AssassinIndividualData.SkillSunderDefenses _oriAttribute = null;

        private object _curTarget; //reduce the set buff times, not repeatedly set buff to the same enemy

#endregion

#region private parameters' get set
        public override uint gs_skillType
        {
            get { return (uint)AssassinIndividualData.IndividualDataType.SUNDERDEFENSES; }
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
                _oriAttribute = new AssassinIndividualData.SkillSunderDefenses((_soldier.gs_oriIndividualData as AssassinIndividualData)
                    .gs_individualItems[(int)AssassinIndividualData.IndividualDataType.SUNDERDEFENSES]);
            else
                _oriAttribute.ReInit((_soldier.gs_oriIndividualData as AssassinIndividualData)
                    .gs_individualItems[(int)AssassinIndividualData.IndividualDataType.SUNDERDEFENSES]);
            _name = _oriAttribute.GetDescription().p_name;
            _curTarget = null;
            return true;
        }

        protected override void OnSkillDoAttackPost(float hit, bool isDead, object rivalScript, WarFieldElements.WarEleType rivalType)
        {
            if(isDead == true)
                return;

            if (_curTarget != rivalScript)
            {
                if(rivalType != WE.WarEleType.SOLDIER)
                    return;

                Soldier sd = rivalScript as Soldier;
                _curTarget = rivalScript;
                //armor
                var value = (SD.StateSoldierEffectType.PHYARMOR, _oriAttribute.p_armorDown, GD.CalDeltaType.SUB, -1f, "AssassinSunderDefence",
                    BFD.BuffStrategy.IGNORE, (object)this);
                sd.BeAffectedByBuff(BFD.SoldierBuffType.STATE, in value);
            }
        }

#endregion
    }
}

