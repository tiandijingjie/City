using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using WBD = WarBuildingDefines;
    using GD = GlobalDefines;

    //增加一个兵营同样的资源消耗生产双倍士兵10%/22%概率（4/1）*3                                       40/22    62
    public class CardMagicBarrackDoubleSpawn : CardEffection<CardMagicBarrackDoubleSpawn>
    {
        public CardMagicBarrackDoubleSpawn()
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
                value = 0.1f * 100;  //10%
            else if(level == CD.CardLevel.RARE)
                value = 0.22f * 100; //22%
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            bool improveRet = WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WE.RaceType.Human, WBD.BuildingMode.BARRACK, (int)
                HumanDefines.BarrackType.MAGIC, "doubleChance", value, GD.CalDeltaType.ADD);
            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve failed");
                return false;
            }

            return true;
        }
    }

}
