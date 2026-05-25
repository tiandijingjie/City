using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using WBD = WarBuildingDefines;
    using GD = GlobalDefines;

    //近战兵营     双倍概率超过40之后，增加兵营同样的资源消耗生产三倍士兵7%/18%概率（4/1） *3          28/18   46
    public class CardMagicBarrackTripleSpawn : CardEffection<CardMagicBarrackTripleSpawn>
    {
        private BarrackConf _conf;
        public CardMagicBarrackTripleSpawn()
        {
            _category = CD.CardCategory.BUILDING;
            _levelCnt = new[] { 0, 0, 4, 1 };
        }

        public override bool CanBeInit()
        {
            if (_conf == null)
                _conf = (BarrackConf)WarBuildingCtrl.Instance.GetBdConf(WE.RaceType.Human, WBD.BuildingMode.BARRACK,
                    (int)HumanDefines.BarrackType.MAGIC);
            if(_conf.gs_doubleChance >= 40) //双倍概率超过40
                return true;
            return false;
        }

        protected override bool OnTakeEffect(CardDefines.CardLevel level)
        {
            float value = 0;
            if (level == CD.CardLevel.NORMAL)
                value = 0.07f * 100;  //7%
            else if(level == CD.CardLevel.RARE)
                value = 0.18f * 100; //18%
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            bool improveRet = WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WE.RaceType.Human, WBD.BuildingMode.BARRACK, (int)
                HumanDefines.BarrackType.MAGIC, "tripleChance", value, GD.CalDeltaType.ADD);
            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve failed");
                return false;
            }

            return true;
        }
    }
}

