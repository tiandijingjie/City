using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //减小狙击手3.5%/10%的攻击距离（4/1）                                    13.3%/10%   22%
    public class CardRangedSniperAttackRange : CardEffection<CardRangedSniperAttackRange>
    {
        public CardRangedSniperAttackRange()
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
                value = 1 - 0.035f; //3.5%
            else if (level == CD.CardLevel.RARE)
                value = 1 - 0.1f; //10%
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            bool improveRet = SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, SoldierDefines.SoldierImproveTarget.STATE,
                WarFieldElements.RaceType.Human, SD.TroopType.Ranged, (int)HumanDefines.RangedType.SNIPER,
                (SoldierDefines.StateSoldierEffectType.ATTACKRANGE, value, GD.CalDeltaType.MUL), false);
            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve soldier sniper failed");
            }

            return true;
        }
    }
}
