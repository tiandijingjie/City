using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using WBD = WarBuildingDefines;
    using GD = GlobalDefines;

    //有20%的概率下个士兵生产金币消耗减少20%/50% （4/1）                       0.6/0.5  0.8
    public class CardBarrackReduceProducePrice : CardEffection<CardBarrackReduceProducePrice>
    {
        public CardBarrackReduceProducePrice()
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
                value = 1 - 0.2f;  //20%
            else if (level == CD.CardLevel.RARE)
                value = 1 - 0.5f; //50%
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            bool improveRet = WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WE.RaceType.Human, WBD.BuildingMode.BARRACK, (int)
                HumanDefines.BarrackType.MELEE, "spawnPriceDown", value, GD.CalDeltaType.MUL);
            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} melee improve failed");
                return false;
            }

            improveRet = WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WE.RaceType.Human, WBD.BuildingMode.BARRACK, (int)
                HumanDefines.BarrackType.RANGED, "spawnPriceDown", value, GD.CalDeltaType.MUL);
            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} ranged improve failed");
                return false;
            }

            improveRet = WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WE.RaceType.Human, WBD.BuildingMode.BARRACK, (int)
                HumanDefines.BarrackType.MAGIC, "spawnPriceDown", value, GD.CalDeltaType.MUL);
            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} support improve failed");
                return false;
            }

            return true;
        }
    }
}
