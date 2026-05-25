using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Assassin;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //破甲技能
    public class CardMeleeAssassinSkillSunderDefenses : CardEffection<CardMeleeAssassinSkillSunderDefenses>
    {
        public CardMeleeAssassinSkillSunderDefenses()
        {
            _category = CD.CardCategory.MELEE;
            _levelCnt = new[] { 0, 2, 1, 1 };
        }

        //激活刺客之后才能被初始化
        public override bool CanBeInit()
        {
            return SoldierCtrl.Instance.IsSoldierAvailable(WE.RaceType.Human, SD.TroopType.Melee, (int)HumanDefines.MeleeType.ASSASSIN);
        }

        protected override bool OnTakeEffect(CardDefines.CardLevel level)
        {
            bool improveRet = SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, SD.SoldierImproveTarget.SKILL,
                WarFieldElements.RaceType.Human, SD.TroopType.Melee, (int)HumanDefines.MeleeType.ASSASSIN,
                (AssassinIndividualData.IndividualDataType.SUNDERDEFENSES, (object)null), false);

            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve failed");
                return false;
            }

            return true;
        }
    }
}

