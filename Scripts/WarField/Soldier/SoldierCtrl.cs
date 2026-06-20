using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Xml;
using UnityEngine;

using Assassin;
using ShieldSoldier;
using Swordsman;

using SharpShooter;
using Sniper;
using Cannoneer;

using Priest;
using Shaman;

using HeroGeneral;
using MeleeHero;
using RangedHero;
using MagicHero;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;
using UnityEngine.Rendering;
using WarField.Anim;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WarField
{
    using SD = SoldierDefines;
    using WE = WarFieldElements;
    using GD = GlobalDefines;

    public class SoldierCtrl : MonoBehaviour
    {
#region public parameters

        static public SoldierCtrl Instance;

#endregion

#region private parameters
        //collect all of the data about one kind of soldier
        private class SoldierBasicData
        {
            public bool p_isUnlocked; //由upgrade解锁
            public bool p_isAvailable; //由card激活
            public SoldierConf p_conf; //conf of state
            public IndividualData p_individual; //private data different from soldier to soldier
            public GameObject p_prefab; //soldier prefab

            public SoldierBasicData()
            {
                p_isUnlocked = false;
                p_isAvailable = false;
                p_conf = null;
                p_individual = null;
                p_prefab = null;
            }
        }

        // 处理一个地图中一个race的移动job
        private struct SoldierMoveBatch
        {
            public byte p_mapId;
            public int p_raceType;
            public AsyncDataPoolForTransform<Soldier> p_soldiers;
            public int p_cmdOffset;
            public int p_count;
        }

        //record all the data of a soldier type
        private SoldierBasicData[,][] _soldierData; //[WE.RaceType.MAX, SD.TroopType.MAX][e.g. WE.RaceType.Human.MeleeType]

        //record all the data of a Hero
        private SoldierBasicData[] _heroData; //[HumanDefines.HeroType]
        private HeroGenericIndividualData _heroGenericData;

        // all the soldiers in every map field, key is the mapId <mapId, soldiers[race]>  记录每个地图中每个种族的active士兵
        private Dictionary<byte,AsyncDataPoolForTransform<Soldier>[]> _sdOnField;
        private Dictionary<int, DataPool<GameObject>>[,] _sdPool; //没有使用的士兵的内存池  [WE.RaceType.MAX, SD.TroopType.MAX] <sd_subtype>

        //hero
        private Hero _curHero = null;

        //notify
        private DataPool<ISoldierProductionNotify>[,] _sdProdNotifyList; //[faction, troop]
        private DataPool<ISoldierDieNotify>[,] _sdDieNotifyList; // [faction, troop]

        //solider move job
        private NativeArray<SoldierMoveCmd> _moveCmds;  //每次调用都将所有士兵的移动命令写在这个数组中,每个job只读取一段,保证了jobs之间的并行执行
        private int _moveJobCapacity;  //_moveCmds最大容量
        private JobHandle _moveJobHandle = default;
        private bool _moveJobScheduled = false;
        private int _moveCmdIndex;

        private List<SoldierMoveBatch> _moveJobBatches;

        private bool _canWork;
        private bool _beInited;
#endregion

#region private parameters' get set

        public Hero gs_curHero
        {
            get { return _curHero; }
        }

        public JobHandle gs_moveJobHandle
        {
            get { return _moveJobHandle; }
        }

#endregion

#region Unity callbacks

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(Instance);
                return;
            }

            Instance = this;
            _beInited = false;
            _canWork = false;

            _soldierData = new SoldierBasicData[(int)WE.RaceType.MAX, (int)SD.TroopType.MAX][];
            for (int i = 1; i < (int)WE.RaceType.MAX; i++)
            {
                for (int j = 1; j < (int)SD.TroopType.MAX; j++)
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
                    switch ((SD.TroopType)j)
                    {
                        case SD.TroopType.Melee:
                        {
                            Type typeEnum = typeDefine.GetNestedType("MeleeType");
                            if (typeEnum != null)
                            {
                                var maxField = typeEnum.GetField("MAX");
                                if (maxField != null)
                                    maxValue = (int)maxField.GetValue(null);
                            }
                        }
                            break;
                        case SD.TroopType.Ranged:
                        {
                            Type typeEnum = typeDefine.GetNestedType("RangedType");
                            if (typeEnum != null)
                            {
                                var maxField = typeEnum.GetField("MAX");
                                if (maxField != null)
                                    maxValue = (int)maxField.GetValue(null);
                            }
                        }
                            break;
                        case SD.TroopType.Magic:
                        {
                            Type typeEnum = typeDefine.GetNestedType("MagicType");
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
                        GameLogger.LogError($"{(WE.RaceType)i} {(SD.TroopType)j} fail to get soldier type max");
                        continue;
                    }

                    _soldierData[i, j] = new SoldierBasicData[maxValue];
                    for (int m = 1; m < maxValue; m++)
                        _soldierData[i, j][m] = new SoldierBasicData();
                }
            }

            _heroData = new SoldierBasicData[(int)HumanDefines.HeroType.MAX];
            for (int m = 1; m < (int)HumanDefines.HeroType.MAX; m++)
                _heroData[m] = new SoldierBasicData();
            _heroGenericData = new HeroGenericIndividualData();

            _sdOnField = new SerializedDictionary<byte, AsyncDataPoolForTransform<Soldier>[]>();
            var pool = new AsyncDataPoolForTransform<Soldier>[(int)WE.RaceType.MAX];
            for (int i = 1; i < (int)WE.RaceType.MAX; i++)
            {
                pool[i] = new AsyncDataPoolForTransform<Soldier>();
                if(i == (int)WE.RaceType.Neutral) //中立生物要少得多
                    pool[i].EnableTransformSync(1000, sdAdd => sdAdd.gs_transform);
                else
                    pool[i].EnableTransformSync(10000, sdAdd => sdAdd.gs_transform);
                pool[i].RegisterOnItemRemoved(ReleaseSoldierIntoPool);
            }
            _sdOnField.Add(WE.OnGroundMapIndex, pool);

            _sdPool = new Dictionary<int, DataPool<GameObject>>[(int)WE.RaceType.MAX, (int)SD.TroopType.MAX];

            //load soldier prefabs
            LoadSoldierPrefab();
            //load hero prefabs
            LoadHeroPrefab();

            //soldier conf
            for (int i = 1; i < (int)WE.RaceType.MAX; i++)
            {
                StringBuilder path = new StringBuilder("Conf/Soldier/");
                path.Append(((WE.RaceType)i).ToString() + "/");
                for (int j = 1; j < (int)SD.TroopType.MAX; j++)
                {
                    ReadConfs($"{path.ToString()}{((SD.TroopType)j).ToString()}");
                }
            }

            //hero conf
            {
                StringBuilder path = new StringBuilder("Conf/Hero");
                ReadConfs(path.ToString());
            }

            //load soldier individual data
            InitIndividualDdata();

            _sdProdNotifyList = new DataPool<ISoldierProductionNotify>[(int)WE.FactionType.MAX, (int)SD.TroopType.MAX];
            _sdDieNotifyList = new DataPool<ISoldierDieNotify>[(int)WE.FactionType.MAX, (int)SD.TroopType.MAX];
            for (int i = 1; i < (int)WE.FactionType.MAX; i++)
            {
                for (int j = 1; j < (int)SD.TroopType.MAX; j++)
                {
                    _sdProdNotifyList[i, j] = new DataPool<ISoldierProductionNotify>(true);
                    _sdDieNotifyList[i, j] = new DataPool<ISoldierDieNotify>(true);
                }
            }
        }

        private void OnDestroy()
        {
            _moveJobHandle.Complete();
            if (_moveCmds.IsCreated)
                _moveCmds.Dispose();

            foreach (var tmp in _sdOnField)
            {
                var pool = tmp.Value;
                if(pool == null)
                    continue;
                for (int i = 0; i < (int)WE.RaceType.MAX; i++)
                {
                    pool[i]?.Dispose();
                }
            }
        }

#endregion

#region public functions
        public bool InitSoldierCtrl()
        {
            //add melee/magic/ranged pools
            for (int i = 0; i < (int)WE.RaceType.MAX; i++)
            {
                for (int j = 0; j < (int)SD.TroopType.MAX; j++)
                {
                    _sdPool[i, j] = new Dictionary<int, DataPool<GameObject>>();
                }
            }

            _beInited = true;
            return true;
        }

        public void WaitForMoveJobFinish()
        {
            _moveJobHandle.Complete();
        }

        //调度所有士兵的移动
        public void AllSoldierMoveJob(JobHandle dependency)
        {
            if (_beInited == false || _canWork == false)
                return;

            if (_moveJobBatches == null)
                _moveJobBatches = new List<SoldierMoveBatch>(64);
            else
                _moveJobBatches.Clear();

            _moveCmdIndex = 0;
            foreach (var value in _sdOnField)//分地图
            {
                byte mapId = value.Key;
                if(WarMapCtrl.Instance.GetMapByIndex(mapId).gs_isOpened == false) //地图还没有打开
                    continue;

                var pools = value.Value;
                for (int i = 1; i < (int)WE.RaceType.MAX; i++) //分种族 调度
                {
                    var soldiers = pools[i];
                    soldiers.ForEachAndFlush(null); //先flush一下把soldier加进来,要不cnt永远是0

                    int cnt = soldiers.Count;
                    if (cnt == 0)
                        continue;

                    _moveJobBatches.Add(new SoldierMoveBatch
                    {
                        p_mapId = mapId,
                        p_raceType = i,
                        p_soldiers = soldiers,
                        p_cmdOffset = _moveCmdIndex,
                        p_count = cnt
                    });
                    _moveCmdIndex += cnt;
                }
            }

            if (_moveJobBatches.Count == 0)
                return;

            EnsureMoveCapacity(_moveCmdIndex);

            for (int b = 0; b < _moveJobBatches.Count; b++)
            {
                SoldierMoveBatch batch = _moveJobBatches[b];
                var cmdSlice = _moveCmds.GetSubArray(batch.p_cmdOffset, batch.p_count);
                batch.p_soldiers.ForList(static (list, cmd) =>
                {
                    for (int k = 0; k < list.Count; k++)
                    {
                        var sd = list[k];
                        cmd[k] = new SoldierMoveCmd
                        {
                            p_currentPos = new float2(sd.gs_transform.position.x, sd.gs_transform.position.y),
                            p_gridIndex = sd.gs_gridIndex,
                            p_moveSpeed = sd.gs_curState.p_moveSpeed,
                            p_mass = sd.gs_curState.p_mass,
                            p_radius = sd.gs_bodyRadius,
                            p_status = (byte)sd.gs_curStatus,
                            p_homeY = sd.gs_homeY,
                            p_homeYBackMul = sd.GetHomeYMul(),
                            p_targetPos = sd.gs_rival != null
                                ? new float2(sd.gs_rival.transform.position.x, sd.gs_rival.transform.position.y)
                                : float2.zero,
                            p_desiredDir = new float2(sd.gs_desiredMoveDir.x, sd.gs_desiredMoveDir.y),
                            p_moveCmd = (byte)sd.gs_curMoveCmd,
                            p_flowIndex = sd.gs_currentFlowIndex
                        };
                    }
                }, cmdSlice);
            }

            for (int b = 0; b < _moveJobBatches.Count; b++)
            {
                SoldierMoveBatch batch = _moveJobBatches[b];
                byte mapId = batch.p_mapId;
                int i = batch.p_raceType;
                var soldiers = batch.p_soldiers;
                var cmdSlice = _moveCmds.GetSubArray(batch.p_cmdOffset, batch.p_count);

                var flowMap = WarMapCtrl.Instance.GetPathFinderMapByIndex(mapId);

                var dynamicQuery = SpatialGridManager.Instance.GetQueryHelper(mapId, (int)WE.WarEleType.SOLDIER);
                var staticQuery = SpatialGridManager.Instance.GetQueryHelper(mapId, (int)WE.WarEleType.BUILDING); // 建筑和石头都属于 Static Grid

                SoldierMoveJob job = new SoldierMoveJob
                {
                    p_moveCmds = cmdSlice,
                    p_flowFieldPool = flowMap.gs_flowFieldPool,
                    p_faction = (byte)(i == (int)WE.RaceType.Human ? WE.FactionType.FRIENDLY : WE.FactionType.ENEMY),
                    p_flowCellSize = flowMap.gs_cellSize,
                    p_flowMapOrigin = new float2(flowMap.gs_bounds.min.x, flowMap.gs_bounds.min.y),
                    p_flowCols = Mathf.CeilToInt(flowMap.gs_bounds.size.x / flowMap.gs_cellSize),
                    p_flowRows = Mathf.CeilToInt(flowMap.gs_bounds.size.y / flowMap.gs_cellSize),
                    p_flowMapMax = new float2(flowMap.gs_bounds.max.x, flowMap.gs_bounds.max.y),
                    p_dynamicQuery = dynamicQuery,
                    p_staticQuery = staticQuery,
                    p_deltaTime = Time.fixedDeltaTime
                };

                // 分发 Job（此时 _moveCmds 已全部写完，无读写与已调度 Job 交错）
                TransformAccessArray transformArray = soldiers.gs_transformArray;
                JobHandle mapDeps = JobHandle.CombineDependencies(dependency, flowMap.gs_flowFieldJobHandle);
                JobHandle mapMoveJob = job.Schedule(transformArray, mapDeps);
                _moveJobHandle = JobHandle.CombineDependencies(_moveJobHandle, mapMoveJob);
            }

            _moveJobScheduled = true;
        }

        //确保士兵的位置计算完成,并更新到grid中
        public void SoldiersLaterUpdate()
        {
            if (_moveJobScheduled == false)
                return;

            // 主线程强制等待子线程计算完毕
            _moveJobHandle.Complete();
            _moveJobScheduled = false;

            // MoveJob 只修改 Transform，统一在这里把位置同步回 SpatialGrid 的实体数据。
            foreach (var mapPools in _sdOnField.Values)
            {
                for (int i = 1; i < (int)WE.RaceType.MAX; i++)
                {
                    var pool = mapPools[i];
                    pool?.ForEachReadOnly(static sd => sd.SyncSpatialEntity());
                }
            }
        }

        public Soldier AddSoldierAt(WE.RaceType raceT, SD.TroopType troopT, int soldierT, Vector2 pos, byte mapId)
        {
            if (_beInited == false)
                return null;

            GameObject obj = GetSoldierFromPool(raceT, (int)troopT, soldierT);
            Soldier sd = null;
            if (obj == null) //create a new game object
            {
                GameObject sdPfb = null;
                SoldierBasicData[] array = null;

                array = _soldierData[(int)raceT, (int)troopT];
                if (array == null || soldierT >= array.Length)
                    return null;
                sdPfb = array[soldierT].p_prefab;
                if (sdPfb == null)
                {
                    GameLogger.LogError($"Can not get soldier prefab {raceT} {troopT} {soldierT}");
                    return null;
                }

                sd = Instantiate(sdPfb, pos, Quaternion.identity, transform).GetComponent<Soldier>();
                if (sd == null)
                {
                    GameLogger.LogError($"Add soldier error {raceT} {troopT} {soldierT}");
                    return null;
                }

                EnsureMapPool(mapId);
                _sdOnField[mapId][(int)raceT].AddItemAsync(sd);
            }
            else
            {
                List<Soldier> list = null;

                obj.transform.position = pos;
                sd = obj.GetComponent<Soldier>();
                if (ReferenceEquals(sd, null) == true)
                {
                    GameLogger.LogError($"Add soldier error 2 {raceT} {troopT} {soldierT}");
                    return null;
                }

                EnsureMapPool(mapId);
                _sdOnField[mapId][(int)raceT].AddItemAsync(sd);
            }

            //不能在这里调用NotifySoldierProduction,是因为必须要在barrack中init soldier之后才能调用
            return sd;
        }

        //remove a soldier from the war field
        public void RemoveSoldier(WE.RaceType race, SD.TroopType troop, int soldierType, Soldier sd, SD.SoldierLevel sdLevel, byte mapId)
        {
            { //NotifySoldierDie 里面用了_rmLock,所以不能在_rmLock内部
                WE.FactionType faction;
                if (race == WE.RaceType.Human)
                    faction = WE.FactionType.FRIENDLY;
                else
                    faction = WE.FactionType.ENEMY;
                NotifySoldierDie(faction, troop, sd);
            }

            EnsureMapPool(mapId);
            _sdOnField[mapId][(int)race].RemoveItemAsync(sd);

            //敌方士兵死亡生成能量宝石
            if (race != WE.RaceType.Human && race != WE.RaceType.Neutral)
            {
                WarResCtrl.Instance.AddPickableResAt(sdLevel, sd.transform.position, mapId);
            }
        }

        //在地图之间转移士兵
        //修改士兵的mapid
        public void TransferSoldiderToMap(Soldier sd, Vector2 toPos, byte fromMapId, byte toMapId)
        {
            if (sd == null)
                return;
            EnsureMapPool(fromMapId);
            EnsureMapPool(toMapId);
            _sdOnField[fromMapId][(int)sd.gs_race].RemoveItemAsync(sd);

            sd.transform.position = toPos;
            sd.ChangeMapId(toMapId);
            sd.SyncSpatialEntity();

            _sdOnField[toMapId][(int)sd.gs_race].AddItemAsync(sd);
        }

        public Hero AddHeroAt(HumanDefines.HeroType heroT, Vector2 pos, byte mapId, int skillType,
            HeroGenericIndividualData.IndividualDataType[] talents)
        {
            //not check _beInited
            if (_curHero != null)
            {
                GameLogger.LogError($"Can not add hero again");
                return null;
            }

            Hero hero = null;
            try
            {
                GameObject heroPfb = _heroData[(int)heroT].p_prefab;
                hero = Instantiate(heroPfb, pos, Quaternion.identity, transform).GetComponent<Hero>();
            }
            catch (Exception e)
            {
                GameLogger.LogException($"Load hero error {heroT}", e);
                return null;
            }

            _curHero = hero;
            switch (heroT)
            {
                case HumanDefines.HeroType.MELEEHERO:
                    ((MeleeHero)_curHero).InitHero(mapId, skillType, talents);
                    break;
                case HumanDefines.HeroType.RANGEDHERO:
                    ((RangedHero)_curHero).InitHero(mapId, skillType, talents);
                    break;
                case HumanDefines.HeroType.MAGICHERO:
                    ((MagicHero)_curHero).InitHero(mapId, skillType, talents);
                    break;
                default:
                    GameLogger.LogError($"Unknown hero type {heroT}");
                    break;
            }

            // Hero 也必须进入地图士兵池，才能参与统一的 Move Job 调度并实际改变 Transform。
            EnsureMapPool(mapId);
            _sdOnField[mapId][(int)WE.RaceType.Human].AddItemAsync(_curHero);

            return hero;
        }

        //通过upgrade解锁到一种新的士兵类型
        public bool UnlockNewSoldierType(WE.RaceType race, SD.TroopType troopT, int soldierT, bool isHero)
        {
            SoldierBasicData data = null;
            if (isHero == false)
                data = _soldierData[(int)race, (int)troopT][soldierT];
            else
                data = _heroData[soldierT];

            if (data.p_isUnlocked == true)
            {
                GameLogger.LogError($"Soldier already unlocked {race} {troopT} {soldierT}, can not unlocked again");
                return false;
            }

            data.p_isUnlocked = true;
            return true;
        }

        //通过抽卡等方式获取到一种新的士兵类型
        public bool EnableNewSoldierType(WE.RaceType race, SD.TroopType troopT, int soldierT)
        {
            var data = _soldierData[(int)race, (int)troopT][soldierT];
            if (data.p_isAvailable == true)
            {
                GameLogger.LogError($"Soldier already available {race} {troopT} {soldierT}, can not enable again");
                return false;
            }

            if (data.p_isUnlocked == false)
            {
                GameLogger.LogError($"Soldier not unlocked yet {race} {troopT} {soldierT}");
                return false;
            }

            data.p_isAvailable = true;
            return true;
        }

        // get the available soldier of the race troop
        public List<SoldierConf> GetAvailableSoldiers(WE.RaceType race, SD.TroopType troopT)
        {
            List<SoldierConf> ret = new List<SoldierConf>();
            var list = _soldierData[(int)race, (int)troopT];
            int cnt = list.Length;
            for (int i = 0; i < cnt; i++)
            {
                if (list[i].p_isAvailable == true && list[i].p_isUnlocked == true)
                    ret.Add(list[i].p_conf);
            }

            return ret;
        }

        public bool IsSoldierAvailable(WE.RaceType race, SD.TroopType troopT, int soldierT)
        {
            if (race != WE.RaceType.Human) //enemy soldier are available by default
                return true;

            return (_soldierData[(int)race, (int)troopT][soldierT].p_isAvailable && _soldierData[(int)race, (int)troopT][soldierT].p_isUnlocked);
        }

        // for human a troop only has one basic soldier
        public SoldierConf GetHumanBasicTypeSoldier(SD.TroopType troopT)
        {
            var array = _soldierData[(int)WE.RaceType.Human, (int)troopT];
            int cnt = array.Length;
            for (int i = 0; i < cnt; i++)
            {
                if (array[i] == null || array[i].p_conf == null)
                    continue;

                if (array[i].p_conf.p_level == SD.SoldierLevel.BASICLEVEL)
                    return array[i].p_conf;
            }

            return null;
        }

        //only human has high level type
        public List<SoldierConf> GetHighLevelTypeSoldiers(WE.RaceType race, SD.TroopType troopT)
        {
            List<SoldierConf> ret = new List<SoldierConf>();
            var array = _soldierData[(int)race, (int)troopT];
            int cnt = array.Length;
            for (int i = 0; i < cnt; i++)
            {
                if (array[i] == null || array[i].p_conf == null)
                    continue;

                if (array[i].p_conf.p_level == SD.SoldierLevel.HIGHLEVEL)
                    ret.Add(array[i].p_conf);
            }

            return ret;
        }

        //get all of the soldier conf of the race.troop
        public List<SoldierConf> GetSoldiers(WE.RaceType race, SD.TroopType troopT)
        {
            List<SoldierConf> ret = new List<SoldierConf>();
            var array = _soldierData[(int)race, (int)troopT];
            if(array == null || array.Length == 0)
                return null;
            int cnt = array.Length;
            for (int i = 0; i < cnt; i++)
            {
                if (array[i] == null || array[i].p_conf == null)
                    continue;
                ret.Add(array[i].p_conf);
            }

            return ret;
        }

        public SoldierConf GetHeroConf(HumanDefines.HeroType type)
        {
            return _heroData[(int)type].p_conf;
        }

        public void StartWork()
        {
            if (_beInited == true)
                _canWork = true;
        }

        public SoldierConf GetSdConf(WE.RaceType race, SD.TroopType troop, int sdType)
        {
            return _soldierData[(int)race, (int)troop][sdType].p_conf;
        }

        public IndividualData GetSdIndividualData(WE.RaceType race, SD.TroopType troop, int sdType)
        {
            var array = _soldierData[(int)race, (int)troop];
            if (array == null || array.Length <= sdType)
                return null;
            return array[sdType].p_individual;
        }

        public IndividualData GetHeroIndividualData(HumanDefines.HeroType heroType)
        {
            return _heroData[(int)heroType].p_individual;
        }

        public HeroGenericIndividualData GetHeroGenericIndividualData()
        {
            return _heroGenericData;
        }

        public bool ApplyImprovement(WE.ImproveSrc src, SD.SoldierImproveTarget target, WE.RaceType race, SD.TroopType troop, int soldierType,
            object value, bool isHero)
        {
            if (src == WE.ImproveSrc.FROMCARD || src == WE.ImproveSrc.FROMUPGRADE)
            {
                if (target == SD.SoldierImproveTarget.STATE)
                {
                    return ImproveConf(race, troop, soldierType, value, isHero);
                }
                else if (target == SD.SoldierImproveTarget.TALENT || target == SD.SoldierImproveTarget.SKILL)
                {
                    if(isHero == false)
                        return ImproveSDIndividualData(target, race, troop, soldierType, value);
                    else
                        return ImproveHeroIndividualData(target, race, troop, soldierType, value);
                }
                else if (target == SD.SoldierImproveTarget.GENERALTALENT || target == SD.SoldierImproveTarget.GENERALSKILL) //hero general individual data
                {
                    return ImproveHeroIndividualData(target, race, troop, soldierType, value);
                }

                GameLogger.LogError($"Improvement from {src} target unknown {target}");
            }
            else if (src == WE.ImproveSrc.FROMPROP) //使用道具
            {

            }

            GameLogger.LogError($"Improvement from unknown sroce {src}");
            return false;
        }

        public bool UserManipulateHero(int src, Vector2 targetPos)
        {
            if (_beInited == false)
                return false;
            return _curHero.UserManipulate(src, targetPos);
        }

        public void RegisterSoliderProduceNotify(ISoldierProductionNotify notify, WE.FactionType faction, SD.TroopType troop)
        {
            _sdProdNotifyList[(int)faction, (int)troop].AddItem(notify);
        }

        public void UnregisterSoliderProduceNotify(ISoldierProductionNotify notify, WE.FactionType faction, SD.TroopType troop)
        {
            _sdProdNotifyList[(int)faction, (int)troop].RemoveItem(notify);
        }

        //soldier生成的通知
        public void NotifySoldierProduction(WE.FactionType faction, SD.TroopType troop, Soldier sd, Vector2 pos)
        {
            var state = (faction, troop, sd, pos);
            _sdProdNotifyList[(int)faction, (int)troop].ForEach(static (notify, st) =>
            {
                notify.SoldierProduceIntf(st.faction, st.troop, st.sd, st.pos);
            }, state);
        }

        public void RegisterSoliderDieNotify(ISoldierDieNotify notify, WE.FactionType faction, SD.TroopType troop)
        {
            _sdDieNotifyList[(int)faction, (int)troop].AddItem(notify);
        }

        public void UnregisterSoliderDieNotify(ISoldierDieNotify notify, WE.FactionType faction, SD.TroopType troop)
        {
            _sdDieNotifyList[(int)faction, (int)troop].RemoveItem(notify);
        }

        //soldier死亡的通知
        public void NotifySoldierDie(WE.FactionType faction, SD.TroopType troop, Soldier sd)
        {
            var state = (faction, troop, sd);
            _sdDieNotifyList[(int)faction, (int)troop].ForEach(static (notify, st) =>
            {
                notify.SoldierDieIntf(st.faction, st.troop, st.sd);
            }, state);
        }
#endregion

#region private functions

        // 把 GlobalAnimConfig 里的 StateAnimData 按 SoldierAnimType 枚举名映射成运行时字典.
        // 不再区分 "默认变体" 与 "所有变体列表"——StateAnimData 本身包含所有变体,
        // Soldier 的 SelectRandomAnimVariation 在切换动画时随机选一个变体索引即可.
        private Dictionary<SD.SoldierAnimType, StateAnimData> BuildSoldierAnimStateClips(ElementAnimBakedData elementData)
        {
            if (elementData == null)
                return null;

            var stateClips = new Dictionary<SD.SoldierAnimType, StateAnimData>();
            for (int i = (int)SD.SoldierAnimType.MIN + 1; i < (int)SD.SoldierAnimType.MAX; i++)
            {
                SD.SoldierAnimType animType = (SD.SoldierAnimType)i;
                string stateName = GetStateName(animType);
                StateAnimData stateData = elementData.GetStateAnim(stateName);
                if (stateData == null || stateData.p_variations == null || stateData.p_variations.Count == 0)
                    continue;

                stateClips[animType] = stateData;
            }

            return stateClips.Count > 0 ? stateClips : null;
        }

        private string GetStateName(SD.SoldierAnimType animType)
        {
            switch (animType)
            {
                case SD.SoldierAnimType.IDLE:
                    return "IDLE";
                case SD.SoldierAnimType.MOVE:
                    return "MOVE";
                case SD.SoldierAnimType.ATTACK:
                    return "ATTACK";
                case SD.SoldierAnimType.SKILL:
                    return "SKILL";
                case SD.SoldierAnimType.STUN:
                    return "STUN";
                case SD.SoldierAnimType.DIE:
                    return "DIE";
                case SD.SoldierAnimType.BORN:
                    return "BORN";
                default:
                    return string.Empty;
            }
        }

        private GameObject GetSoldierFromPool(WE.RaceType raceT, int troopT, int soldierT)
        {
            if (troopT >= (int)SD.TroopType.MAX)
                return null;

            if (_sdPool[(int)raceT, troopT].ContainsKey(soldierT) == false)
                return null;

            DataPool<GameObject> pool = _sdPool[(int)raceT, troopT][soldierT];

            if (pool == null)
                return null;

            return pool.PopOut();
        }

        //make sure has this pool for map
        private void EnsureMapPool(byte mapId)
        {
            if (_sdOnField.ContainsKey(mapId))
                return;

            var pool = new AsyncDataPoolForTransform<Soldier>[(int)WE.RaceType.MAX];
            for (int i = 1; i < (int)WE.RaceType.MAX; i++) //洞穴中士兵会少很多
            {
                pool[i] = new AsyncDataPoolForTransform<Soldier>();
                if (i == (int)WE.RaceType.Neutral) //中立生物要少得多
                    pool[i].EnableTransformSync(500, sdAdd => sdAdd.gs_transform);
                else
                    pool[i].EnableTransformSync(5000, sdAdd => sdAdd.gs_transform);
                pool[i].RegisterOnItemRemoved(ReleaseSoldierIntoPool);
            }
            _sdOnField.Add(mapId, pool);
        }

        //private void ReleaseSoldierIntoPool(WE.RaceType raceT, SD.TroopType troopT, int soldierT, GameObject obj)
        private void ReleaseSoldierIntoPool(Soldier soldier)
        {
            int raceT = (int)soldier.gs_race;
            int troopT = (int)soldier.gs_troopType;
            int soldierT = soldier.gs_sdType;
            if (_sdPool[(int)raceT, (int)troopT].ContainsKey(soldierT) == false)
                _sdPool[(int)raceT, (int)troopT][soldierT] = new DataPool<GameObject>(true);

            DataPool<GameObject> pool = _sdPool[(int)raceT, (int)troopT][soldierT];
            pool.AddItem(soldier.gameObject);

            return;
        }

        private void LoadSoldierPrefab()
        {
            for (int i = 1; i < (int)WE.RaceType.MAX; i++)
            {
                StringBuilder path = new StringBuilder("Prefabs/SoldierPf/");
                path.Append(((WE.RaceType)i).ToString() + "/");
                for (int j = 1; j < (int)SD.TroopType.MAX; j++)
                {
                    var tmp = path.ToString() + ((SD.TroopType)j).ToString();
                    GameObject[] prefabs = Resources.LoadAll<GameObject>(path.ToString() + ((SD.TroopType)j).ToString());
                    if (prefabs.Length == 0) //not has prefabs or not has this folder
                        continue;

                    Type scriptType = typeof(Soldier);
                    foreach (var prefab in prefabs)
                    {
                        try
                        {
                            Component scriptComponent = prefab.GetComponent(scriptType);
                            Type actualType = scriptComponent.GetType();

                            if (scriptComponent != null)
                            {
                                PropertyInfo property = null;
                                property = actualType.GetProperty("gs_sdType");

                                if (property != null)
                                {
                                    object sdType = property.GetValue(scriptComponent);
                                    if ((int)sdType == 0)
                                    {
                                        GameLogger.LogError($"Load soldier prefab {prefab.name} soldier type {sdType} {(int)sdType} error");
                                        continue;
                                    }
                                    else
                                        GameLogger.LogInfo($"Load soldier prefab {prefab.name} {sdType} {(int)sdType}");

                                    _soldierData[i, j][(int)sdType].p_prefab = prefab;
                                }
                                else
                                {
                                    GameLogger.LogError($"Load soldier prefab {prefab.name} soldier error, can not get the interface gs_sdType");
                                }

                                //check race/troop/faction are set for the prefab
                                property = actualType.GetProperty("gs_faction");
                                if (property != null)
                                {
                                    object value = property.GetValue(scriptComponent);
                                    if ((int)value == 0)
                                        GameLogger.LogError($"Load soldier prefab {prefab.name} faction {value} {(int)value} error");
                                }

                                property = actualType.GetProperty("gs_race");
                                if (property != null)
                                {
                                    object value = property.GetValue(scriptComponent);
                                    if ((int)value != i)
                                        GameLogger.LogError($"Load soldier prefab {prefab.name} race {value} {(int)value}!={i} error");
                                }

                                property = actualType.GetProperty("gs_troopType");
                                if (property != null)
                                {
                                    object value = property.GetValue(scriptComponent);
                                    if ((int)value != j)
                                        GameLogger.LogError($"Load soldier prefab {prefab.name} troop {value} {(int)value}!={j} error");
                                }

                                if (AnimCtrl.Instance == null)
                                    GameLogger.LogError("AnimCtrl.Instance not ready");
                                else
                                {
                                    IAnimInfo animInfo = scriptComponent as IAnimInfo;
                                    uint eleAnimId = animInfo.IAnimInfo_GetEleAnimId();
                                    Dictionary<string, uint> stateDic = animInfo.IAnimInfo_GetStateId();
                                    if (AnimCtrl.Instance.BindAnimWithEntity(eleAnimId, prefab.name, stateDic, out var blobAssetRef) == false)
                                        GameLogger.LogWarning($"BindAnimWithEntity failed：{prefab.name} (eleAnimId={eleAnimId})");
                                    else
                                    {
                                        ref BlobAnimData data = ref blobAssetRef.Value;
                                        int stateCount = data.p_states.Length;
                                        for (int m = 0; m < stateCount; m++)
                                        {
                                            ref BlobStateData state = ref data.p_states[m];
                                            //attack 和 skill 必须要有event frame来做事件回调
                                            if (state.p_stateId == (uint)SD.SoldierAnimType.ATTACK || state.p_stateId == (uint)SD.SoldierAnimType.SKILL)
                                            {
                                                if(state.p_isLoop == true)
                                                    GameLogger.LogError($"{prefab.name} can not set animation {(SD.SoldierAnimType)state.p_stateId} to be loop");

                                                int variationCount = state.p_variations.Length;
                                                for (int n = 0; n < variationCount; n++)
                                                {
                                                    ref BlobVariationData variation = ref state.p_variations[n];
                                                    if (variation.p_eventFrame < 0)
                                                        GameLogger.LogError($"{prefab.name} not set event frame for animation {(SD.SoldierAnimType)state.p_stateId}");
                                                }
                                            }
                                            else //其他的动画必须要loop
                                            {
                                                if(state.p_isLoop != true)
                                                    GameLogger.LogError($"{prefab.name} must set animation {(SD.SoldierAnimType)state.p_stateId} to be loop");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            GameLogger.LogException($"Load soldier prefab exception {prefab.name}", e);
                            throw;
                        }
                    }
                }
            }
        }

        private void LoadHeroPrefab()
        {
            StringBuilder path = new StringBuilder("Prefabs/HeroPf/");
            GameObject[] prefabs = Resources.LoadAll<GameObject>(path.ToString());
            Type scriptType = typeof(Hero);
            foreach (var prefab in prefabs)
            {
                Component scriptComponent = prefab.GetComponent(scriptType);
                Type actualType = scriptComponent.GetType();
                if (scriptComponent != null)
                {
                    PropertyInfo property = actualType.GetProperty("gs_sdType");
                    if (property != null)
                    {
                        object sdType = property.GetValue(scriptComponent);
                        if ((int)sdType == 0)
                        {
                            GameLogger.LogError($"Load soldier prefab {prefab.name} soldier type {sdType} {(int)sdType} error");
                            continue;
                        }
                        else
                            GameLogger.LogInfo($"Load soldier prefab {prefab.name} {sdType} {(int)sdType}");

                        _heroData[(int)sdType].p_prefab = prefab;
                    }
                    else
                    {
                        GameLogger.LogError($"Load hero prefab {prefab.name}  error, can not get the interface gs_sdType");
                    }

                    //check race/troop/faction are set for the prefab
                    property = actualType.GetProperty("gs_faction");
                    if (property != null)
                    {
                        object value = property.GetValue(scriptComponent);
                        if ((int)value == 0)
                            GameLogger.LogError($"Load hero prefab {prefab.name} faction {value} {(int)value} error");
                    }

                    property = actualType.GetProperty("gs_troopType");
                    if (property != null)
                    {
                        object value = property.GetValue(scriptComponent);
                        if ((int)value == 0)
                            GameLogger.LogError($"Load hero prefab {prefab.name} troop {value} {(int)value} error");
                    }
                }
            }

        }

        private bool ImproveConf(WE.RaceType race, SD.TroopType troop, int soldierType, object improveValue, bool isHero)
        {
            SoldierConf conf = null;
            try
            {
                if(isHero == false)
                    conf = _soldierData[(int)race, (int)troop][soldierType].p_conf;
                else
                    conf = _heroData[soldierType].p_conf;
                if (conf == null)
                {
                    GameLogger.LogError($"conf {race} {troop} {soldierType} is NULL");
                    return false;
                }
            }
            catch (Exception e)
            {
                GameLogger.LogException($"Fail to get conf {race} {troop} {soldierType}", e);
                return false;
            }

            ValueTuple<SD.StateSoldierEffectType, float, GD.CalDeltaType> tupple =
                (ValueTuple<SD.StateSoldierEffectType, float, GD.CalDeltaType>)improveValue;
            SD.StateSoldierEffectType stateType = tupple.Item1;
            float value = tupple.Item2;
            GD.CalDeltaType calType = tupple.Item3;

            switch (stateType)
            {
                case SD.StateSoldierEffectType.HEALTH:
                    conf.p_health = Utils.CalDeltaValue(conf.p_health, value, calType); //only change the hp max
                    if (conf.p_health <= 0)
                    {
                        conf.p_health = 0.00001f;
                        GameLogger.LogWarning("Max health must > 0");
                    }

                    break;
                case SD.StateSoldierEffectType.DAMAGE:
                    conf.p_damage = Utils.CalDeltaValue(conf.p_damage, value, calType);
                    if (conf.p_damage <= 0)
                    {
                        conf.p_damage = 0.00001f;
                        GameLogger.LogWarning("Damage must > 0");
                    }

                    break;
                case SD.StateSoldierEffectType.ATTACKRANGE:
                    conf.p_attackRange = Utils.CalDeltaValue(conf.p_attackRange, value, calType);
                    if (conf.p_attackRange < 0)
                    {
                        conf.p_attackRange = 0;
                        GameLogger.LogWarning("Attack range must >= 0");
                    }

                    break;
                case SD.StateSoldierEffectType.SEARCHRANGE:
                    conf.p_searchRange = Utils.CalDeltaValue(conf.p_searchRange, value, calType);
                    if (conf.p_searchRange < 0)
                    {
                        conf.p_searchRange = 0;
                        GameLogger.LogWarning("Search range must >= 0");
                    }

                    break;
                case SD.StateSoldierEffectType.ATTACKSPEED:
                    conf.p_attackSpeed = Utils.CalDeltaValue(conf.p_attackSpeed, value, calType);
                    if (conf.p_attackSpeed <= 0)
                    {
                        conf.p_attackSpeed = 0.00001f;
                        GameLogger.LogWarning("Attack speed must > 0");
                    }

                    break;
                case SD.StateSoldierEffectType.MOVESPEED:
                    conf.p_moveSpeed = Utils.CalDeltaValue(conf.p_moveSpeed, value, calType);
                    if (conf.p_moveSpeed <= 0)
                    {
                        conf.p_moveSpeed = 0.00001f;
                        GameLogger.LogWarning("Move speed must > 0");
                    }

                    break;
                case SD.StateSoldierEffectType.PHYARMOR:
                    conf.p_phyArmor = Utils.CalDeltaValue(conf.p_phyArmor, value, calType);
                    break;
                case SD.StateSoldierEffectType.SPAWNSPEED:
                    conf.p_spawnTime = Utils.CalDeltaValue(conf.p_spawnTime, value, calType);
                    if (conf.p_spawnTime <= 0)
                    {
                        conf.p_spawnTime = 0.1f;
                        GameLogger.LogWarning("Spawn speed must > 0");
                    }

                    conf.p_spawnTimeInCycle = Utils.CountOfFixUpdate(conf.p_spawnTime);
                    break;
                case SD.StateSoldierEffectType.HPINC:
                    conf.p_hpInc = Utils.CalDeltaValue(conf.p_hpInc, value, calType);
                    if (conf.p_hpInc < 0)
                    {
                        conf.p_hpInc = 0;
                        GameLogger.LogWarning("HP inc must >= 0");
                    }

                    break;
                case SD.StateSoldierEffectType.SKILLTIMESTEP:
                    conf.p_skillTimeStep = Utils.CalDeltaValue(conf.p_skillTimeStep, value, calType);
                    if (conf.p_skillTimeStep < 0)
                    {
                        GameLogger.LogError($"Skill time step  must > 0, {conf.p_skillTimeStep} set tobe 0");
                        conf.p_skillTimeStep = 0;
                    }

                    break;
                case SD.StateSoldierEffectType.DIEAWARD:
                    conf.p_dieReward = Utils.CalDeltaValue(conf.p_dieReward, value, calType);
                    if (conf.p_dieReward < 0)
                    {
                        GameLogger.LogError($"Die award  must >= 0, {conf.p_dieReward} set tobe 0");
                        conf.p_dieReward = 0;
                    }

                    break;
                case SD.StateSoldierEffectType.DIEAWARDCHANCE:
                    conf.p_dieRewardChance = Mathf.RoundToInt(Utils.CalDeltaValue(conf.p_dieRewardChance, value, calType));
                    if (conf.p_dieRewardChance < 0)
                    {
                        GameLogger.LogError($"Die award chance must >= 0, {conf.p_dieRewardChance} set tobe 0");
                        conf.p_dieRewardChance = 0;
                    }

                    break;
                default:
                    GameLogger.LogError($"Unknown soldier type {stateType}");
                    return false;
            }

            return true;
        }

        //升级士兵的individual data
        private bool ImproveSDIndividualData(SD.SoldierImproveTarget target, WE.RaceType race, SD.TroopType troop, int soldierType, object value)
        {
            if (race != WE.RaceType.Human)
            {
                GameLogger.LogError($"Not support improve race {race}");
                return false;
            }

            IndividualData data = null;
            try
            {
                data = _soldierData[(int)race, (int)troop][soldierType].p_individual;
            }
            catch (Exception e)
            {
                GameLogger.LogException($"Exception during find soldier individual data of {troop} {soldierType}", e);
                return false;
            }

            if (data == null)
            {
                GameLogger.LogError($"Can not find soldier individual data of {troop} {soldierType}");
                return false;
            }

            return data.Improve(value);
        }

        //升级英雄的individual data
        private bool ImproveHeroIndividualData(SD.SoldierImproveTarget target, WE.RaceType race, SD.TroopType troop, int soldierType, object value)
        {
            if (race != WE.RaceType.Human)
            {
                GameLogger.LogError($"Not support improve race {race}");
                return false;
            }

            IndividualData data = null;
            try
            {
                if (target == SD.SoldierImproveTarget.GENERALSKILL || target == SD.SoldierImproveTarget.GENERALTALENT)
                    data = _heroGenericData;
                else
                    data = _heroData[soldierType].p_individual;
            }
            catch (Exception e)
            {
                GameLogger.LogException($"Exception during find soldier individual data of {troop} {soldierType}", e);
                return false;
            }

            if (data == null)
            {
                GameLogger.LogError($"Can not find soldier individual data of {troop} {soldierType}");
                return false;
            }

            return data.Improve(value);
        }

        private void ReadConfs(string path)
        {
            TextAsset[] xmlFiles = Resources.LoadAll<TextAsset>(path);
            for (int i = 0; i < xmlFiles.Length; i++)
            {
                XmlDocument confXML = new XmlDocument();
                confXML.LoadXml(xmlFiles[i].text);
                if (confXML == null)
                    continue;

                SoldierConf sdConf = new SoldierConf();
                XmlNodeList nodeList = confXML.SelectSingleNode("sdConf").ChildNodes;
                for (int j = 0; j < nodeList.Count; j++)
                {
                    if (nodeList[j].NodeType == XmlNodeType.Comment)
                        continue;

                    XmlElement tmp = (XmlElement)nodeList[j];
                    switch (tmp.Name)
                    {
                        case "name":
                            sdConf.p_name = tmp.GetAttribute("value");
                            break;
                        case "type":
                            try
                            {
                                sdConf.p_troop = (SD.TroopType)Enum.Parse(typeof(SD.TroopType), tmp.GetAttribute("troop"), ignoreCase: true);
                            }
                            catch (Exception e)
                            {
                                GameLogger.LogException("Read soldier conf troop failed " + tmp.GetAttribute("troop"), e);
                                continue;
                            }

                            sdConf.p_soldierType = int.Parse(tmp.GetAttribute("soldier"));
                            break;
                        case "health":
                            sdConf.p_health = float.Parse(tmp.GetAttribute("value"));
                            break;
                        case "level":
                            try
                            {
                                sdConf.p_level = (SD.SoldierLevel)Enum.Parse(typeof(SD.SoldierLevel), tmp.GetAttribute("value"), ignoreCase: true);
                            }
                            catch (Exception e)
                            {
                                GameLogger.LogException("Read soldier conf level failed " + tmp.GetAttribute("value"), e);
                                throw;
                            }

                            break;
                        case "attackRange":
                            sdConf.p_attackRange = float.Parse(tmp.GetAttribute("value"));
                            break;
                        case "searchRange":
                            sdConf.p_searchRange = float.Parse(tmp.GetAttribute("value"));
                            break;
                        case "attackSpeed": //每秒攻击多少次
                            sdConf.p_attackSpeed = float.Parse(tmp.GetAttribute("value"));
                            break;
                        case "damage":
                            sdConf.p_damage = float.Parse(tmp.GetAttribute("value"));
                            break;
                        case "moveSpeed": //每秒移动的距离
                            sdConf.p_moveSpeed = float.Parse(tmp.GetAttribute("value"));
                            break;
                        case "race":
                            string attribute = tmp.GetAttribute("value");
                            if (attribute == "human")
                                sdConf.p_race = WE.RaceType.Human;
                            else if (attribute == "oculars")
                                sdConf.p_race = WE.RaceType.Oculars;
                            else
                                sdConf.p_race = WE.RaceType.Neutral;
                            sdConf.p_faction = WarFieldUtil.GetFactionByRace(sdConf.p_race);
                            break;
                        case "phyArmor":
                            sdConf.p_phyArmor = float.Parse(tmp.GetAttribute("value"));
                            break;
                        case "spawnSpeed": //多长时间生产一个士兵
                        {
                            float speed = float.Parse(tmp.GetAttribute("value"));
                            sdConf.p_spawnTime = speed;
                            sdConf.p_spawnTimeInCycle = Utils.CountOfFixUpdate(speed);
                        }
                            break;
                        case "price":
                            sdConf.p_price = int.Parse(tmp.GetAttribute("value"));
                            break;
                        case "hpIncrease": //回血
                        {
                            float inc = float.Parse(tmp.GetAttribute("value"));
                            sdConf.p_hpInc = inc;
                        }
                            break;
                        case "description":
                        {
                            string description = tmp.GetAttribute("value");
                            if (string.IsNullOrEmpty(description) == false)
                            {
                                sdConf.p_description = description;
                                sdConf.p_description = sdConf.p_description.Replace("\\n", "\n");
                            }
                        }
                            break;
                        case "mass": //重量
                            sdConf.p_mass = float.Parse(tmp.GetAttribute("value"));
                            break;
                        case "specific":
                        {
                            var specNodeList = nodeList[j].ChildNodes;
                            int specCnt = specNodeList.Count;
                            for (int k = 0; k < specCnt; k++)
                            {
                                if (specNodeList[k].NodeType == XmlNodeType.Comment)
                                    continue;
                                XmlElement specTmp = specNodeList[k] as XmlElement;
                                if (sdConf.p_specConfs == null)
                                    sdConf.p_specConfs = new Dictionary<string, float>();
                                sdConf.p_specConfs.Add(specTmp.Name, float.Parse(specTmp.GetAttribute("value")));
                            }
                        }
                            break;
                        default:
                            break;
                    }
                }

                SoldierBasicData data = null;
                if (sdConf.p_race == WE.RaceType.Human)
                {
                    if (sdConf.p_level == SD.SoldierLevel.BASICLEVEL)
                    {
                        data = _soldierData[(int)sdConf.p_race, (int)sdConf.p_troop][sdConf.p_soldierType];
                        data.p_isAvailable = true;
                        data.p_isUnlocked = true;
                    }
                    else if (sdConf.p_level == SD.SoldierLevel.BOSSLEVEL) //hero
                    {
                        data = _heroData[sdConf.p_soldierType];
                        data.p_isAvailable = true;
                        data.p_isUnlocked = false; //需要upgrade解锁
                    }
                    else
                    {
                        data = _soldierData[(int)sdConf.p_race, (int)sdConf.p_troop][sdConf.p_soldierType];
                        data.p_isAvailable = false;
                        data.p_isUnlocked = false; //需要upgrade解锁
                    }
                }
                else //enemy soldier default is available
                {
                    data = _soldierData[(int)sdConf.p_race, (int)sdConf.p_troop][sdConf.p_soldierType];
                    data.p_isAvailable = true;
                    data.p_isUnlocked = true;
                }

                data.p_conf = sdConf;
            }
        }

        private void InitIndividualDdata()
        {
            //only human soldier has individual data
            int race = (int)WE.RaceType.Human;
            for (int i = 0; i < (int)SD.TroopType.MAX; i++)
            {
                if (i == (int)SD.TroopType.Melee)
                {
                    _soldierData[race, i][(int)HumanDefines.MeleeType.ASSASSIN].p_individual = new AssassinIndividualData();
                    _soldierData[race, i][(int)HumanDefines.MeleeType.SWORDSMAN].p_individual = new SwordsmanIndividualData();
                    _soldierData[race, i][(int)HumanDefines.MeleeType.SHIELDSOLDIER].p_individual = new ShieldSoldierIndividualData();

                    _heroData[(int)HumanDefines.HeroType.MELEEHERO].p_individual = new MeleeHeroIndividualData();
                }
                else if (i == (int)SD.TroopType.Ranged)
                {
                    _soldierData[race, i][(int)HumanDefines.RangedType.SHARPSHOOTER].p_individual = new SharpShooterIndividualData();
                    _soldierData[race, i][(int)HumanDefines.RangedType.SNIPER].p_individual = new SniperIndividualData();
                    _soldierData[race, i][(int)HumanDefines.RangedType.CANNONEER].p_individual = new CannoneerIndividualData();

                    _heroData[(int)HumanDefines.HeroType.RANGEDHERO].p_individual = new RangedHeroIndividualData();
                }
                else if (i == (int)SD.TroopType.Magic)
                {
                    _soldierData[race, i][(int)HumanDefines.MagicType.PRIEST].p_individual = new PriestIndividualData();
                    _soldierData[race, i][(int)HumanDefines.MagicType.SHAMAN].p_individual = new ShamanIndividualData();

                    _heroData[(int)HumanDefines.HeroType.MAGICHERO].p_individual = new MagicHeroIndividualData();
                }
            }
        }

        //对移动相关的数组扩容
        private void EnsureMoveCapacity(int neededCount)
        {
            if (neededCount <= _moveJobCapacity)
                return;

            int newCapacity = math.max(1024, (int)math.ceilpow2(neededCount));  //翻倍扩容

            if (_moveCmds.IsCreated)
                _moveCmds.Dispose();

            _moveCmds = new NativeArray<SoldierMoveCmd>(newCapacity, Allocator.Persistent);
            _moveJobCapacity = newCapacity;
        }
#endregion
    }
}
