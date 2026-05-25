using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using WBD = WarBuildingDefines;
    using GD = GlobalDefines;

    // 出售金矿收益增加15%/36% (4/1)             75%/36%   138%
    class CardGoldMineSellPrice : CardEffection<CardGoldMineSellPrice>
    {
        public CardGoldMineSellPrice()
        {
            _category = CD.CardCategory.BUILDING;
            _levelCnt = new[] { 0, 0, 4, 1 };
        }

        public override bool CanBeInit() // 至少占有一个金矿
        {
            bool found = false;
            //先检查地面的金矿
            if (WarBuildingCtrl.Instance.GetBuildingOnField(WE.RaceType.Neutral, WBD.BuildingMode.GOLDMINE, WE.OnGroundMapIndex, out var list) == true)
            {
                found = list.FindFirst(static mine =>
                {
                    if (((GoldMine)mine).gs_isOcuppied == true)
                        return true;
                    return false;
                }, out var result);
            }

            if(found == true)
                return true;

            //检查洞穴地图中的金矿
            var mapDict = WarMapCtrl.Instance.gs_mapDict;
            foreach (var map in mapDict.Values)
            {
                byte mapId = map.gs_mapIndex;
                if (WarBuildingCtrl.Instance.GetBuildingOnField(WE.RaceType.Neutral, WBD.BuildingMode.GOLDMINE, mapId, out list) == true)
                {
                    found = list.FindFirst(static mine =>
                    {
                        if (((GoldMine)mine).gs_isOcuppied == true)
                            return true;
                        return false;
                    }, out var result);
                }

                if(found == true)
                    return true;
            }

            return false;
        }

        protected override bool OnTakeEffect(CardDefines.CardLevel level)
        {
            float value = 0;
            if (level == CD.CardLevel.NORMAL)
                value = 1 + 0.15f;
            else if (level == CD.CardLevel.RARE)
                value = 1 + 0.36f;
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            bool improveRet = WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WE.RaceType.Neutral, WBD.BuildingMode.GOLDMINE, (int)
                NeutralDefines.GoldMineType.LOWLEVEL, "sellPrice", value, GD.CalDeltaType.MUL);
            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve failed");
                return false;
            }

            improveRet = WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WE.RaceType.Neutral, WBD.BuildingMode.GOLDMINE, (int)
                NeutralDefines.GoldMineType.MIDLEVEL, "sellPrice", value, GD.CalDeltaType.MUL);
            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve failed");
                return false;
            }

            improveRet = WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WE.RaceType.Neutral, WBD.BuildingMode.GOLDMINE, (int)
                NeutralDefines.GoldMineType.HIGHLEVL, "sellPrice", value, GD.CalDeltaType.MUL);
            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve failed");
                return false;
            }
            return true;
        }
    }
}


