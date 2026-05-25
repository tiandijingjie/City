using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using WBD = WarBuildingDefines;
    using GD = GlobalDefines;

    //增加6%/20%燃烧伤害（4/1）                  26%/20%          51.2%
    public class CardDefenceFlameTowerFlameDamage : CardEffection<CardDefenceFlameTowerFlameDamage>
    {
        private DefenceConf _conf;
        public CardDefenceFlameTowerFlameDamage()
        {
            _category = CD.CardCategory.BUILDING;
            _levelCnt = new[] { 0, 0, 4, 1 };
        }

        public override bool CanBeInit()
        {
            if(_conf == null)
                _conf = _conf = (DefenceConf)WarBuildingCtrl.Instance.GetBdConf(WE.RaceType.Human, WBD.BuildingMode.DEFENCE,
                    (int)HumanDefines.DefenceType.FLAMETOWER);

            return _conf.IsContructable();
        }

        protected override bool OnTakeEffect(CardDefines.CardLevel level)
        {
            float value = 0;
            if (level == CD.CardLevel.NORMAL)
                value = 1 + 0.06f; //6%
            else if (level == CD.CardLevel.RARE)
                value = 1 + 0.2f; //20%
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }
            bool improveRet = WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WE.RaceType.Human, WBD.BuildingMode.DEFENCE, (int)
                HumanDefines.DefenceType.FLAMETOWER, "flameDamage", value, GD.CalDeltaType.MUL);
            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve failed");
                return false;
            }

            return true;
        }
    }
}
