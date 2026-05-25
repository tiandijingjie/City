using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using WBD = WarBuildingDefines;
    using GD = GlobalDefines;

    // 增加1个激光（2/1）                           3
    public class CardDefenceLaserTowerAddLaser : CardEffection<CardDefenceLaserTowerAddLaser>
    {
        private DefenceConf _conf;
        public CardDefenceLaserTowerAddLaser()
        {
            _category = CD.CardCategory.BUILDING;
            _levelCnt = new[] { 0, 0, 2, 1 };
        }

        public override bool CanBeInit()
        {
            if(_conf == null)
                _conf = _conf = (DefenceConf)WarBuildingCtrl.Instance.GetBdConf(WE.RaceType.Human, WBD.BuildingMode.DEFENCE,
                    (int)HumanDefines.DefenceType.LASERTOWER);

            return _conf.IsContructable();
        }

        protected override bool OnTakeEffect(CardDefines.CardLevel level)
        {
            float value = 0;
            if (level == CD.CardLevel.NORMAL)
                value = 1;
            else if (level == CD.CardLevel.RARE)
                value = 1;
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }
            bool improveRet = WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WE.RaceType.Human, WBD.BuildingMode.DEFENCE, (int)
                HumanDefines.DefenceType.LASERTOWER, "laserCnt", value, GD.CalDeltaType.ADD);
            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve failed");
                return false;
            }

            return true;
        }
    }
}
