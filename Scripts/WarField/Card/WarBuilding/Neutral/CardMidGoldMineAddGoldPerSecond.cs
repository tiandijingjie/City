using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using WBD = WarBuildingDefines;
    using GD = GlobalDefines;

    // 每个中级金矿每秒采矿数量增加3/8 (4/1)   12/8 20
    class CardMidGoldMineAddGoldPerSecond : CardEffection<CardMidGoldMineAddGoldPerSecond>
    {
        public CardMidGoldMineAddGoldPerSecond()
        {
            _category = CD.CardCategory.BUILDING;
            _levelCnt = new[] { 0, 0, 4, 1 };
        }

        public override bool CanBeInit()
        {
            bool found = false;
            //先检查地面的金矿
            if (WarBuildingCtrl.Instance.GetBuildingOnField(WE.RaceType.Neutral, WBD.BuildingMode.GOLDMINE, WE.OnGroundMapIndex, out var list) == true)
            {
                found = list.FindFirst(static mine =>
                {
                    if (mine.gs_subtype == (int)NeutralDefines.GoldMineType.MIDLEVEL)
                    {
                        if (((GoldMine)mine).gs_isOcuppied == true)
                            return true;
                    }

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
                        if (mine.gs_subtype == (int)NeutralDefines.GoldMineType.MIDLEVEL)
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
                value = 3;
            else if (level == CD.CardLevel.RARE)
                value = 8;
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            bool improveRet = WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WE.RaceType.Neutral, WBD.BuildingMode.GOLDMINE, (int)
                NeutralDefines.GoldMineType.MIDLEVEL, "goldAddPerSec", value, GD.CalDeltaType.ADD);
            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve failed");
                return false;
            }
            return true;
        }
    }
}


