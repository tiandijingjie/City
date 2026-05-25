using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Cannoneer;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //震慑
    public class CardRangedCannoneerSkillIntimidate : CardEffection<CardRangedCannoneerSkillIntimidate>
    {
        public CardRangedCannoneerSkillIntimidate()
        {
            _category = CD.CardCategory.RANGED;
            _levelCnt = new[] { 0, 2, 1, 1}; //CardDefines.CardLevel.MAX
        }

        //激活火炮手之后才能被初始化
        public override bool CanBeInit()
        {
            return SoldierCtrl.Instance.IsSoldierAvailable(WE.RaceType.Human, SD.TroopType.Ranged, (int)HumanDefines.RangedType.CANNONEER);
        }

        protected override bool OnTakeEffect(CardDefines.CardLevel level)
        {
            bool improveRet = SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, SD.SoldierImproveTarget.SKILL,
                WE.RaceType.Human, SD.TroopType.Ranged, (int)HumanDefines.RangedType.CANNONEER,
                (CannoneerIndividualData.IndividualDataType.INTIMIDATE, (object)null), false);

            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve failed");
                return false;
            }

            return true;
        }
    }
}
