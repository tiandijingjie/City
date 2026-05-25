using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //增加远程士兵8.5%/30%血量（4/1）                                           1.39/1.3   1.8
    public class CardRangedMaxHp : CardEffection<CardRangedMaxHp>
    {
        public CardRangedMaxHp()
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
                value = 1 + 0.085f; //8.5%
            else if (level == CD.CardLevel.RARE)
                value = 1 + 0.3f; //30%
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            List<SoldierConf> confs = SoldierCtrl.Instance.GetSoldiers(WE.RaceType.Human, SD.TroopType.Ranged);
            for (int i = 0; i < confs.Count; i++)
            {
                bool improveRet = SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, SoldierDefines.SoldierImproveTarget.STATE,
                    WarFieldElements.RaceType.Human, SD.TroopType.Ranged, confs[i].p_soldierType,
                    (SoldierDefines.StateSoldierEffectType.HEALTH, value, GD.CalDeltaType.MUL), false);
                if (improveRet == false)
                {
                    GameLogger.LogError($"{this.GetType().Name} improve soldier {confs[i].p_soldierType} failed");
                }
            }

            return true;
        }
    }
}
