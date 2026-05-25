using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using HeroGeneral;

namespace WarField
{
    using SKD = SkillDefines;
    using SD = SoldierDefines;
    using GD = GlobalDefines;
    using WE = WarFieldElements;

    //反转  有一定概率将受到的伤害转成自身血量  概率:8%
    public class HeroReversalTalent : Talent
    {
#region public parameters

#endregion

#region private parameters

        private HeroGenericIndividualData.TalentReversal _curTalent = null;

#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        protected override void Awake()
        {
            base.Awake();
            _triggerType = new[] { SKD.SkillTriggerType.BEATTACKTRIGGER };
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
                _curTalent = (HeroGenericIndividualData.TalentReversal)(((Hero)_soldier).gs_genericData as HeroGenericIndividualData)
                    .gs_individualItems[(int)HeroGenericIndividualData.IndividualDataType.REVERSAL];
            _name = _curTalent.GetDescription().p_name;

            return true;
        }

        //忽略bypass,所有伤害都可以触发
        protected override float OnTalentBeAttackPre(float damage, object rival, WarFieldElements.WarEleType rivalType, bool isByPass)
        {
            int chance = Utils.GetRandomInt();
            if (chance < _curTalent.p_chance)
            {
                float cure = (float)(damage * Utils.GetRandomInt()) / 100f;
                ((Soldier)rival).BeCure(null, cure, GD.CalDeltaType.ADD);
            }

            return damage;
        }

#endregion
    }
}
