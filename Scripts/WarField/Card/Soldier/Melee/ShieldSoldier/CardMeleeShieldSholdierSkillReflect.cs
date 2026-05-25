using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using ShieldSoldier;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;

    //反击
    public class CardMeleeShieldSholdierSkillReflect : CardEffection<CardMeleeShieldSholdierSkillReflect>
    {
        public CardMeleeShieldSholdierSkillReflect()
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
            bool improveRet = SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, SD.SoldierImproveTarget.SKILL,
                WE.RaceType.Human, SD.TroopType.Melee, (int)HumanDefines.MeleeType.SHIELDSOLDIER,
                (ShieldSoldierIndividualData.IndividualDataType.REFLECT, (object)null), false);

            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve failed");
                return false;
            }

            return true;
        }
    }
}
