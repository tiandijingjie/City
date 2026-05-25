
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace WarField
{
    using WE = WarFieldElements;

    public class WarFieldUtil
    {
        //通过Y坐标计算出相应的z的坐标
        //通过z实现前后的遮挡关系
        //mapBaseY： 所在地图的下边界的y，防止y的绝对值过大 (计算方式决定了map的高度不能超过1000，因为camera只能拍摄1000以内的z)
        static public float GetZByY(float y, float mapBaseY)
        {
            int z = (int)((y - mapBaseY) * 100);
            return z * 0.01f; //限制z的最小距离是0.01
        }

        static public WE.WarEleType GetWarEleType(string tag)
        {
            WE.WarEleType ret = WE.WarEleType.MIN;
            switch(tag)
            {
                case "FriendlyBuilding":
                case "EnemyBuilding":
                    ret = WE.WarEleType.BUILDING;
                    break;
                case "FriendlySoldier":
                case "EnemySoldier":
                    ret = WE.WarEleType.SOLDIER;
                    break;
                case "FriendlyWeapon":
                case "EnemyWeapon":
                    ret = WE.WarEleType.WEAPON;
                    break;
                default:
                    break;
            }
            return ret;
        }

        static public WE.FactionType GetFactionByTag(string tag)
        {
            WE.FactionType ret = WE.FactionType.MIN;
            switch(tag)
            {
                case "FriendlyBuilding":
                case "FriendlySoldier":
                case "FriendlyWeapon":
                    ret = WE.FactionType.FRIENDLY;
                    break;
                case "EnemyBuilding":
                case "EnemySoldier":
                case "EnemyWeapon":
                    ret = WE.FactionType.ENEMY;
                    break;
                case "NeutralBuilding":
                case "NeutralSoldier":
                    ret = WE.FactionType.NEUTRAL;
                    break;
                default:
                    break;
            }
            return ret;
        }

        static public WE.FactionType GetFactionByRace(WE.RaceType race)
        {
            if (race == WE.RaceType.Human)
                return WE.FactionType.FRIENDLY;
            else if (race == WE.RaceType.Neutral)
                return WE.FactionType.NEUTRAL;
            else
                return WE.FactionType.ENEMY;
        }

        static public WE.WarEleType GetTypeByTag(string tag)
        {
            WE.WarEleType ret = WE.WarEleType.MIN;
            switch (tag)
            {
                case "FriendlyBuilding":
                case "EnemyBuilding":
                    ret = WE.WarEleType.BUILDING;
                    break;
                case "FriendlySoldier":
                case "EnemySoldier":
                    ret = WE.WarEleType.SOLDIER;
                    break;
                case "FriendlyWeapon":
                case "EnemyWeapon":
                    ret = WE.WarEleType.WEAPON;
                    break;
                default:
                    break;
            }

            return ret;
        }

        static public LayerMask GetLaymaskByType(WE.FactionType faction, WE.WarEleType type)
        {
            LayerMask ret = -1;
            if (faction == WE.FactionType.FRIENDLY)
            {
                if (type == WE.WarEleType.BUILDING)
                    ret = 1 << 6;
                else if (type == WE.WarEleType.SOLDIER)
                    ret = 1 << 7;
            }
            else if(faction == WE.FactionType.ENEMY)
            {
                if (type == WE.WarEleType.BUILDING)
                    ret = 1 << 8;
                else if (type == WE.WarEleType.SOLDIER)
                    ret = 1 << 9;
            }
            return ret;
        }

        //get objects in a circular area
        static public List<GameObject> GetObjectsInRegion(float radius, Vector2 center, LayerMask mask)
        {
            List<GameObject> objectsInRegion = new List<GameObject>();
            if (SearchManager.Instance == null || WarMapCtrl.Instance == null)
                return objectsInRegion;

            SearchArea searcher = new SearchArea(0, (targets) =>
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    if (targets[i] is Component comp)
                        objectsInRegion.Add(comp.gameObject);
                }
            }, () =>
            {
                return new SearchShapeDef
                {
                    p_shapeType = SearchDefines.SearchShapeType.CIRCLE,
                    p_centerOrStartPos = new float2(center.x, center.y),
                    p_radius = radius,
                    p_radiusSq = radius * radius,
                };
            }, null, WarMapCtrl.Instance.gs_curMapId);

            int enemySoldierMask = LayerMask.GetMask("EnemySoldierBody");
            int enemyBuildingMask = LayerMask.GetMask("EnemyBuildingBody");
            int friendlySoldierMask = LayerMask.GetMask("FriendlySoldierBody");
            int friendlyBuildingMask = LayerMask.GetMask("FriendlyBuildingBody");

            if ((mask.value & enemySoldierMask) != 0 || (mask.value & enemyBuildingMask) != 0)
            {
                if ((mask.value & enemySoldierMask) != 0)
                    SearchConditionUtil.AddEnemySoldierConditions(searcher);
                if ((mask.value & enemyBuildingMask) != 0)
                    SearchConditionUtil.AddEnemyBuildingCondition(searcher);
            }
            if ((mask.value & friendlySoldierMask) != 0 || (mask.value & friendlyBuildingMask) != 0)
            {
                if ((mask.value & friendlySoldierMask) != 0)
                    SearchConditionUtil.AddFriendlySoldierCondition(searcher);
                if ((mask.value & friendlyBuildingMask) != 0)
                    SearchConditionUtil.AddFriendlyBuildingCondition(searcher);
            }

            if (searcher.p_conditions.Count > 0)
                SearchManager.Instance.RegisterSearch(searcher);

            return objectsInRegion;
        }

        //get faction,
        //soldier: _wfId = $"{_warEleType}_{_faction}_{_sdName}_{WE.GetSdIndex(_faction)}";
        //building: _wfId = $"{_warEleType}_{_bdConf.gs_faction}_{_bdConf.gs_name}_{WE.GetBdIndex()}";
        //weapon: _wfId = $"{_warEleType}";
        static public bool GetObjInfoByWfId(string wfId, out WE.WarEleType eleType, out WE.FactionType faction)
        {
            eleType = WE.WarEleType.MIN;
            faction = WE.FactionType.MIN;

            string[] tags = wfId.Split('_');
            if(tags.Length == 0)
                return false;
            switch (tags[0])
            {
                case "BUILDING":
                    eleType = WE.WarEleType.BUILDING;
                    faction = Enum.Parse<WE.FactionType>(tags[1], true);
                    break;
                case "SOLDIER":
                    eleType = WE.WarEleType.SOLDIER;
                    faction = Enum.Parse<WE.FactionType>(tags[1], true);
                    break;
                case "WEAPON":
                    eleType = WE.WarEleType.WEAPON;
                    break;
                default:
                    return false;
            }
            return true;
        }
    }
}

