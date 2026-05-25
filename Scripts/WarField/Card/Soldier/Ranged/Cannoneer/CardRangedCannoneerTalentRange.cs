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

    //增加攻击的0.16/0.36覆盖范围（4/1）                                      0.64/0.36  1
    public class CardRangedCannoneerTalentRange : CardEffection<CardRangedCannoneerTalentRange>
    {
        public CardRangedCannoneerTalentRange()
        {
            _category = CD.CardCategory.RANGED;
            _levelCnt = new[] { 0, 0, 4, 1}; //CardDefines.CardLevel.MAX
        }

        //激活火炮手之后才能被初始化
        public override bool CanBeInit()
        {
            return SoldierCtrl.Instance.IsSoldierAvailable(WE.RaceType.Human, SD.TroopType.Ranged, (int)HumanDefines.RangedType.CANNONEER);
        }

        protected override bool OnTakeEffect(CardDefines.CardLevel level)
        {
            float value = 0;
            if (level == CD.CardLevel.NORMAL)
                value = 0.16f;
            else if (level == CD.CardLevel.RARE)
                value = 0.36f;
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            bool improveRet = SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, SD.SoldierImproveTarget.TALENT,
                WE.RaceType.Human, SD.TroopType.Ranged, (int)HumanDefines.RangedType.CANNONEER,
                (CannoneerIndividualData.IndividualDataType.TALENT,
                    (object)(CannoneerIndividualData.Talent.ParameterType.RANGE, value, GD.CalDeltaType.ADD)), false);

            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve failed");
                return false;
            }

            return true;
        }
    }
}
