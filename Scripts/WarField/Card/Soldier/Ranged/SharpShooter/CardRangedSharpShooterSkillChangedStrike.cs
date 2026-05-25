using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using SharpShooter;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;

    //蓄力击
    public class CardRangedSharpSholdierSkillChangedStrike : CardEffection<CardRangedSharpSholdierSkillChangedStrike>
    {
        public CardRangedSharpSholdierSkillChangedStrike()
        {
            _category = CD.CardCategory.RANGED;
            _levelCnt = new[] { 0, 2, 1, 1 };
        }

        //激活神射手之后才能被初始化
        public override bool CanBeInit()
        {
            return SoldierCtrl.Instance.IsSoldierAvailable(WE.RaceType.Human, SD.TroopType.Ranged, (int)HumanDefines.RangedType.SHARPSHOOTER);
        }

        protected override bool OnTakeEffect(CardDefines.CardLevel level)
        {
            bool improveRet = SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, SD.SoldierImproveTarget.SKILL,
                WE.RaceType.Human, SD.TroopType.Ranged, (int)HumanDefines.RangedType.SHARPSHOOTER,
                (SharpShooterIndividualData.IndividualDataType.CHARGEDSTRIKE, (object)null), false);

            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve failed");
                return false;
            }

            return true;
        }
    }
}
