using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Sniper;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //增加3%/10%暴击概率（4/1）                                                       12/10        22
    public class CardRangedSniperTalentChance : CardEffection<CardRangedSniperTalentChance>
    {
        public CardRangedSniperTalentChance()
        {
            _category = CD.CardCategory.RANGED;
            _levelCnt = new[] { 0, 0, 4, 1}; //CardDefines.CardLevel.MAX
        }

        //激活神射手之后才能被初始化
        public override bool CanBeInit()
        {
            return SoldierCtrl.Instance.IsSoldierAvailable(WE.RaceType.Human, SD.TroopType.Ranged, (int)HumanDefines.RangedType.SNIPER);
        }

        protected override bool OnTakeEffect(CardDefines.CardLevel level)
        {
            float value = 0;
            if (level == CD.CardLevel.NORMAL)
                value = 0.03f * 100; //3%
            else if(level == CD.CardLevel.RARE)
                value = 0.1f * 100; //10%
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            bool improveRet = SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, SD.SoldierImproveTarget.TALENT,
                WE.RaceType.Human, SD.TroopType.Ranged, (int)HumanDefines.RangedType.SHARPSHOOTER,
                (SniperIndividualData.IndividualDataType.TALENT,
                    (object)(SniperIndividualData.Talent.ParameterType.CHANCE, value, GD.CalDeltaType.ADD)), false);

            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve failed");
                return false;
            }

            return true;
        }
    }
}
