using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using HeroGeneral;

namespace WarField
{
    using SKD = SkillDefines;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //双重伤害 普通攻击有一定概率造成双倍伤害 概率:12%
    public class HeroDoubleDamageTalent : Talent
    {
#region public parameters

#endregion

#region private parameters
        private HeroGenericIndividualData.TalentDoubleDamage _curTalent = null;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks
        protected override void Awake()
        {
            base.Awake();
            _triggerType = new[] { SKD.SkillTriggerType.ATTACKTRIGGER };
            for (int i = 0; i < _triggerType.Length; i++)
                _soldier.RegisterTalent(this, _triggerType[i]);  //talent need to do register during awake by itself
        }
#endregion

#region public functions

#endregion

#region private functions
        protected override bool OnActiveTalent()
        {
            if (_curTalent == null)
                _curTalent = (HeroGenericIndividualData.TalentDoubleDamage)(((Hero)_soldier).gs_genericData as HeroGenericIndividualData)
                    .gs_individualItems[(int)HeroGenericIndividualData.IndividualDataType.DOUBLEDAMAGE];
            _name = _curTalent.GetDescription().p_name;

            return true;
        }

        protected override bool OnTalentDoAttackPre(float hit, object rivalScript, WarFieldElements.WarEleType rivalType, out float damage)
        {
            damage = hit;
            int chance = Utils.GetRandomInt();
            if (chance < _curTalent.p_chance)
                damage = hit * 2;

            return true;
        }

#endregion
    }
}

