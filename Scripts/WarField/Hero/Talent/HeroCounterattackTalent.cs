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

    //反击  受到近战攻击有一定概率给伤害者造成伤害,对建筑无效  概率:15%  伤害:45
    public class HeroCounterattackTalent : Talent
    {
#region public parameters

#endregion

#region private parameters

        private HeroGenericIndividualData.TalentCounterattack _curTalent = null;

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
                _curTalent = (HeroGenericIndividualData.TalentCounterattack)(((Hero)_soldier).gs_genericData as HeroGenericIndividualData)
                    .gs_individualItems[(int)HeroGenericIndividualData.IndividualDataType.COUNTERATTACK];
            _name = _curTalent.GetDescription().p_name;

            return true;
        }

        protected override float OnTalentBeAttackPre(float damage, object rival, WarFieldElements.WarEleType rivalType, bool isByPass)
        {
            if(isByPass == true) //不触发技能
                return damage;

            if(rivalType != WE.WarEleType.SOLDIER)
                return damage;

            int chance = Utils.GetRandomInt();
            if (chance < _curTalent.p_chance)
                ((Soldier)rival).BeAttacked(null, null, WE.WarEleType.SOLDIER, _curTalent.p_damage, false, false, out float
                    oriValue);
            return damage;
        }

#endregion
    }
}
