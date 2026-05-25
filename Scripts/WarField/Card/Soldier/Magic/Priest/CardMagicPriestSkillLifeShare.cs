using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Priest;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //分享生命
    public class CardSupportPriestSkillLifeShare : CardEffection<CardSupportPriestSkillLifeShare>
    {
        public CardSupportPriestSkillLifeShare()
        {
            _category = CD.CardCategory.MAGIC;
            _levelCnt = new[] { 0, 2, 1, 1 }; //CardDefines.CardLevel.MAX
        }

        //激活牧师之后才能被初始化
        public override bool CanBeInit()
        {
            return SoldierCtrl.Instance.IsSoldierAvailable(WE.RaceType.Human, SD.TroopType.Magic, (int)HumanDefines.MagicType.PRIEST);
        }

        protected override bool OnTakeEffect(CardDefines.CardLevel level)
        {
            bool improveRet = SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, SD.SoldierImproveTarget.SKILL,
                WE.RaceType.Human, SD.TroopType.Magic, (int)HumanDefines.MagicType.PRIEST,
                (PriestIndividualData.IndividualDataType.LIFESHARE, (object)null), false);

            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve failed");
                return false;
            }

            return true;
        }
    }
}
