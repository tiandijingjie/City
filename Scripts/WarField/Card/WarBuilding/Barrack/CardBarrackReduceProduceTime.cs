using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using WBD = WarBuildingDefines;
    using GD = GlobalDefines;

    //兵营有30%的概率缩短下一轮士兵的11%/25%生产时间（4/1）                                                       0.37/0.25   0.5
    public class CardBarrackReduceProduceTime : CardEffection<CardBarrackReduceProduceTime>
    {
        public CardBarrackReduceProduceTime()
        {
            _category = CD.CardCategory.BUILDING;
            _levelCnt = new[] { 0, 0, 4, 1 };
        }

        public override bool CanBeInit()
        {
            return true;
        }

        protected override bool OnTakeEffect(CardDefines.CardLevel level)
        {
            float value = 0;
            if (level == CD.CardLevel.NORMAL)
                value = 1 - 0.11f;  //11%
            else if (level == CD.CardLevel.RARE)
                value = 1 - 0.26f; //7%
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            bool improveRet = WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WE.RaceType.Human, WBD.BuildingMode.BARRACK, (int)
                HumanDefines.BarrackType.MELEE, "spwawnTimeDown", value, GD.CalDeltaType.MUL);
            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} melee improve failed");
                return false;
            }

            improveRet = WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WE.RaceType.Human, WBD.BuildingMode.BARRACK, (int)
                HumanDefines.BarrackType.RANGED, "spwawnTimeDown", value, GD.CalDeltaType.MUL);
            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} ranged improve failed");
                return false;
            }

            improveRet = WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WE.RaceType.Human, WBD.BuildingMode.BARRACK, (int)
                HumanDefines.BarrackType.MAGIC, "spwawnTimeDown", value, GD.CalDeltaType.MUL);
            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} support improve failed");
                return false;
            }

            return true;
        }
    }
}

