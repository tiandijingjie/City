using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using WBD = WarBuildingDefines;
    using GD = GlobalDefines;

    //每个低级金矿每秒采矿数量增加1/3 (4/1)   4/3   7
    class CardLowGoldMineAddGoldPerSecond : CardEffection<CardLowGoldMineAddGoldPerSecond>
    {
        public CardLowGoldMineAddGoldPerSecond()
        {
            _category = CD.CardCategory.BUILDING;
            _levelCnt = new[] { 0, 0, 4, 1 };
        }

        public override bool CanBeInit() //至少占据一个低级金矿
        {
            bool found = false;
            //先检查地面的金矿
            if (WarBuildingCtrl.Instance.GetBuildingOnField(WE.RaceType.Neutral, WBD.BuildingMode.GOLDMINE, WE.OnGroundMapIndex, out var list) == true)
            {
                found = list.FindFirst(static mine =>
                {
                    if (mine.gs_subtype == (int)NeutralDefines.GoldMineType.LOWLEVEL)
                    {
                        if (((GoldMine)mine).gs_isOcuppied == true)
                            return true;
                    }

                    return false;
                }, out var result);
            }

            if(found == true)
                return true;

            //检查地图中的金矿
            var mapDict = WarMapCtrl.Instance.gs_mapDict;
            foreach (var map in mapDict.Values)
            {
                byte mapId =map.gs_mapIndex;
                if (WarBuildingCtrl.Instance.GetBuildingOnField(WE.RaceType.Neutral, WBD.BuildingMode.GOLDMINE, mapId, out list) == true)
                {
                    found = list.FindFirst(static mine =>
                    {
                        if (mine.gs_subtype == (int)NeutralDefines.GoldMineType.LOWLEVEL)
                        {
                            if (((GoldMine)mine).gs_isOcuppied == true)
                                return true;
                        }

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
                value = 10;
            else if (level == CD.CardLevel.RARE)
                value = 3;
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            bool improveRet = WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WE.RaceType.Neutral, WBD.BuildingMode.GOLDMINE, (int)
                NeutralDefines.GoldMineType.LOWLEVEL, "goldAddPerSec", value, GD.CalDeltaType.ADD);
            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve failed");
                return false;
            }
            return true;
        }
    }
}


