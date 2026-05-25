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

    //增加暴击伤害倍数0.25/0.85（4/1）                                             1/0.85     1.85
    public class CardRangedSniperTalentDamageUp : CardEffection<CardRangedSniperTalentDamageUp>
    {
        public CardRangedSniperTalentDamageUp()
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
                value = 0.25f;
            else if(level == CD.CardLevel.RARE)
                value = 0.85f;
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            bool improveRet = SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, SD.SoldierImproveTarget.TALENT,
                WE.RaceType.Human, SD.TroopType.Ranged, (int)HumanDefines.RangedType.SHARPSHOOTER,
                (SniperIndividualData.IndividualDataType.TALENT,
                    (object)(SniperIndividualData.Talent.ParameterType.DAMGEUP, value, GD.CalDeltaType.ADD)), false);

            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve failed");
                return false;
            }

            return true;
        }
    }
}
