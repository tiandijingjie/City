using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WarField
{
    using SD = SoldierDefines;
    using WBD = WarBuildingDefines;
    using WE = WarFieldElements;

    public class EnemyBarrack : WarBuilding, ISpawnSoldier
    {
#region public parameters

#endregion

#region private parameters

        //how many enemy solider will spawn was determined by configuration file, so add a new class to save spawn count
        private class EnemySoldierConf
        {
            public readonly SoldierConf p_conf;
            public int p_spawnCntPerTime; //每一次生成的个数
            public float p_spawnTimeLeft;
            public int p_spawnMaxTimes; //for some boss, may only can produce several times

            public EnemySoldierConf(SoldierConf conf, int spawnCntPerTime, int spawnMaxTimes)
            {
                p_conf = conf;
                p_spawnCntPerTime = spawnCntPerTime;
                p_spawnTimeLeft = p_conf.p_spawnTimeInCycle;
                p_spawnMaxTimes = spawnMaxTimes;
            }
        }

        private class SpawnStageInfo
        {
            public SD.TroopType p_troop;
            public int p_soldierType;
            public int p_spawnCntPerTime;
            public int p_spawnMaxTimes;
        }

        [SerializeField] private WBD.BarrackTriggerStage _triggerStage;
        [SerializeField] protected BarrackConf _barrackConf; //just the _bdConf

        //for debug
        [SerializeField] private bool _inTest;
        [SerializeField] private int _dbgProduceCnt = 1;

        private EnemySpawnStageTrigger[] _triggers;
        private List<SpawnStageInfo>[] _spawnStageInfos;
        private List<EnemySoldierConf> _spawnList;
        private int _spawnListLength = 0;

#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        protected override void Awake()
        {
            base.Awake();
            _spawnList = new List<EnemySoldierConf>();
            _spawnStageInfos = new List<SpawnStageInfo>[(int)WBD.BarrackTriggerStage.MAX];
            for (int i = 1; i < (int)WBD.BarrackTriggerStage.MAX; i++)
                _spawnStageInfos[i] = new List<SpawnStageInfo>();

            _triggers = new EnemySpawnStageTrigger[(int)WBD.BarrackTriggerStage.MAX];
            _triggers[(int)WBD.BarrackTriggerStage.FIRST] = _transform.Find("FirstTriggger").GetComponent<EnemySpawnStageTrigger>();
            _triggers[(int)WBD.BarrackTriggerStage.SECOND] = _transform.Find("SecondTriggger").GetComponent<EnemySpawnStageTrigger>();
            _triggerStage = WBD.BarrackTriggerStage.MIN;
        }

#endregion

#region public functions

        public bool InitBuilding(BuildingConf conf, WBD.BarrackTriggerStage defaultStage, byte mapId)
        {
            _bdConf = new BarrackConf(conf);
            if (base.InitBuilding(mapId) == false)
                return false;
            _barrackConf = _bdConf as BarrackConf;
            _triggerStage = defaultStage;
            return true;
        }

        //add stage info from level xml
        public bool AddSpawnStage(WBD.BarrackTriggerStage triggerStage, float triggerDistance, List<object> soldierInfos)
        {
            if (_spawnStageInfos[(int)triggerStage].Count > 0)
            {
                GameLogger.LogError($"Already add stage {triggerStage}, can not add again");
                return false;
            }

            int count = soldierInfos.Count;
            for (int i = 0; i < count; i++)
            {
                var value = (ValueTuple<SD.TroopType, int, int, int>)soldierInfos[i];
                SpawnStageInfo info = new SpawnStageInfo();
                info.p_troop = value.Item1;
                info.p_soldierType = value.Item2;
                info.p_spawnCntPerTime = value.Item3;
                info.p_spawnMaxTimes = value.Item4;
                _spawnStageInfos[(int)triggerStage].Add(info);
            }

            //the last stage trigger is just the building self ,not has trigger
            if (triggerStage == WarBuildingDefines.BarrackTriggerStage.FIRST || triggerStage == WarBuildingDefines.BarrackTriggerStage.SECOND)
            {
                //25 is half height of the map
                _triggers[(int)triggerStage].InitTrigger(new[] { new Vector2(-triggerDistance, 25), new Vector2(-triggerDistance, -25) });
            }

            return true;
        }

        public override void StartWork()
        {
            base.StartWork();
            switch (_triggerStage)
            {
                case WBD.BarrackTriggerStage.MIN:
                    _canWork = false;
                    _triggers[(int)WBD.BarrackTriggerStage.FIRST].gameObject.SetActive(true);
                    _triggers[(int)WBD.BarrackTriggerStage.SECOND].gameObject.SetActive(true);
                    break;
                case WBD.BarrackTriggerStage.FIRST:
                    _canWork = true;
                    _triggers[(int)WBD.BarrackTriggerStage.FIRST].gameObject.SetActive(false);
                    _triggers[(int)WBD.BarrackTriggerStage.SECOND].gameObject.SetActive(true);
                    ChangeSoldierSpawnByStage();
                    break;
                case WBD.BarrackTriggerStage.SECOND:
                    _canWork = true;
                    _triggers[(int)WBD.BarrackTriggerStage.SECOND].gameObject.SetActive(false);
                    ChangeSoldierSpawnByStage();
                    break;
                case WBD.BarrackTriggerStage.LAST:
                    _canWork = true;
                    ChangeSoldierSpawnByStage();
                    break;
                default:
                    break;
            }
        }

        public void OnSpawnStageTrigger(WBD.BarrackTriggerStage index)
        {
            if (_triggerStage < index)
            {
                _canWork = true;
                _triggerStage = index;
                if (ChangeSoldierSpawnByStage() == false)
                    GameLogger.LogError($"{_bdConf.gs_name} Fail to change stage from {_triggerStage} to {index}");
                else
                    GameLogger.LogInfo($"{_bdConf.gs_name} change stage from {_triggerStage} to {index}");
            }
            else
                GameLogger.LogError($"{_bdConf.gs_name} Fail to change small stage from {_triggerStage} to {index}");
        }

        public bool SpawnSoldierByIndex(int index)
        {
            throw new NotImplementedException();
        }

        //enemy not need to change skillType
        public bool SpawnSoldier(int race, int troopT, int soldierT, uint skillType)
        {
            if (_inTest == true)
            {
                if (_dbgProduceCnt <= 0)
                    return true;
                _dbgProduceCnt--;
            }

            //make sure the soldier pic showed in front of the barrack
            float seedY = Random.Range(-0.6f, -0.1f);
            float seedX = Random.Range(-0.3f, 0.3f);
            Soldier sd = SoldierCtrl.Instance.AddSoldierAt((WE.RaceType)race, (SD.TroopType)troopT, soldierT,
                _transform.position + new Vector3(seedX, seedY, 0), _mapId);
            if (ReferenceEquals(sd, null) == true)
                return false;
            sd.InitSoldier(_mapId);
            //isTest = true;
            return sd;
        }

#endregion

#region private functions

        protected override void OnBdWork(float deltaTime)
        {
            bool needRm = false;
            List<int> rmIndexList = null;
            for (int i = 0; i < _spawnListLength; i++)
            {
                EnemySoldierConf conf = _spawnList[i];
                conf.p_spawnTimeLeft--;
                if (conf.p_spawnTimeLeft <= 0)
                {
                    for (int j = 0; j < conf.p_spawnCntPerTime; j++)
                    {
                        ((ISpawnSoldier)this).SpawnSoldier((int)_bdConf.gs_race, (int)conf.p_conf.p_troop, conf.p_conf.p_soldierType);
                    }

                    if (conf.p_spawnMaxTimes > 0)
                    {
                        conf.p_spawnMaxTimes--;
                        if (conf.p_spawnMaxTimes == 0) //no more produce times, remmove it from list
                        {
                            if (rmIndexList == null)
                                rmIndexList = new List<int>();
                            rmIndexList.Add(i);
                            needRm = true;
                        }
                    }

                    conf.p_spawnTimeLeft += conf.p_conf.p_spawnTimeInCycle;
                }
            }

            if (needRm)
            {
                int cnt = rmIndexList.Count;
                for (int i = 0; i < cnt; i++)
                {
                    _spawnList.RemoveAt(rmIndexList[i]);
                }
            }
        }

        private bool ChangeSoldierSpawnByStage()
        {
            _spawnList.Clear();
            _spawnListLength = 0;
            List<SpawnStageInfo> lit = _spawnStageInfos[(int)_triggerStage];
            int count = lit.Count;
            for (int i = 0; i < count; i++)
            {
                SpawnStageInfo info = lit[i];
                if (AddSpawnSoldier(info.p_troop, info.p_soldierType, info.p_spawnCntPerTime, info.p_spawnMaxTimes) == false)
                    return false;
            }

            return true;
        }

        //add a type of soldier into spawn list, then when building woek will spawn it
        //as enemy can not draw card, so only can add soldier during the awake
        private bool AddSpawnSoldier(SD.TroopType troop, int soldierT, int spawnCntPerTime, int spawnMaxTimes)
        {
            if (spawnCntPerTime == 0 || spawnMaxTimes == 0)
            {
                GameLogger.LogError($"Enemy building {_bdConf.gs_name} add soldier fail with {spawnCntPerTime} {spawnMaxTimes}");
                return false;
            }

            SoldierConf sdConf = SoldierCtrl.Instance.GetSdConf(_bdConf.gs_race, troop, soldierT);
            if (sdConf == null)
            {
                GameLogger.LogError($"Can not get enemy soldier conf {_bdConf.gs_race} {troop} {soldierT}");
                return false;
            }

            EnemySoldierConf state = new EnemySoldierConf(sdConf, spawnCntPerTime, spawnMaxTimes);
            _spawnList.Add(state);
            _spawnListLength++;
            return true;
        }

        protected override bool OnBeAttacked(GameObject attacker, MonoBehaviour attackScript, WE.WarEleType attackerT, float damage, out float hitValue)
        {
            if (_triggerStage < WBD.BarrackTriggerStage.LAST)
            {
                _triggerStage = WBD.BarrackTriggerStage.LAST;
                ChangeSoldierSpawnByStage();
            }

            return base.OnBeAttacked(attacker, attackScript, attackerT, damage, out hitValue);
        }

#endregion
    }
}

