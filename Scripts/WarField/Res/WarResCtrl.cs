using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using UnityEngine;

namespace WarField
{
    using WRD = WarResDefine;
    using SD = SoldierDefines;
    using WE = WarFieldElements;
    using GD = GlobalDefines;

    public class WarResCtrl : MonoBehaviour, ITask
    {
#region public parameters

        public static WarResCtrl Instance;

#endregion

#region private parameters

        private class ResWait
        {
            public WRD.ResTypes p_waitResType;
            public int p_waitAmount; //需求的总量
            public int p_resRemain; //还需的资源数量
            public IResWaiter p_waiter;
        }

        [SerializeField] private Sprite[] _stoneSprites; //[WRD.ResContainLevel] 不同等级stone的图片
        [SerializeField] private float _taskInterval = 0.5f;  //RunNormalTask执行间隔时间, 也是时间轮的每个时间片的时长

        // 高级时间轮配置
        private const int _timeWheelLength = 64;        // 时间轮的槽数,就是时间轮的长度
        private const int _timeWheelMask = 63;          // 掩码：用于替代 % 64 的极速位运算

        private List<OcularStone>[] _timeWheel;     // 环形数组
        private int _currentWheelIndex = 0;         // 表针当前位置

        private WarResInStoreBase[] _warResArray; //存储的资源

        //将res的数量缓存下来，避免太频繁的调用IWarResListener,只有每次update的时候才通知
        private int[] _resAmount;
        private int[] _resDelta;
        private System.Object[] _resLock;

        private int _goldGenPerSec; //金钱每秒增长
        private int _passGemAward; //通关宝石奖励

        //资源不足时，等待队列
        private List<ResWait>[] _resWaitList; //[WRD.ResTypes]

        //ocular stone
        private List<OcularStone> _ocularStonePool;
        private int _ocularStoneGenerateChance; //生成曈石的概率
        private int[] _basicSdGenerateChance, _highSdGenerateChance, _rareSdGenerateChance; //各个等级的士兵死亡时生成不同等级曈石的概率

        private GameObject _ocularStonePfb;
        //不同丰度曈石中蕴含的能量数量
        private int[] _energyInStone;
        private float _ocularStoneTimeOut;

        //等待资源的队列
        private Stack<ResWait> _resWaitPool;

        private object _glodIncomeChangeLock = new object();
        private int _loopCnt;
        private bool _beInited;
#endregion

#region private parameters' get set

        public int gs_passGemAward
        {
            get { return _passGemAward; }
        }

        public Sprite[] gs_stoneSprites
        {
            get { return _stoneSprites; }
        }
#endregion

#region Unity callbacks

        private void Awake()
        {
            if (ReferenceEquals(Instance, null) == false)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _warResArray = new WarResInStoreBase[(int)WRD.ResTypes.MAX];
            _resAmount = new int[(int)WRD.ResTypes.MAX];
            _resDelta = new int[(int)WRD.ResTypes.MAX];
            _resLock = new object[(int)WRD.ResTypes.MAX];

            _resWaitList = new List<ResWait>[(int)WRD.ResTypes.MAX];
            for (int i = 1; i < _warResArray.Length; i++)
            {
                _resWaitList[i] = new List<ResWait>(16);
                _resLock[i] = new object();
            }

            _ocularStonePool = new List<OcularStone>(128);

            // 初始化时间轮，预分配 64 个槽，永不产生 GC
            _timeWheel = new List<OcularStone>[_timeWheelLength];
            for (int i = 0; i < _timeWheelLength; i++)
                _timeWheel[i] = new List<OcularStone>(16);
            _basicSdGenerateChance = new int[(int)WRD.ResContainLevel.MAX];
            _highSdGenerateChance = new int[(int)WRD.ResContainLevel.MAX];
            _rareSdGenerateChance = new int[(int)WRD.ResContainLevel.MAX];

            _ocularStonePfb = Resources.Load<GameObject>("Prefabs/ResPf/OcularStone");
            _energyInStone = new int[(int)WRD.ResContainLevel.MAX];

            _resWaitPool = new Stack<ResWait>(64);

            _beInited = false;
        }
#endregion

#region public functions

        public void RunNormalTask(float deltaTime)
        {
            if (!_beInited)
                return;
            UpdateRes();
        }

        public bool InitWarRes()
        {
            if(_beInited == true)
                return false;

            _warResArray[(int)WRD.ResTypes.GOLDCOIN] = new GoldCoinInStore();
            _warResArray[(int)WRD.ResTypes.OCULARSTONE] = new OcularStoneInStore();
            _warResArray[(int)WRD.ResTypes.GEM] = new GemInStore();

            for (int i = 1; i < (int)WRD.ResTypes.MAX; i++)
            {
                _resAmount[i] = _warResArray[(int)WRD.ResTypes.GOLDCOIN].gs_total;
                _resDelta[i] = 0;
            }
            ReadConf();  //添加初始资金
            _loopCnt = 2;

            _beInited = true;
            WarFieldGameManager.Instance.RegisterTask(this, WE.TaskType.NORMAL);
            WarFieldGameManager.Instance.ActiveTask(this, WE.TaskType.NORMAL, _taskInterval);
            return true;
        }

        public int GetResInStore(WRD.ResTypes resType)
        {
            return _warResArray[(int)resType].gs_total;
        }

        //监听资源变化
        public void RegisterResListener(WRD.ResTypes type, IWarResListener listener)
        {
            if(_beInited == false)
                return;
            _warResArray[(int)type].RegisterListener(listener);
        }

        public void RemoveResListener(WRD.ResTypes type, IWarResListener listener)
        {
            if(_beInited == false)
                return;
            _warResArray[(int)type].RemoveListener(listener);
        }

        //增加res，amount必须>0
        public bool AddRes(WRD.ResTypes type, int amount)
        {
            if(_beInited == false)
                return false;

            if (amount < 0)
            {
                GameLogger.LogError($"Can not add negative amount of res {type} {amount}");
                return false;
            }

            lock (_resLock[(int)type])
            {
                ChangeResTmp((int)type, amount);
            }

            return true;
        }

        //消耗res，amount必须>0
        //waitForRes: true 等待资源ready， false：不等待
        public bool ConsumeRes(WRD.ResTypes type, int amount, bool waitForRes, IResWaiter waiter, out object indexer)
        {
            indexer = null;
            if(_beInited == false)
                return false;

            if (amount < 0)
            {
                GameLogger.LogError($"Can not consume negative amount of res {type} {amount}");
                return false;
            }

            lock (_resLock[(int)type])
            {
                //not has enough res
                if (amount > _resAmount[(int)type])
                {
                    if (waitForRes == true)
                    {
                        ResWait wait;
                        wait = _resWaitPool.Count > 0 ? _resWaitPool.Pop() : new ResWait();
                        wait.p_waitResType = type;
                        wait.p_waitAmount = amount;
                        wait.p_waiter = waiter;
                        wait.p_resRemain = amount - _resAmount[(int)type];//获取到剩余的所有资源

                        ChangeResTmp((int)type, -_resAmount[(int)type]);
                        _resWaitList[(int)type].Add(wait);
                        indexer = (object)wait;
                    }
                    return false;
                }
                else
                    ChangeResTmp((int)type, -amount);
            }

            return true;
        }

        //停止等待资源
        public void NotWaitRes(object indexer, WRD.ResTypes type)
        {
            ResWait waiter = (ResWait)indexer;
            lock (_resLock[(int)type])
            {
                var list = _resWaitList[(int)type];
                list.Remove(waiter);
                waiter.p_waiter = null;
                if(waiter.p_resRemain > 0)
                    ChangeResTmp((int)type, waiter.p_waitAmount - waiter.p_resRemain); //归还资源
                _resWaitPool.Push(waiter); //回收到内存池
            }
        }

        //每秒收入变化
        //占领/丢失一个金矿,incomeChange:每秒收入改变
        public void GoldIncomePerSecChange(int incomeChange, GD.CalDeltaType calType)
        {
            lock (_glodIncomeChangeLock)
            {
                int value = (int)Utils.CalDeltaValue(_goldGenPerSec, incomeChange, calType);
                if (value < 0)
                {
                    GameLogger.LogError("Gold per second can not < 0");
                    return;
                }
                _goldGenPerSec = value;
            }
            GameLogger.LogInfo($"Gold salary per second change to {_goldGenPerSec}");
        }

        //占领宝石矿之后收获宝石矿
        public void OccupyGemMine(int gemIncome)
        {
            AddRes(WRD.ResTypes.GEM, gemIncome);
        }

        //敌方士兵死亡可能产生曈石
        public bool AddOcularStoneAt(SD.SoldierLevel sdLevel, Vector2 pos, int mapId)
        {
            int chance = Utils.GetRandomInt();
            // if(chance > _ocularStoneGenerateChance)
            //     return false;

            WRD.ResContainLevel resContainLevel = WRD.ResContainLevel.MIN;
            int[] list = null;
            switch (sdLevel)
            {
                case SD.SoldierLevel.BASICLEVEL:
                    list = _basicSdGenerateChance;
                    break;
                case SD.SoldierLevel.HIGHLEVEL:
                    list = _highSdGenerateChance;
                    break;
                case SD.SoldierLevel.RARELEVEL:
                    list = _rareSdGenerateChance;
                    break;
                case SD.SoldierLevel.BOSSLEVEL:
                    return true; //敌方boss就不产生曈石了
                default:
                    return false;
            }

            chance = Utils.GetRandomInt();
            int cnt = list.Length;
            for (int i = 1; i < cnt; i++)
            {
                if (chance < list[i])
                {
                    resContainLevel = (WRD.ResContainLevel)i;
                    break;
                }
            }

            OcularStone stone = TakeOcularStoneFromPool();
            stone.transform.position = pos;
            stone.InitOcularStone(resContainLevel, pos, mapId, _energyInStone[(int)resContainLevel]);

            // 时间轮落槽与圈数计算
            int ticksNeeded = Mathf.CeilToInt(_ocularStoneTimeOut / _taskInterval);
            int targetSlot = (_currentWheelIndex + ticksNeeded) & _timeWheelMask;

            stone.p_timeWheelLap = ticksNeeded / _timeWheelLength;
            _timeWheel[targetSlot].Add(stone);
            return true;
        }

        //ReleaseOcularStoneToPool只能在TimeOut调用，
        //isTaken：是否被hero采集
        public void ReleaseOcularStoneToPool(OcularStone stone)
        {
            _ocularStonePool.Add(stone);
        }

        //升级
        public bool ApplyImprovement(WE.ImproveSrc src, WRD.ResTypes resType, string improveName, float improveValue, GD.CalDeltaType improveCal)
        {
            if (src == WE.ImproveSrc.FROMUPGRADE || src == WE.ImproveSrc.FROMCARD)
            {
                switch (improveName)
                {
                    case "energyInLowStone":
                        _energyInStone[(int)WRD.ResContainLevel.LOW] =
                            (int)Utils.CalDeltaValue(_energyInStone[(int)WRD.ResContainLevel.LOW], improveValue, improveCal);
                        break;
                    case "energyInMidStone":
                        _energyInStone[(int)WRD.ResContainLevel.MID] =
                            (int)Utils.CalDeltaValue(_energyInStone[(int)WRD.ResContainLevel.MID], improveValue, improveCal);
                        break;
                    case "energyInHighStone":
                        _energyInStone[(int)WRD.ResContainLevel.HIGH] =
                            (int)Utils.CalDeltaValue(_energyInStone[(int)WRD.ResContainLevel.HIGH], improveValue, improveCal);
                        break;
                    default:
                        return false;
                }
                return true;
            }
            else if (src == WE.ImproveSrc.FROMPROP)
            {
                return true;
            }
            return false;
        }

        public void RunFixTask(float deltaTime)
        {
            throw new NotImplementedException();
        }
#endregion

#region private functions
        //修改临时值，并不会直接修改到warresbase去
        //value在减少时 < 0
        private int ChangeResTmp(int index, int value)
        {
            _resAmount[index] += value;
            _resDelta[index] += value;

            return _resAmount[index];
        }

        private void ReadConf()
        {
            XmlDocument xmlDocument = Utils.LoadXmlFile($"Conf/Res/ResConf");
            if (xmlDocument == null)
                return;

            XmlNodeList nodeList = xmlDocument.SelectSingleNode("resConf").ChildNodes;
            int cnt = nodeList.Count;
            for (int i = 0; i < cnt; i++)
            {
                if (nodeList[i].NodeType == XmlNodeType.Comment)
                    continue;

                XmlElement tmp = (XmlElement)nodeList[i];
                switch (tmp.Name)
                {
                    case "startGold": //添加初始资金
                    {
                        int value = int.Parse(tmp.GetAttribute("value"));
                        _resAmount[(int)WRD.ResTypes.GOLDCOIN] += value;
                        _resDelta[(int)WRD.ResTypes.GOLDCOIN] += value;
                    }
                        break;
                    case "goldGenPerSec":
                        _goldGenPerSec = int.Parse(tmp.GetAttribute("value"));
                        break;
                    case "gemAward":
                        _passGemAward = int.Parse(tmp.GetAttribute("value"));
                        break;
                    case "ocularStoneGenerateChance":
                        _ocularStoneGenerateChance = int.Parse(tmp.GetAttribute("value"));
                        break;
                    case "energyInStone":
                    {
                        int[] energy = tmp.GetAttribute("value").Split(',').Select(x => int.Parse(x.Trim())).ToArray();
                        for (int j = 1; j < _energyInStone.Length; j++)
                            _energyInStone[j] = energy[j - 1];
                    }
                        break;
                    case "ocularStoneTimeOut":
                        _ocularStoneTimeOut = float.Parse(tmp.GetAttribute("value"));
                        break;
                    case "sdGenerateChance":
                    {
                        int sdLevel = int.Parse(tmp.GetAttribute("sdlevel"));
                        int[] gemChance = tmp.GetAttribute("gemChance").Split(',').Select(x => int.Parse(x.Trim())).ToArray();
                        int[] list = null;
                        if (sdLevel == 1)
                            list = _basicSdGenerateChance;
                        else if (sdLevel == 2)
                            list = _highSdGenerateChance;
                        else if (sdLevel == 3)
                            list = _rareSdGenerateChance;
                        if (list != null)
                        {
                            for (int j = 1; j < list.Length; j++)
                            {
                                list[j] = list[j - 1] + gemChance[j - 1];
                            }
                        }
                    }
                        break;
                    default:
                        break;
                }
            }
        }

        private void UpdateRes()
        {
            // 1. 时间轮步进与 O(1) 批量清理
            List<OcularStone> slotList = _timeWheel[_currentWheelIndex];

            // 倒序遍历，配合 Swap-And-Pop 实现 0 GC 清理
            for (int i = slotList.Count - 1; i >= 0; i--)
            {
                var stone = slotList[i];

                if (stone.p_timeWheelLap > 0)
                {
                    // 圈数没到，减一圈
                    stone.p_timeWheelLap--;
                }
                else
                {
                    // 圈数到 0，执行销毁逻辑
                    stone.TimeOut();
                    // 不管是不是被taken,都是在这个时候冲
                    Utils.ExecuteSwapAndPop(slotList, i); //所有的timeout的stone (包括之前被taken,和正在被taken的)都在此时从时间轮中删除
                }
            }

            // 指针走一格 (绕回机制)
            _currentWheelIndex = (_currentWheelIndex + 1) & _timeWheelMask;
            // 金币周期性产出
            _loopCnt--;
            if (_loopCnt <= 0)
            {
                AddRes(WRD.ResTypes.GOLDCOIN, _goldGenPerSec);
                _loopCnt = 2; //一秒增长一次金钱
            }

            //目前只处理gold
            if(_resAmount[(int)WRD.ResTypes.GOLDCOIN] > 0)
            {
                lock (_resLock[(int)WRD.ResTypes.GOLDCOIN])
                {
                    var list = _resWaitList[(int)WRD.ResTypes.GOLDCOIN];
                    if (list.Count > 0)
                    {
                        int step = _resAmount[(int)WRD.ResTypes.GOLDCOIN] >> 1;

                        for (int i = 0; i < list.Count; )
                        {
                            if (step <= 0)
                                break;

                            var waitObj = list[i];
                            if (waitObj.p_resRemain <= _resAmount[(int)WRD.ResTypes.GOLDCOIN]) //资源已经够了,直接开始生产
                            {
                                if(waitObj.p_waiter.ResReady(null, WRD.ResTypes.GOLDCOIN, waitObj.p_waitAmount) == true)
                                    ChangeResTmp((int)WRD.ResTypes.GOLDCOIN, -waitObj.p_resRemain); //消耗资源
                                else //并没有消耗资源
                                    ChangeResTmp((int)WRD.ResTypes.GOLDCOIN, waitObj.p_waitAmount - waitObj.p_resRemain); //归还资源
                                list.RemoveAt(i);
                                waitObj.p_waiter = null;
                                _resWaitPool.Push(waitObj); //会收到内存池

                                step = _resAmount[(int)WRD.ResTypes.GOLDCOIN] >> 1;
                            }
                            else //资源已经够了,存下来step的资源
                            {
                                waitObj.p_resRemain -= step;
                                ChangeResTmp((int)WRD.ResTypes.GOLDCOIN, -step);
                                step >>= 1;
                                i++;
                            }
                        }
                    }
                    //如果_resAmount还有剩余就不处理了
                }
            }

            for (int i = 1; i < _warResArray.Length; i++)
            {
                if(_resDelta[i] == 0)
                    continue;

                _warResArray[i].AmountChange(_resDelta[i]);
                lock (_resLock[i])
                {
                    _resDelta[i] = 0;
                    _resAmount[i] = _warResArray[i].gs_total;
                }
            }
        }

        private OcularStone TakeOcularStoneFromPool()
        {
            int cnt = _ocularStonePool.Count;
            if (cnt > 0)
            {
                var ret = _ocularStonePool[cnt - 1];
                _ocularStonePool.RemoveAt(cnt - 1);
                ret.gameObject.SetActive(true); // 保证取出的是激活状态
                return ret;
            }

            var newObj = Instantiate(_ocularStonePfb, transform);
            newObj.SetActive(true);
            return newObj.GetComponent<OcularStone>();
        }
#endregion
    }
}

