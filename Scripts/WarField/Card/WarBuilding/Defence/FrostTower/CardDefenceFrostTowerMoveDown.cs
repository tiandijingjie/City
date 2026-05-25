using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using WBD = WarBuildingDefines;
    using GD = GlobalDefines;

    //受到诅咒士兵移动速度下降增加16%/45%（4/1）         81%/45%    263%
    public class CardDefenceFrostTowerMoveDown : CardEffection<CardDefenceFrostTowerMoveDown>
    {
        private DefenceConf _conf;
        public CardDefenceFrostTowerMoveDown()
        {
            _category = CD.CardCategory.BUILDING;
            _levelCnt = new[] { 0, 0, 4, 1 };
        }

        public override bool CanBeInit()
        {
            if(_conf == null)
                _conf = _conf = (DefenceConf)WarBuildingCtrl.Instance.GetBdConf(WE.RaceType.Human, WBD.BuildingMode.DEFENCE,
                    (int)HumanDefines.DefenceType.FROSTTOWER);

            return _conf.IsContructable();
        }

        protected override bool OnTakeEffect(CardDefines.CardLevel level)
        {
            float value = 0;
            if (level == CD.CardLevel.NORMAL)
                value = 1 + 0.16f; //16%
            else if (level == CD.CardLevel.RARE)
                value = 1 + 0.45f; //45%
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }
            bool improveRet = WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WE.RaceType.Human, WBD.BuildingMode.DEFENCE, (int)
                HumanDefines.DefenceType.FROSTTOWER, "moveDown", value, GD.CalDeltaType.MUL);
            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve failed");
                return false;
            }

            return true;
        }
    }
}
