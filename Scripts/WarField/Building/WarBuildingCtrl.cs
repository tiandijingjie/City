using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Xml;
using UnityEngine;

namespace WarField
{
    using WBD = WarBuildingDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    public class WarBuildingCtrl : MonoBehaviour
    {
#region public parameters

        static public WarBuildingCtrl Instance;

#endregion

#region private parameters
        private GameObject[,][] _buildingPfbs; //[(int)WE.RaceType.MAX, (int)WBD.BuildingMode.MAX][e.g. HumanDefines.BarrackType]

        //all the buildings on the field  <mapid, [(int)WE.RaceType.MAX, (int)WBD.BuildingMode.MAX]>
        private Dictionary<byte, AsyncDataPool<WarBuilding>[,]> _warBuildingsOnField;

        private List<WarBuilding>[,] _warBuildingPool; //[(int)WE.RaceType.MAX, (int)WBD.BuildingMode.MAX]

        //the confs read from file, the confs define in the level map xml, not add into this
        //List里面存放的数据是无序的，在查找的时候需要遍历，因为建筑的添加在游戏中是一个很缓慢发生的过程
        private List<BuildingConf>[,] _buildingConfs; //[WE.RaceType, WBD.BuildingMode];

        private bool _friendlyBarrackOrdered = false; //因为己方兵营在_warBuildingsOnField需要按照一定顺序排列，_friendlyBarrackOrdered表示是否排序
        private float _destroyEnemyBdGetGold; //摧毁敌人建筑获得的金币奖励
        private bool _beInited;
        private bool _canWork;

#endregion

#region private parameters' get set

        public List<BuildingConf>[,] gs_buildingConfs
        {
            get { return _buildingConfs; }
        }
#endregion

#region Unity callbacks

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _destroyEnemyBdGetGold = 0;
            _buildingPfbs = new GameObject[(int)WE.RaceType.MAX, (int)WBD.BuildingMode.MAX][];
            _warBuildingsOnField = new Dictionary<byte, AsyncDataPool<WarBuilding>[,]>();
            _warBuildingPool = new List<WarBuilding>[(int)WE.RaceType.MAX, (int)WBD.BuildingMode.MAX];
            for (int i = 1; i < (int)WE.RaceType.MAX; i++)
            {
                var faction = WarFieldUtil.GetFactionByRace((WE.RaceType)i);

                for (int j = 1; j < (int)WBD.BuildingMode.MAX; j++)
                {
                    Type typeDefine = null;
                    switch ((WE.RaceType)i)
                    {
                        case WE.RaceType.Human:
                            typeDefine = typeof(HumanDefines);
                            break;
                        case WE.RaceType.Oculars:
                            typeDefine = typeof(OcularsDefines);
                            break;
                        case WE.RaceType.Neutral:
                            typeDefine = typeof(NeutralDefines);
                            break;
                        default:
                            break;
                    }

                    if (typeDefine == null)
                    {
                        GameLogger.LogWarning($"Can not find the race {(WE.RaceType)i}");
                        continue;
                    }

                    int maxValue = 0;
                    switch ((WBD.BuildingMode)j)
                    {
                        case WBD.BuildingMode.FORTRESS:
                        {
                            if (faction == WE.FactionType.FRIENDLY)
                            {
                                Type typeEnum = typeDefine.GetNestedType("FortressType");
                                if (typeEnum != null)
                                {
                                    var maxField = typeEnum.GetField("MAX");
                                    if (maxField != null)
                                        maxValue = (int)maxField.GetValue(null);
                                }
                            }
                            else if (faction == WE.FactionType.ENEMY)
                                continue; //enemy not has fortress, one map has a enemy forteress, but it belong to no race
                        }
                            break;
                        case WBD.BuildingMode.BARRACK:
                        {
                            Type typeEnum = typeDefine.GetNestedType("BarrackType");
                            if (typeEnum != null)
                            {
                                var maxField = typeEnum.GetField("MAX");
                                if (maxField != null)
                                    maxValue = (int)maxField.GetValue(null);
                            }
                        }
                            break;
                        case WBD.BuildingMode.DEFENCE:
                        {
                            Type typeEnum = typeDefine.GetNestedType("DefenceType");
                            if (typeEnum != null)
                            {
                                var maxField = typeEnum.GetField("MAX");
                                if (maxField != null)
                                    maxValue = (int)maxField.GetValue(null);
                            }
                        }
                            break;
                        case WBD.BuildingMode.PORTAL:
                        {
                            Type typeEnum = typeDefine.GetNestedType("PortalType");
                            if (typeEnum != null)
                            {
                                var maxField = typeEnum.GetField("MAX");
                                if (maxField != null)
                                    maxValue = (int)maxField.GetValue(null);
                            }
                        }
                            break;
                        case WBD.BuildingMode.GOLDMINE:
                        {
                            Type typeEnum = typeDefine.GetNestedType("GoldMineType");
                            if (typeEnum != null)
                            {
                                var maxField = typeEnum.GetField("MAX");
                                if (maxField != null)
                                    maxValue = (int)maxField.GetValue(null);
                            }
                        }
                            break;
                        case WBD.BuildingMode.GEMMINE:
                        {
                            Type typeEnum = typeDefine.GetNestedType("GemMineType");
                            if (typeEnum != null)
                            {
                                var maxField = typeEnum.GetField("MAX");
                                if (maxField != null)
                                    maxValue = (int)maxField.GetValue(null);
                            }
                        }
                            break;
                        case WBD.BuildingMode.CAVE:
                        {
                            Type typeEnum = typeDefine.GetNestedType("CaveType");
                            if (typeEnum != null)
                            {
                                var maxField = typeEnum.GetField("MAX");
                                if (maxField != null)
                                    maxValue = (int)maxField.GetValue(null);
                            }
                        }
                            break;
                        case WBD.BuildingMode.PROPBD:
                        {
                            Type typeEnum = typeDefine.GetNestedType("PropBdType");
                            if (typeEnum != null)
                            {
                                var maxField = typeEnum.GetField("MAX");
                                if (maxField != null)
                                    maxValue = (int)maxField.GetValue(null);
                            }
                        }
                            break;
                        default:
                            break;
                    }

                    if (maxValue == 0)
                    {
//                        GameLogger.LogWarning($"{(WE.RaceType)i} {(WBD.BuildingMode)j} fail to get build type max");
                        continue;
                    }

                    _warBuildingPool[i, j] = new List<WarBuilding>();
                    _buildingPfbs[i, j] = new GameObject[maxValue];
                }
            }

            InitBuildingConf();

            LoadWarBuildingPrefabs();

            _beInited = false;
            _canWork = false;
        }

        private void OnDestroy()
        {
            foreach (var tmp in _warBuildingsOnField)
            {
                var arr = tmp.Value;
                for (int i = 1; i < (int)WE.RaceType.MAX; i++)
                {
                    for (int j = 1; j < (int)WBD.BuildingMode.MAX; j++)
                    {
                        var pool = arr[i, j];
                        pool?.Dispose();
                    }
                }
            }
        }

#endregion

#region public functions

        public bool InitWarBuildingCtrl()
        {
            _beInited = true;
            return true;
        }

        //显示/隐藏 建筑的覆盖范围
        public void SetDefenceCoverRange(bool value, byte mapId, WE.RaceType race, WBD.BuildingMode mode, int subType = -1)
        {
            AsyncDataPool<WarBuilding> list;
            if(_warBuildingsOnField.TryGetValue(mapId, out var pool) == false)
            {
                GameLogger.LogError($"Fail to get war buildings on map {mapId}");
                return;
            }

            if (subType == -1)
            {
                list = pool[(int)race, (int)mode];
                if (list != null)
                {
                    list.ForEachReadOnly(static (bd, v) =>
                    {
                        bd.SetCoverRange(v);
                    },value);
                }
            }
            else
            {
                list = pool[(int)race, (int)mode];
                var stateValue = (value, subType);
                if (list != null)
                {
                    list.ForEachReadOnly(static (bd, state) =>
                    {
                        if(bd.gs_subtype == state.subType)
                            bd.SetCoverRange(state.value);
                    },stateValue);
                }
            }
        }

        //will call InitBuilding in this function
        //在创建地图时调用,游戏运行过程中不允许调用
        //Ttype是otherValue的泛型
        public WarBuilding AddBuildingDuringMapCreate<Ttype>(BuildingConf bdConf, Vector2 pos, Ttype otherValue, byte mapId)
        {
            if (bdConf == null)
            {
                GameLogger.LogError($"Can not add building at {pos} as conf is null");
                return null;
            }

            WarBuilding bd = null;
            //不init建筑，之后统一初始化
            if (bdConf.gs_faction == WE.FactionType.FRIENDLY)
                bd = AddFriendlyBuilding(bdConf, pos, mapId);
            else if (bdConf.gs_faction == WE.FactionType.ENEMY)
                bd = AddEnemyBuilding(bdConf, pos, otherValue, mapId);
            else if (bdConf.gs_faction == WE.FactionType.NEUTRAL)
                bd = AddNeutralBuilding(bdConf, pos, mapId);
            if (ReferenceEquals(bd, null) == false)
                bdConf.AddSubscriber(bd);
            return bd;
        }

        //will call InitBuilding in this function
        //在创建地图时调用,游戏运行过程中不允许调用,
        //通过这种方式创建的建筑的conf都不是标准conf,在_buildingConfs中没有
        public WarBuilding AddBuildingDuringMapCreate<Ttype>(XmlNode confNode, Vector2 pos, Ttype otherValue, byte mapId)
        {
            if (_beInited == false)
                return null;

            BuildingConf bdConf = ConvertXmlToBuildingConf(confNode, out bool isNew);
            if (isNew == true) //如果并没有创建新的conf, 不需要改Attribute
            {
                if (_destroyEnemyBdGetGold > 0) //摧毁获取的金币
                    bdConf.ChangeAttribute("destroyFee", _destroyEnemyBdGetGold, GD.CalDeltaType.ADD, out float valueAfterCal, out float oriValue);

                WE.RaceType race = bdConf.gs_race;
                WBD.BuildingMode mode = bdConf.gs_mode;
                if (_buildingConfs[(int)race, (int)mode] == null)
                    _buildingConfs[(int)race, (int)mode] = new List<BuildingConf>();
                _buildingConfs[(int)race, (int)mode].Add(bdConf); //加入_buildingConfs统一管理
            }

            return AddBuildingDuringMapCreate(bdConf, pos, otherValue, mapId);
        }

        //游戏运行过程中动态添加建筑
        public WarBuilding AddBuildingDuringRunning<Ttype>(BuildingConf bdConf, Vector2 pos, Ttype otherValue, byte mapId)
        {
            return OnAddBuilding(bdConf, pos, otherValue, mapId);
        }

        public void RemoveBuilding(WarBuilding bdScript, WE.RaceType race, WBD.BuildingMode mode, int subType, byte mapId)
        {
            if (race == WE.RaceType.Human)
            {
                if (mode == WBD.BuildingMode.BARRACK || mode == WBD.BuildingMode.FORTRESS)
                {
                    GameLogger.LogError($"Forbid to remove human {mode} !!!");
                    return;
                }
            }

            if(_warBuildingsOnField.TryGetValue(mapId, out var arr) == false)
            {
                GameLogger.LogError($"Fail to get war buildings on map {mapId}");
                return;
            }
            var pool = arr?[(int)race, (int)mode];
            if (pool != null)
                pool.RemoveItemAsync(bdScript);

            var conf = GetBdConf(race, mode, subType);
            if (conf != null) //对于敌方建筑是查询不到conf的
                conf.RemoveSubscriber(bdScript);
        }

        public void StartWork()
        {
            if (_beInited == false)
                return;

            //将随着地图创建出来的建筑设置map，同时与minimap绑定
            foreach (var tmp in _warBuildingsOnField)
            {
                byte mapId = tmp.Key;
                var arr = tmp.Value;
                for (WE.RaceType i = WE.RaceType.MIN; i < WE.RaceType.MAX; i++)
                {
                    for (WBD.BuildingMode j = WBD.BuildingMode.MIN; j < WBD.BuildingMode.MAX; j++)
                    {
                        var pool = arr[(int)i, (int)j];
                        if (pool != null)
                        {
                            pool.ForEachAndFlush(static (bd, id) =>
                            {
                                bd.ChangeMapId(id);
                            }, mapId, false);
                        }
                    }
                }
            }

            foreach (var tmp in _warBuildingsOnField)
            {
                int mapId = tmp.Key;
                var poolsInMap = tmp.Value;
                for (int i = 1; i < (int)WE.RaceType.MAX; i++)
                {
                    for (int j = 1; j < (int)WBD.BuildingMode.MAX; j++)
                    {
                        var pool = poolsInMap[i, j];
                        if(pool == null)
                            continue;
                        pool.ForEachReadOnly(static bd => bd.StartWork());
                    }
                }
            }

            _canWork = true;
            return;
        }

        //己方的barrack不会真的删除，所以顺序不会变
        public FriendlyBarrack GetFriendlyBarrackByTroop(SD.TroopType troopType)
        {
            int index = (int)troopType - 1;
            var pool = _warBuildingsOnField[WE.OnGroundMapIndex][(int)WE.RaceType.Human, (int)WBD.BuildingMode.BARRACK];
            if (pool == null)
                return null;

            if (pool.Count == 3) //所有的barrack都添加
            {
                if (_friendlyBarrackOrdered == false) //按照gs_subtype从小到大排序
                {
                    int n = pool.Count;
                    for (int i = 0; i < n - 1; i++)
                    {
                        for (int j = 0; j < n - 1 - i; j++)
                        {
                            if (pool.GetByIndex(j).gs_subtype > pool.GetByIndex(j + 1).gs_subtype)
                                pool.SwapElements(j, j + 1);
                        }
                    }

                    _friendlyBarrackOrdered = true;
                }
                //只有地面有己方Barrack
                return (FriendlyBarrack)pool.GetByIndex(index);
            }
            else //barrck还没有全部添加
            {
                pool.FindFirst(static (bd, type) =>
                {
                    if (bd.gs_bdConf.gs_subType == (int)type)
                        return true;
                    return false;
                }, troopType, out var result);
                return (FriendlyBarrack)result;
            }
        }

        public bool GetBuildingOnField(WE.RaceType raceType, WBD.BuildingMode bdMode, byte mapId, out AsyncDataPool<WarBuilding> list)
        {
            list = null;
            if (_warBuildingsOnField.TryGetValue(mapId, out var arr) == true)
            {
                list = arr[(int)raceType, (int)bdMode];
                return list != null;
            }

            return false;
        }

        public GameObject GetWarBuildingPfb(WE.RaceType raceType, WBD.BuildingMode bdMode, int bdType)
        {
            return _buildingPfbs[(int)raceType, (int)bdMode][bdType];
        }

        public BuildingConf GetBdConf(WE.RaceType raceType, WBD.BuildingMode bdMode, int subType)
        {
            List<BuildingConf> list = _buildingConfs[(int)raceType, (int)bdMode];
            if (list == null)
                return null;
            int count = list.Count;
            for (int i = 0; i < count; i++)
            {
                if (list[i].gs_subType == subType)
                    return list[i];
            }

            return null;
        }

        public bool ApplyImprovement(WE.ImproveSrc src, WE.RaceType race, WBD.BuildingMode mode, int subtype, string improveName, float improveValue,
            GD.CalDeltaType improveCal)
        {
            if (src == WE.ImproveSrc.FROMUPGRADE || src == WE.ImproveSrc.FROMCARD || src == WE.ImproveSrc.FROMOTHERSOURCE)
            {
                var conf = GetBdConf(race, mode, subtype);
                if (conf == null)
                {
                    GameLogger.LogError($"Failed to find improve buiding conf {race} {mode} {subtype}");
                    return false;
                }
                if (conf.ChangeAttribute(improveName, improveValue, improveCal, out float valueAfterCal, out float oriValue) == false)
                {
                    GameLogger.LogError($"Failed to apply improvement for {race} {mode} {subtype} detail: {improveName}");
                    return false;
                }
                GameLogger.LogInfo($"Improve building success {race} {mode} {subtype} detail: {improveName} {improveValue} {improveCal} from {src}");
                return true;
            }
            else if (src == WE.ImproveSrc.FROMPROP)
            {
                return true;
            }

            GameLogger.LogError($"Unknown improve source {src}");
            return false;
        }

        //upgrade the targetBd upgrade to the type of upgradeConf
        public bool BuildingUpgrade(WarBuilding targetBd, BuildingConf upgradeConf)
        {
            targetBd.gameObject.SetActive(false); //先禁用，这样就不会跟新添加的建筑发生碰撞
            WarBuilding bd = OnAddBuilding(upgradeConf, targetBd.gs_transform.position, (object)null, targetBd.gs_mapId);
            if (ReferenceEquals(bd, null) == true)
                return false;
            targetBd.DoUpgrade(bd);
            return true;
        }

        //摧毁敌人建筑之后获得金币
        public void SetDestroyEnemyBdGetGold(float value, GD.CalDeltaType calType)
        {
            _destroyEnemyBdGetGold = Utils.CalDeltaValue(_destroyEnemyBdGetGold, value, calType);
            for (int i = 1; i < (int)WE.RaceType.MAX; i++)
            {
                if(WarFieldUtil.GetFactionByRace((WE.RaceType)i) != WE.FactionType.ENEMY)
                    continue;
                for (int j = 1; j < (int)WBD.BuildingMode.MAX; j++)
                {
                    var list = _buildingConfs[i, j];
                    if(list == null)
                        continue;

                    for (int k = 0; k < list.Count; k++)
                    {
                        if (list[k].ChangeAttribute("destroyFee", value, calType, out float valueAfterCal, out float oriValue) == false)
                            GameLogger.LogError($"Set enemy building destroy gold failed {list[k].gs_race} {list[k].gs_mode} {list[k].gs_subType}");
                    }
                }
            }
        }

        //当从map添加的建筑完成之后，需要强制flush一次，保证这些建筑可以查询到
        public void ForceBuildingsPoolFlush()
        {
            foreach (var mapPools in _warBuildingsOnField.Values)
            {
                for (int i = 1; i < (int)WE.RaceType.MAX; i++)
                {
                    for (int j = 1; j < (int)WBD.BuildingMode.MAX; j++)
                    {
                        mapPools[i, j]?.ForEachAndFlush(null);
                    }
                }
            }
        }
#endregion

#region private functions

        //will call InitBuilding in this function
        private WarBuilding OnAddBuilding<Ttype>(BuildingConf bdConf, Vector2 pos, Ttype otherValue, byte mapId)
        {
            if (bdConf == null)
            {
                GameLogger.LogError($"Can not add building at {pos} as conf is null");
                return null;
            }

            WarBuilding bd = null;
            if (bdConf.gs_faction == WE.FactionType.FRIENDLY)
                bd = AddFriendlyBuilding(bdConf, pos, mapId);
            else if (bdConf.gs_faction == WE.FactionType.ENEMY)
                bd = AddEnemyBuilding(bdConf, pos, otherValue, mapId);
            else if (bdConf.gs_faction == WE.FactionType.NEUTRAL)
                bd = AddNeutralBuilding(bdConf, pos, mapId);
            if (bd != null)
            {
                bdConf.AddSubscriber(bd);
                if (_canWork == true)
                    bd.StartWork();
            }

            return bd;
        }

        //不判断是否能够建造,是在UI选取的时候判断
        private WarBuilding AddFriendlyBuilding(BuildingConf bdConf, Vector2 pos, byte mapId)
        {
            WarBuilding ret = null;
            WBD.BuildingMode bdMode = bdConf.gs_mode;
            GameObject[] array = _buildingPfbs[(int)WE.RaceType.Human, (int)bdMode];
            if (bdMode == WBD.BuildingMode.FORTRESS)
            {
                HumanDefines.FortressType subType = (HumanDefines.FortressType)bdConf.gs_subType;
                GetBuildingOnField(WE.RaceType.Human, bdMode, mapId, out var list);
                if (list.IsNullOrEmpty() == false)
                {
                    GameLogger.LogError("Already has a Main Fortress,Can not add a new one");
                    return null;
                }
                try
                {
                    MainFortress bd = (MainFortress)GetBuildingFromPool(bdConf.gs_race, bdConf.gs_mode, bdConf.gs_subType);
                    if (bd == null)
                        bd = Instantiate(array[(int)subType], pos, Quaternion.identity, transform).GetComponent<MainFortress>();
                    else
                        bd.gs_transform.position = pos;
                    bd.InitBuilding(bdConf, mapId);
                    ret = bd;
                }
                catch (Exception e)
                {
                    GameLogger.LogException($"Fail to add friendly building {bdMode} {subType}", e);
                    return null;
                }
            }
            else if (bdMode == WBD.BuildingMode.BARRACK)
            {
                HumanDefines.BarrackType subType = (HumanDefines.BarrackType)bdConf.gs_subType;
                if (GetFriendlyBarrackByTroop((SD.TroopType)subType) != null)
                {
                    GameLogger.LogError($"Alread has a Friendly Barrack {bdMode} {subType},Can not add a new one");
                    return null;
                }

                try
                {
                    FriendlyBarrack bd = (FriendlyBarrack)GetBuildingFromPool(bdConf.gs_race, bdConf.gs_mode, bdConf.gs_subType);
                    if (bd == null)
                        bd = Instantiate(array[(int)subType], pos, Quaternion.identity, transform).GetComponent<FriendlyBarrack>();
                    else
                        bd.gs_transform.position = pos;
                    bd.InitBuilding(bdConf, mapId);
                    ret = bd;
                }
                catch (Exception e)
                {
                    GameLogger.LogException($"Fail to add friendly building {bdMode} {subType}", e);
                    return null;
                }
            }
            else if (bdMode == WBD.BuildingMode.DEFENCE)
            {
                HumanDefines.DefenceType subType = (HumanDefines.DefenceType)bdConf.gs_subType;
                try
                {
                    DefenceBuilding bd = (DefenceBuilding)GetBuildingFromPool(bdConf.gs_race, bdConf.gs_mode, bdConf.gs_subType);
                    if (bd == null)
                        bd = Instantiate(array[(int)subType], pos, Quaternion.identity, transform).GetComponent<DefenceBuilding>();
                    else
                        bd.gs_transform.position = pos;
                    bd.InitBuilding(bdConf, mapId);
                    ret = bd;
                }
                catch (Exception e)
                {
                    GameLogger.LogException($"Fail to add friendly building {bdMode} {subType}", e);
                    return null;
                }
            }
            else if (bdMode == WBD.BuildingMode.PROPBD)
            {
                HumanDefines.PropBdType subType = (HumanDefines.PropBdType)bdConf.gs_subType;
                try
                {
                    PropBaseBuilding bd = (PropBaseBuilding)GetBuildingFromPool(bdConf.gs_race, bdConf.gs_mode, bdConf.gs_subType);
                    if (bd == null)
                        bd = Instantiate(array[(int)subType], pos, Quaternion.identity, transform).GetComponent<PropBaseBuilding>();
                    else
                        bd.gs_transform.position = pos;
                    bd.InitBuilding(bdConf, mapId);
                    ret = bd;
                }
                catch (Exception e)
                {
                    GameLogger.LogException($"Fail to add friendly building {bdMode} {subType}", e);
                    return null;
                }
            }
            else
            {
                GameLogger.LogError($"Try to add unknown Friendly Building Mode: {bdMode}");
                return null;
            }

            AddItemIntoOnFieldPool(ret, bdConf.gs_race, bdConf.gs_mode, mapId);
            return ret;
        }

        private WarBuilding AddNeutralBuilding(BuildingConf bdConf, Vector2 pos, byte mapId)
        {
            WarBuilding ret = null;
            WBD.BuildingMode bdMode = bdConf.gs_mode;
            GameObject[] array = _buildingPfbs[(int)WE.RaceType.Neutral, (int)bdMode];

            int subType = bdConf.gs_subType;
            try
            {
                WarBuilding bd = GetBuildingFromPool(bdConf.gs_race, bdConf.gs_mode, bdConf.gs_subType);
                if (ReferenceEquals(bd, null) == true)
                    bd = Instantiate(array[(int)subType], pos, Quaternion.identity, transform).GetComponent<WarBuilding>();
                else
                    bd.gs_transform.position = pos;
                if (bd == null)
                {
                    GameLogger.LogError($"Fail to add neutral building {bdMode} {subType}");
                    return null;
                }

                switch (bdMode)
                {
                    case WBD.BuildingMode.PORTAL:
                        ((Portal)bd).InitBuilding(bdConf, mapId);
                        break;
                    case WBD.BuildingMode.GOLDMINE:
                        ((GoldMine)bd).InitBuilding(bdConf, mapId);
                        break;
                    case WBD.BuildingMode.GEMMINE:
                        ((GemMine)bd).InitBuilding(bdConf, mapId);
                        break;
                    case WBD.BuildingMode.CAVE:
                        ((CaveTran)bd).InitBuilding(bdConf, mapId);
                        break;
                    default:
                        break;
                }

                ret = bd;
            }
            catch (Exception e)
            {
                GameLogger.LogException($"Fail to add neutral building {bdMode} {subType}", e);
                return null;
            }

            AddItemIntoOnFieldPool(ret, bdConf.gs_race, bdConf.gs_mode, mapId);
            return ret;
        }

        private WarBuilding AddEnemyBuilding<Ttype>(BuildingConf bdConf, Vector2 pos, Ttype otherValue, byte mapId)
        {
            WarBuilding ret = null;
            WE.RaceType race = bdConf.gs_race;
            WBD.BuildingMode bdMode = bdConf.gs_mode;
            GameObject[] array = _buildingPfbs[(int)race, (int)bdMode];
            Type typeDefine = null;
            switch (race) //not inlcude human as for enemy
            {
                case WE.RaceType.Oculars:
                    typeDefine = typeof(OcularsDefines);
                    break;
                default:
                    GameLogger.LogError($"Can not add enemy building with race {race}");
                    return null;
            }

            Type typeEnum = null;
            switch (bdMode)
            {
                case WBD.BuildingMode.FORTRESS:
                    typeEnum = typeDefine.GetNestedType("FortressType");
                    break;
                case WBD.BuildingMode.BARRACK:
                    typeEnum = typeDefine.GetNestedType("BarrackType");
                    break;
                case WBD.BuildingMode.DEFENCE:
                    typeEnum = typeDefine.GetNestedType("DefenceType");
                    break;
                default:
                    GameLogger.LogError($"Can not add enemy building with mode {bdMode}");
                    return null;
            }

            if (typeEnum == null)
            {
                GameLogger.LogError($"Can not add enemy building with mode {bdMode}");
                return null;
            }

            if (bdMode == WBD.BuildingMode.BARRACK)
            {
                WBD.BarrackTriggerStage defaultStage = otherValue is WBD.BarrackTriggerStage s? s : WBD.BarrackTriggerStage.MIN;
                if (defaultStage == WBD.BarrackTriggerStage.MIN)
                {
                    GameLogger.LogError("Error defaultStage");
                    return null;
                }
                try
                {
                    int intSubType = bdConf.gs_subType;
                    EnemyBarrack bd = (EnemyBarrack)GetBuildingFromPool(bdConf.gs_race, bdConf.gs_mode, bdConf.gs_subType);
                    if (ReferenceEquals(bd, null) == true)
                        bd = Instantiate(array[intSubType], pos, Quaternion.identity, transform).GetComponent<EnemyBarrack>();
                    else
                        bd.gs_transform.position = pos;

                    if (bd == null)
                    {
                        GameLogger.LogError($"Fail to add enemy building {bdMode} {pos}");
                        return null;
                    }
                    bd.InitBuilding(bdConf, defaultStage, mapId);
                    ret = bd;
                }
                catch (Exception e)
                {
                    GameLogger.LogException($"Fail to add enemy building {bdMode} at {pos}", e);
                    return null;
                }
            }
            else if (bdMode == WBD.BuildingMode.DEFENCE)
            {

            }
            else
            {
                GameLogger.LogError($"Try to add unknown Enemy Building Mode: {bdMode}");
                return null;
            }

            AddItemIntoOnFieldPool(ret, bdConf.gs_race, bdConf.gs_mode, mapId);
            return ret;
        }

        private WarBuilding GetBuildingFromPool(WE.RaceType raceType, WBD.BuildingMode bdMode, int subType)
        {
            List<WarBuilding> list = _warBuildingPool[(int)raceType, (int)bdMode];
            if (list == null)
                return null;
            int cnt = list.Count;
            if (cnt == 0)
                return null;

            for (int i = cnt - 1; i >= 0; i--)
            {
                if (list[i].gs_bdConf.gs_subType == subType)
                {
                    WarBuilding ret = list[i];
                    list.RemoveAt(list.Count - 1);
                    return ret;
                }
            }

            return null;
        }

        private void ReleaseBuildingIntoPool(WarBuilding bd)
        {
            var conf = bd.gs_bdConf;
            List<WarBuilding> list = _warBuildingPool[(int)conf.gs_race, (int)conf.gs_mode];
            if (list == null)
            {
                list = new List<WarBuilding>();
                _warBuildingPool[(int)conf.gs_race, (int)conf.gs_mode] = list;
            }
            list.Add(bd);
        }

        private void LoadWarBuildingPrefabs()
        {
            //not got through the Neutral folder
            for (int i = 1; i < (int)WE.RaceType.MAX; i++)
            {
                StringBuilder path = new StringBuilder("Prefabs/WarBuilding/");
                path.Append(((WE.RaceType)i).ToString() + "/");
                for (int j = 1; j < (int)WBD.BuildingMode.MAX; j++)
                {
                    GameObject[] prefabs = Resources.LoadAll<GameObject>(path.ToString() + ((WBD.BuildingMode)j).ToString());
                    if (prefabs.Length == 0) //not has prefabs or not has this folder
                        continue;
                    Type scriptType = typeof(WarBuilding);
                    foreach (var prefab in prefabs)
                    {
                        Component scriptComponent = prefab.GetComponent(scriptType);
                        if (scriptComponent == null) //有些prefab并不是建筑（e.g. Laser）
                            continue;
                        Type actualType = scriptComponent.GetType();
                        if (scriptComponent != null)
                        {
                            PropertyInfo property = actualType.GetProperty("gs_subtype");
                            if (property != null)
                            {
                                object bdType = property.GetValue(scriptComponent);
                                if ((int)bdType == 0)
                                {
                                    GameLogger.LogError($"Load warbuilding prefab {prefab.name}  type {bdType} error");
                                    continue;
                                }
                                else
                                    GameLogger.LogInfo($"Load warbuilding prefab {prefab.name} {bdType}");

                                _buildingPfbs[i, j][(int)bdType] = prefab;
                            }
                            else
                            {
                                GameLogger.LogError($"Can not get building type");
                                continue;
                            }

                            //check race/mode/faction are set for the prefab
                            property = actualType.GetProperty("gs_faction");
                            if (property != null)
                            {
                                object value = property.GetValue(scriptComponent);
                                if ((int)value == 0)
                                    GameLogger.LogError($"Load warbuilding prefab {prefab.name} faction {value} {(int)value} error");
                            }

                            property = actualType.GetProperty("gs_race");
                            if (property != null)
                            {
                                object value = property.GetValue(scriptComponent);
                                if ((int)value != i)
                                    GameLogger.LogError($"Load warbuilding prefab {prefab.name} race {value} {(int)value}!={i} error");
                            }

                            property = actualType.GetProperty("gs_bdMode");
                            if (property != null)
                            {
                                object value = property.GetValue(scriptComponent);
                                if ((int)value != j)
                                    GameLogger.LogError($"Load warbuilding prefab {prefab.name} mode {value} {(int)value}!={j} error");
                            }
                        }
                    }
                }
            }
        }

        private void InitBuildingConf()
        {
            _buildingConfs = new List<BuildingConf>[(int)WE.RaceType.MAX, (int)WBD.BuildingMode.MAX];
            StringBuilder path = new StringBuilder("Conf/Building/");
            TextAsset[] xmlFiles = Resources.LoadAll<TextAsset>(path.ToString());
            for (int i = 0; i < xmlFiles.Length; i++)
            {
                XmlDocument confXML = new XmlDocument();
                confXML.LoadXml(xmlFiles[i].text);
                XmlNode confNode = confXML.DocumentElement;
                WE.RaceType race = Enum.Parse<WE.RaceType>(((XmlElement)confNode).GetAttribute("race"), true);
                WBD.BuildingMode mode = Enum.Parse<WarBuildingDefines.BuildingMode>(((XmlElement)confNode).GetAttribute("mode"), true);

                BuildingConf conf = ConvertXmlToBuildingConf(confNode, out bool isNew);
                if (conf == null || isNew == false)
                {
                    GameLogger.LogError($"Fail to read building conf {xmlFiles[i].name}");
                    continue;
                }
                if (_buildingConfs[(int)race, (int)mode] == null)
                    _buildingConfs[(int)race, (int)mode] = new List<BuildingConf>();
                _buildingConfs[(int)race, (int)mode].Add(conf);
            }
        }

        //isNew: 是否创建了新的BuildingConf, false:返回一个已有的conf,没有创建新的
        private BuildingConf ConvertXmlToBuildingConf(XmlNode confNode, out bool isNew)
        {
            isNew = true;
            WE.RaceType race = Enum.Parse<WE.RaceType>(((XmlElement)confNode).GetAttribute("race"), true); //忽略大小写
            WBD.BuildingMode mode = Enum.Parse<WarBuildingDefines.BuildingMode>(((XmlElement)confNode).GetAttribute("mode"), true);
            int subType = int.Parse(((XmlElement)confNode).GetAttribute("subtype"));

            BuildingConf conf = GetBdConf(race, mode, subType); //在level map中定义的xml可能是已经添加过的建筑类型
            if (conf != null)
            {
                isNew = false;
                return conf;
            }

            switch (mode)
            {
                case WBD.BuildingMode.FORTRESS:
                    conf = new FortressConf(confNode);
                    break;
                case WBD.BuildingMode.BARRACK:
                    conf = new BarrackConf(confNode);
                    break;
                case WBD.BuildingMode.DEFENCE:
                    conf = new DefenceConf(confNode);
                    break;
                case WBD.BuildingMode.PORTAL:
                    conf = new PortalConf(confNode);
                    break;
                case WBD.BuildingMode.GOLDMINE:
                    conf = new GoldMineConf(confNode);
                    break;
                case WBD.BuildingMode.GEMMINE:
                    conf = new GemMineConf(confNode);
                    break;
                case WBD.BuildingMode.CAVE:
                    conf = new CaveConf(confNode);
                    break;
                case WBD.BuildingMode.PROPBD:
                    conf = new PropBdConf(confNode);
                    break;
                default:
                    break;
            }

            if (confNode == null)
                GameLogger.LogError($"Fail to init building conf");
            return conf;
        }

        private void AddItemIntoOnFieldPool(WarBuilding item, WE.RaceType race, WBD.BuildingMode mode, byte mapId)
        {
            if (_warBuildingsOnField.TryGetValue(mapId, out var poolsInMap) == false)
            {
                poolsInMap = new AsyncDataPool<WarBuilding>[(int)WE.RaceType.MAX, (int)WBD.BuildingMode.MAX];
                _warBuildingsOnField.Add(mapId, poolsInMap);
            }

            var pool = poolsInMap[(int)race, (int)mode];
            if (pool == null)
            {
                pool = new AsyncDataPool<WarBuilding>();
                pool.RegisterOnItemRemoved(ReleaseBuildingIntoPool);
                poolsInMap[(int)race, (int)mode] = pool;
            }
            pool.AddItemAsync(item);
        }
#endregion
    }
}

