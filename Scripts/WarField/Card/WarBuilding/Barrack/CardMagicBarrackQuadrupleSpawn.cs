using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using WBD = WarBuildingDefines;
    using GD = GlobalDefines;

    //三倍概率超过20之后，增加同样的资源消耗四倍士兵概率3/7（4/1）*3                              12/7      19
    public class CardMagicBarrackQuadrupleSpawn : CardEffection<CardMagicBarrackQuadrupleSpawn>
    {
        private BarrackConf _conf;
        public CardMagicBarrackQuadrupleSpawn()
        {
            _category = CD.CardCategory.BUILDING;
            _levelCnt = new[] { 0, 0, 4, 1 };
        }

        public override bool CanBeInit()
        {
            if (_conf == null)
                _conf = (BarrackConf)WarBuildingCtrl.Instance.GetBdConf(WE.RaceType.Human, WBD.BuildingMode.BARRACK, (int)HumanDefines.BarrackType.MAGIC);
            if(_conf.gs_tripleChance >= 20) //三倍概率超过20
                return true;
            return false;
        }

        protected override bool OnTakeEffect(CardDefines.CardLevel level)
        {
            float value = 0;
            if (level == CD.CardLevel.NORMAL)
                value = 0.03f * 100;  //3%
            else if(level == CD.CardLevel.RARE)
                value = 0.07f * 100; //7%
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            bool improveRet = WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WE.RaceType.Human, WBD.BuildingMode.BARRACK, (int)
                HumanDefines.BarrackType.MAGIC, "quadrupleChance", value, GD.CalDeltaType.ADD);
            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve failed");
                return false;
            }

            return true;
        }
    }
}

