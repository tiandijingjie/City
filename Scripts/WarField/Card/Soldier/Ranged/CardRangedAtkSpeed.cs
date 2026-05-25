using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //增加狙击手和火炮手6%/28%的攻击速度（4/1）                        1.26/1.28   1.62
    public class CardRangedAtkSpeed : CardEffection<CardRangedAtkSpeed>
    {
        public CardRangedAtkSpeed()
        {
            _category = CD.CardCategory.RANGED;
            _levelCnt = new[] { 0, 0, 4, 1}; //CardDefines.CardLevel.MAX
        }

        //激活任意一种远程高级兵可以被初始化
        public override bool CanBeInit()
        {
            var highLevelSd = SoldierCtrl.Instance.GetHighLevelTypeSoldiers(WE.RaceType.Human, SD.TroopType.Ranged);
            foreach (var sd in highLevelSd)
            {
                if(SoldierCtrl.Instance.IsSoldierAvailable(WE.RaceType.Human, SD.TroopType.Ranged, sd.p_soldierType) == true)
                    return true;
            }
            return false;
        }

        protected override bool OnTakeEffect(CardDefines.CardLevel level)
        {
            float value = 0;
            if (level == CD.CardLevel.NORMAL)
                value = 1 + 0.06f; //6%
            else if (level == CD.CardLevel.RARE)
                value = 1 + 0.28f; //28%
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            bool improveRet = SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, SoldierDefines.SoldierImproveTarget.STATE,
                WarFieldElements.RaceType.Human, SD.TroopType.Ranged, (int)HumanDefines.RangedType.SNIPER,
                (SoldierDefines.StateSoldierEffectType.ATTACKSPEED, value, GD.CalDeltaType.MUL), false);
            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve soldier sniper failed");
            }

            improveRet = SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, SoldierDefines.SoldierImproveTarget.STATE,
                WarFieldElements.RaceType.Human, SD.TroopType.Ranged, (int)HumanDefines.RangedType.CANNONEER,
                (SoldierDefines.StateSoldierEffectType.ATTACKSPEED, value, GD.CalDeltaType.MUL), false);
            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve soldier cannoneer failed");
            }

            return true;
        }
    }
}
