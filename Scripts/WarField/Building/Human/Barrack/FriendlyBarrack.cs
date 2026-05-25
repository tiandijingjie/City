using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace WarField
{
    using SD = SoldierDefines;
    using WBD = WarBuildingDefines;
    using WE = WarFieldElements;
    using BFD = BuffDefines;
    using WRD = WarResDefine;

    public class FriendlyBarrack : WarBuilding, ISpawnSoldier, IResWaiter
    {
#region public parameters

#endregion

#region private parameters

        [SerializeField] protected SD.TroopType _troopType; //value is the same as HumanDefines.BarrackType
        [SerializeField] protected BarrackConf _barrackConf; //just the _bdConf

        //for debug
        [SerializeField] protected bool _inTest;
        [SerializeField] protected int _dbgProduceCnt = 1;

        //the list is now producing
        protected float[] _spawnTimeLeft;
        protected bool[] _spawnEnable; //通过ui控制是否暂停生产
        protected uint[] _skillOfSpot; //每个生产位置的士兵的skill type
        protected SoldierConf[] _spawnArr;
        protected object[] _spawnResWait; //资源排队时的队列的indexer
        protected int _arrayLengh; //上面那些数组的长度


        protected SoldierConf _basicSoldierConf = null;
        protected object _spawnLock = new object();

        //通过UI选择的出兵位置
        protected Vector3[] _spawnTargetPos;
        protected float[] _spawnTargetPercent; // _spawnTargetPos/MapPassableHeight

#endregion

#region private parameters' get set

        public SoldierConf[] gs_spawnArr
        {
            get { return _spawnArr; }
        }

        public uint[] gs_skillOfSpot
        {
            get { return _skillOfSpot; }
        }

        public float[] gs_spawnTimeLeft
        {
            get { return _spawnTimeLeft; }
        }

        public bool[] gs_spawnEnable
        {
            get { return _spawnEnable; }
        }

        public float[] gs_spawnTargetPercent
        {
            get { return _spawnTargetPercent; }
        }

#endregion

#region Unity callbacks

#endregion

#region public functions

        public bool InitBuilding(BuildingConf conf, byte mapId)
        {
            _bdConf = new BarrackConf(conf);
            if (base.InitBuilding(mapId) == false)
                return false;

            _barrackConf = _bdConf as BarrackConf;
            _basicSoldierConf = SoldierCtrl.Instance.GetHumanBasicTypeSoldier(_troopType);

            if (_basicSoldierConf != null)
            {
                _arrayLengh = _barrackConf.gs_spawnSpotNum;
                _spawnArr = new SoldierConf[_arrayLengh];
                _spawnTimeLeft = new float[_arrayLengh];
                _spawnEnable = new bool[_arrayLengh];
                _skillOfSpot = new uint[_arrayLengh];
                _spawnTargetPos = new Vector3[_arrayLengh];
                _spawnTargetPercent = new float[_arrayLengh];
                _spawnResWait = new object[_arrayLengh];

                //add default spawn spot
                for (int i = 0; i < _arrayLengh; i++)
                {
                    _spawnArr[i] = _basicSoldierConf;
                    _spawnTimeLeft[i] = _basicSoldierConf.p_spawnTimeInCycle;
                    _spawnEnable[i] = true;
                    _skillOfSpot[i] = 0; //not has skill
                    _spawnTargetPos[i].x = _transform.position.x + 5; //默认士兵的集合点在兵营的x方向+5的位置，y是通过minimap设置的
                    _spawnResWait[i] = null;
                }
            }
            else
            {
                GameLogger.LogError($"Init {_troopType} barrack failed, because fail to get the {_troopType} basic soldier conf");
                return false;
            }

            return true;
        }

        //percent:整个地图高度的比例
        public void SetSpawnTargetPos(float percent, int index)
        {
            _spawnTargetPercent[index] = percent;
            float height = WarMapCtrl.Instance.GetMapByIndex(WE.OnGroundMapIndex).gs_passablePart.size.y;
            Vector2 center = WarMapCtrl.Instance.GetMapByIndex(WE.OnGroundMapIndex).gs_passablePart.center; //地图的中心点
            _spawnTargetPos[index].y = height * _spawnTargetPercent[index] - height / 2 + center.y;
        }

        //增加一个新的生产位
        public virtual int AddNewSpawnSpot()
        {
            int length = _arrayLengh;
            if (length < WBD.MaxSpawnSpotNum)
            {
                lock (_spawnLock)
                {
                    Array.Resize(ref _spawnArr, length + 1);
                    Array.Resize(ref _spawnTimeLeft, length + 1);
                    Array.Resize(ref _spawnEnable, length + 1);
                    Array.Resize(ref _skillOfSpot, length + 1);
                    Array.Resize(ref _spawnTargetPercent, length + 1);
                    Array.Resize(ref _spawnTargetPos, length + 1);
                    Array.Resize(ref _spawnResWait, length + 1);
                    if (_basicSoldierConf != null)
                    {
                        _spawnArr[length] = _basicSoldierConf;
                        _spawnTimeLeft[length] = _basicSoldierConf.p_spawnTimeInCycle;
                        _spawnEnable[length] = true;
                        _skillOfSpot[length] = 0; //not has skill
                        _spawnTargetPos[length].x = _transform.position.x + 5; //默认士兵的集合点在兵营的x方向+5的位置，y是通过minimap设置的
                        _spawnResWait[length] = null;
                        SetSpawnTargetPos(0.5f, length);
                    }

                    _arrayLengh++;
                }
                //更新生产信息到minimap
                UiMiniMapCtrl.Instance.AddSdToMiniMap(_troopType, length, _spawnArr[length], WE.OnGroundMapIndex);

                return length;
            }

            return -1;
        }

        public override void StartWork()
        {
            base.StartWork();
            //消耗资源开始第一次生产
            for (int i = 0; i < _arrayLengh; i++)
            {
                if (WarResCtrl.Instance.ConsumeRes(WRD.ResTypes.GOLDCOIN, _spawnArr[i].p_price, true, this, out var index) == false)
                    _spawnResWait[i] = index;
            }
        }

        //upgrade from basic type to some high level type
        //spotIndex start from 0
        public bool UpgradeSpawnSpot(int spotIndex, int sdType)
        {
            if (spotIndex > _arrayLengh - 1)
            {
                GameLogger.LogError($"Not has a spawn spot {spotIndex} for barrack {_troopType}, spot index start from 0");
                return false;
            }

            if (_spawnArr[spotIndex].p_level != SD.SoldierLevel.BASICLEVEL)
            {
                GameLogger.LogError($"Alread the high level can not upgrade {spotIndex} for barrack {_troopType}");
                return false;
            }

            if (SoldierCtrl.Instance.IsSoldierAvailable(_bdConf.gs_race, _troopType, sdType) == false)
            {
                 GameLogger.LogError($"{_troopType} Can not upgrade soldier to {sdType}, it is not available");
                 return false;
            }

            SoldierConf sd = SoldierCtrl.Instance.GetSdConf(WE.RaceType.Human, _troopType, sdType);
            if (sd == null || sd.p_level != SD.SoldierLevel.HIGHLEVEL)
            {
                GameLogger.LogError($"Wrong soldier type for upgrade {sdType} for barrack {_troopType}");
                return false;
            }

            GameLogger.LogInfo($"{_troopType} spawn spot {spotIndex} soldier upgrade to {sdType}");
            int prvPrice = _spawnArr[spotIndex].p_price;
            lock (_spawnLock)
            {
                _spawnArr[spotIndex] = sd;
                _spawnEnable[spotIndex] = true;
                _spawnTimeLeft[spotIndex] = sd.p_spawnTimeInCycle;
                _skillOfSpot[spotIndex] = 0; //not has skill
                if (_spawnResWait[spotIndex] != null) //如果有等待资源的话，取消等待
                {
                    WarResCtrl.Instance.NotWaitRes(_spawnResWait[spotIndex], WRD.ResTypes.GOLDCOIN);
                    _spawnResWait[spotIndex] = null;
                }
                else
                {
                    WarResCtrl.Instance.AddRes(WRD.ResTypes.GOLDCOIN, prvPrice); //归还之前的资源消耗
                }
            }

            //更新生产信息到minimap
            UiMiniMapCtrl.Instance.UpdateSpawnSpot(_troopType, spotIndex, _spawnArr[spotIndex]);

            return true;
        }

        //from high level down grade to basic type
        //spotIndex start from 0
        public bool DowngradeSpawnSpot(int spotIndex)
        {
            if (spotIndex > _arrayLengh - 1)
            {
                GameLogger.LogError($"Not has a spawn spot {spotIndex} for barrack {_troopType}");
                return false;
            }

            if (_spawnArr[spotIndex].p_level != SD.SoldierLevel.HIGHLEVEL)
            {
                GameLogger.LogError($"Alread the high level can not upgrade {spotIndex} for barrack {_troopType}");
                return false;
            }

            GameLogger.LogInfo($"{_troopType} {spotIndex} soldier downgrade");
            lock (_spawnLock)
            {
                _spawnArr[spotIndex] = _basicSoldierConf;
                _spawnEnable[spotIndex] = true;
                _spawnTimeLeft[spotIndex] = _basicSoldierConf.p_spawnTimeInCycle;
                _skillOfSpot[spotIndex] = 0; //not has skill
                if (_spawnResWait[spotIndex] != null)
                {
                    WarResCtrl.Instance.NotWaitRes(_spawnResWait[spotIndex], WRD.ResTypes.GOLDCOIN);
                    _spawnResWait[spotIndex] = null;
                }
            }

            return true;
        }

        //change a high level soldier's skill
        //spotIndex start from 0
        public bool ChangeSkill(int spotIndex, uint skillType)
        {
            if (spotIndex > _arrayLengh - 1)
            {
                GameLogger.LogError($"Not has a spawn spot {spotIndex} for barrack {_troopType}");
                return false;
            }

            if (_spawnArr[spotIndex].p_level != SD.SoldierLevel.HIGHLEVEL)
            {
                GameLogger.LogError($"Alread the high level can not upgrade {spotIndex} for barrack {_troopType}");
                return false;
            }

            GameLogger.LogInfo($"soldier at spawn spot {spotIndex} change skill from {_skillOfSpot[spotIndex]} to {skillType}");
            lock (_spawnLock)
                _skillOfSpot[spotIndex] = skillType;
            return true;
        }

        //spot start or stop spawn by user controled from ui
        public bool ChangeSpawnActivity(int spotIndex, bool isSpawn)
        {
            if (spotIndex > _arrayLengh - 1)
            {
                GameLogger.LogError($"Not has a spawn spot {spotIndex} for barrack {_troopType}");
                return false;
            }

            if (_spawnEnable[spotIndex] == isSpawn)
            {
                GameLogger.LogWarning($"Spawn spot {spotIndex} is already at spwan status {isSpawn}");
                return true;
            }

            GameLogger.LogInfo($"{_troopType} barrack spawn spot {spotIndex} change spawn status to {isSpawn}");
            _spawnEnable[spotIndex] = isSpawn;
            return true;
        }

        //ISpawnSoldier api
        public bool SpawnSoldierByIndex(int index)
        {
            if (_inTest == true)
            {
                if (_dbgProduceCnt <= 0)
                    return false;
                _dbgProduceCnt--;
            }

            int prodNum = 1;
            if (_barrackConf.gs_doubleChance > 0) //需要判断是否能同时生产出多个士兵
            {
                int chance = Utils.GetRandomInt();
                if (chance < _barrackConf.gs_quadrupleChance)
                    prodNum = 4;
                else
                {
                    chance = Utils.GetRandomInt(); //新获取一个随机数，要不然就是变相降低了3倍的概率
                    if (chance < _barrackConf.gs_tripleChance)
                        prodNum = 3;
                    else
                    {
                        chance = Utils.GetRandomInt(); //新获取一个随机数，要不然就是变相降低了2倍的概率
                        if (chance < _barrackConf.gs_doubleChance)
                            prodNum = 2;
                    }
                }
            }

            for (int i = 0; i < prodNum; i++)
            {
                int priceDownChance = Utils.GetRandomInt();
                int price = _spawnArr[index].p_price;
                if (priceDownChance < _barrackConf.gs_spawnPriceDownChance)
                    price = (int)Mathf.Round(price * _barrackConf.gs_spawnPriceDown);
                //make sure the soldier pic showed in front of the barrack
                float seedY = UnityEngine.Random.Range(-0.5f, 0.5f);
                Vector3 seedPos = new Vector3(0, seedY, 0);
                Vector2 pos = _transform.position + seedPos;
                Soldier sd = SoldierCtrl.Instance.AddSoldierAt(_spawnArr[index].p_race, _spawnArr[index].p_troop, _spawnArr[index].p_soldierType,
                    pos, _mapId);
                if (ReferenceEquals(sd, null) == true)
                {
                    GameLogger.LogError(
                        $"{_spawnArr[index].p_race} {_spawnArr[index].p_troop} {_spawnArr[index].p_soldierType}, can not get soldier prefab");
                    return false;
                }

                ((FriendlySoldier)sd).InitSoldier(_skillOfSpot[index], index, _mapId, _spawnTargetPos[index] + seedPos);
                SoldierCtrl.Instance.NotifySoldierProduction(_faction, _troopType, sd, pos);
            }

            return true;
        }

        //ISpawnSoldier api
        public bool SpawnSoldier(int race, int troopT, int soldierT, uint skillType)
        {
            throw new NotImplementedException();
        }

        //IResWaiter api
        //resource ready
        public bool ResReady(object indexer, WarResDefine.ResTypes type, int amount)
        {
            for (int i = 0; i < _arrayLengh; i++)
            {
                if (_spawnResWait[i] == indexer)
                {
                    _spawnResWait[i] = null;
                    return true;
                }
            }

            return false;
        }

        // public override void UserSelected(bool firstTime)
        // {
        //     if (firstTime == true)
        //         UIBuildingManipulateBar.Instance.ShowBuildingManipulateBar(this); //显示ManipulateBar
        // }

#endregion

#region private functions

        protected override void OnBdWork()
        {
            lock (_spawnLock)
            {
                for (int i = 0; i < _arrayLengh; i++)
                {
                    if (_spawnEnable[i] == false)
                        continue;
                    if (_spawnResWait[i] != null) //没有获取足够的资源
                        continue;
                    _spawnTimeLeft[i]--;
                    if (_spawnTimeLeft[i] <= 0)
                    {
                        if (((ISpawnSoldier)this).SpawnSoldierByIndex(i) == true)
                        {
                            bool timeDown = false;
                            if (_barrackConf.gs_spwawnTimeDown < 1) //缩短下一次的生产间隔
                            {
                                int chance = Utils.GetRandomInt();
                                if (chance <= _barrackConf.gs_spwawnTimeDownChance)
                                    timeDown = true;
                            }

                            if (timeDown == false)
                                _spawnTimeLeft[i] += _spawnArr[i].p_spawnTimeInCycle;
                            else
                                _spawnTimeLeft[i] += _spawnArr[i].p_spawnTimeInCycle * _barrackConf.gs_spwawnTimeDown;

                            //计算下次生产需要的资源
                            int price = _spawnArr[i].p_price;
                            if (_barrackConf.gs_spawnPriceDown < 1)
                            {
                                int chance = Utils.GetRandomInt();
                                if (chance <= _barrackConf.gs_spawnPriceDownChance)
                                    price = (int)(price * _barrackConf.gs_spawnPriceDown);
                            }

                            //获取下一次生产的所需的，第一次生产士兵的资源是在StartWork
                            if (WarResCtrl.Instance.ConsumeRes(WRD.ResTypes.GOLDCOIN, price, true, this, out var indexer) == false)
                                _spawnResWait[i] = indexer;
                        }
                    }
                }
            }
        }

        //不会真的被删除，进入摧毁状态等待修复
        protected override void BdDestroy()
        {
            _isDestroyed = true;
            _canWork = false;
            gameObject.SetActive(false);
        }
#endregion
    }
}

