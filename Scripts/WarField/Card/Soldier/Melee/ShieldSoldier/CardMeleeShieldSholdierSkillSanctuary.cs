using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using ShieldSoldier;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;

    //守护领域
    public class CardMeleeShieldSholdierSkillSanctuary : CardEffection<CardMeleeShieldSholdierSkillSanctuary>
    {
        public CardMeleeShieldSholdierSkillSanctuary()
        {
            _category = CD.CardCategory.MELEE;
            _levelCnt = new[] { 0, 2, 1, 1 };
        }

        //激活盾兵之后才能被初始化
        public override bool CanBeInit()
        {
            return SoldierCtrl.Instance.IsSoldierAvailable(WE.RaceType.Human, SD.TroopType.Melee, (int)HumanDefines.MeleeType.SHIELDSOLDIER);
        }

        protected override bool OnTakeEffect(CardDefines.CardLevel level)
        {
            bool improveRet = SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, SoldierDefines.SoldierImproveTarget.SKILL,
                WE.RaceType.Human, SoldierDefines.TroopType.Melee, (int)HumanDefines.MeleeType.SHIELDSOLDIER,
                (ShieldSoldierIndividualData.IndividualDataType.SANCTUARY, (object)null), false);

            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve failed");
                return false;
            }

            return true;
        }
    }
}
