using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using WBD = WarBuildingDefines;
    using GD = GlobalDefines;

    //增加8%/22%的燃烧区域的持续时间 （4/1）        36%/22%          66%
    public class CardDefenceFlameTowerFlameDuration : CardEffection<CardDefenceFlameTowerFlameDuration>
    {
        private DefenceConf _conf;
        public CardDefenceFlameTowerFlameDuration()
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
                value = 1 + 0.08f; //8%
            else if (level == CD.CardLevel.RARE)
                value = 1 + 0.22f; //22%
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }
            bool improveRet = WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WE.RaceType.Human, WBD.BuildingMode.DEFENCE, (int)
                HumanDefines.DefenceType.FLAMETOWER, "flameDuration", value, GD.CalDeltaType.MUL);
            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve failed");
                return false;
            }

            return true;
        }
    }
}
