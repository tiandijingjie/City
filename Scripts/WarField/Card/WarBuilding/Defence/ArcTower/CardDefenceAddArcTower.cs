using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using WBD = WarBuildingDefines;
    using GD = GlobalDefines;

    //获取闪电塔
    public class CardDefenceAddArcTower : CardEffection<CardDefenceAddArcTower>
    {
        public CardDefenceAddArcTower()
        {
            _category = CD.CardCategory.BUILDING;
            _levelCnt = new[] { 0, 1, 0, 0}; //CardDefines.CardLevel.MAX
        }

        public override bool CanBeInit()
        {
            return true;
        }

        protected override bool OnTakeEffect(CardDefines.CardLevel level)
        {
            bool improveRet = WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WE.RaceType.Human, WBD.BuildingMode.DEFENCE, (int)
                HumanDefines.DefenceType.ARCTOWER, "enabled", 0, GD.CalDeltaType.MIN);

            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve failed");
                return false;
            }

            return true;
        }
    }
}

