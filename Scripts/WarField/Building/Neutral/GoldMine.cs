using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WarField
{
    using WBD = WarBuildingDefines;
    using WE = WarFieldElements;
    using WRD = WarResDefine;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //金矿
    public class GoldMine : WarBuilding, INeedBeProtect
    {
#region public parameters

#endregion

#region private parameters
        [SerializeField] protected GoldMineConf _goldMineConf = null; //just the _bdConf

        private List<IProtector> _protectors;
        private DataPool<GameObject>[] _targetInRange;  //[WE.FactionType]

        private WBD.OccupyStatus _occupyStatus;
        private float _occupyCycle, _occupyCycleMax;
        private bool _isSelled; //被出售

        //cover
        private Transform _cover;
        private MaterialPropertyBlock _coverRadarMat, _coverMat;
        private static int _radarX0Propoty = 0; //radar的x0变量
        private SpriteRenderer _coverRadarSprite, _coverSprite;

        private object _listLock;

        private float _goldPerSec = 0; //特地定义为float,为了计算更加精确
#endregion

#region private parameters' get set
        public bool gs_isOcuppied //获取占领状态，只有占领和未占领两种
        {
            get
            {
                if(_occupyStatus == WBD.OccupyStatus.OCCUPIED)
                    return true;
                return false;
            }
        }
#endregion

#region Unity callbacks
        protected override void Awake()
        {
            base.Awake();
            _targetInRange = new DataPool<GameObject>[(int)WE.FactionType.MAX];
            for (int i = 1; i < (int)WE.FactionType.MAX; i++)
            {
                _targetInRange[i] = new DataPool<GameObject>(false); //不加锁，使用独立的锁_listLock
            }

            _listLock = new object();
            _protectors = new List<IProtector>();

            _coverMat = new MaterialPropertyBlock();
            _coverRadarMat = new MaterialPropertyBlock();
        }

        //与geoldmine的本体相撞，只有protector才会碰撞上
        private void OnCollisionEnter2D(Collision2D other)
        {
            string tag = other.gameObject.tag;
            WE.WarEleType colType = WarFieldUtil.GetWarEleType(tag);
            if (colType == WE.WarEleType.SOLDIER)
            {
                Soldier sd = other.gameObject.GetComponent<Soldier>();
                if(_protectors.Contains(sd) == true)
                    sd.ReachProtectTarget();
            }
        }

#endregion

#region public functions
        public virtual bool InitBuilding(BuildingConf conf, byte mapId)
        {
            _bdConf = new GoldMineConf(conf);
            if(base.InitBuilding(mapId) == false)
                return false;

            _isSelled = false;
            SetOccupyStatus(WBD.OccupyStatus.NOOCCUPY);
            _goldMineConf = _bdConf as GoldMineConf;
            _rangeCollider.radius = _goldMineConf.gs_range;
            _occupyCycleMax = _occupyCycle = Utils.CountOfFixUpdate(_goldMineConf.gs_occupyTime);

            //set cover
            _cover = _transform.Find("Cover");
            _cover.localScale = new Vector3(_goldMineConf.gs_range * 2, _goldMineConf.gs_range * 2, 1);
            _coverSprite = _transform.Find("Cover/CoverRange").GetComponent<SpriteRenderer>();
            _coverSprite.GetPropertyBlock(_coverMat);
            _coverMat.SetColor("_Color", WE.CoverColorDict["Green"]);
            _coverSprite.SetPropertyBlock(_coverMat);

            _coverRadarSprite = _transform.Find("Cover/Radar").GetComponent<SpriteRenderer>();
            _coverRadarSprite.GetPropertyBlock(_coverRadarMat);
            _coverRadarMat.SetColor("_Color", WE.CoverColorDict["Green"]);
            _coverRadarSprite.SetPropertyBlock(_coverRadarMat);

            _cover.gameObject.SetActive(true);
            if(_radarX0Propoty == 0)
                _radarX0Propoty = Shader.PropertyToID("_x0");
            _bdSpriteRenderer.color = Color.gray;

            //add protector
            var positions = GenerateProtectorPos(_transform.position, ((CircleCollider2D)_bodyCollider).radius, 0.5f, 2);

            Golem golem = (Golem)SoldierCtrl.Instance.AddSoldierAt(WE.RaceType.Neutral, SD.TroopType.Melee, (int)NeutralDefines.MeleeType.GOLEM,
                positions[0], _mapId);
            golem.InitSoldier(WE.FactionType.ENEMY, -1, _mapId);
            golem.BecomeProtector(this, _warEleType, positions[0]);
            _protectors.Add(golem);

            return true;
        }

        //因为是NEUTRAL类型的，Friendly/Enemy都是是rival
        public override void RivalInRange(GameObject colTarget, WE.WarEleType colType, WE.FactionType faction)
        {
            if(_isSelled == true)
                return;

            if(colType == WE.WarEleType.BUILDING)
                return;

            AddObjIntoList(colTarget, faction);

            if(faction == WE.FactionType.ENEMY)
            {
                switch (_occupyStatus)
                {
                    case WBD.OccupyStatus.OCCUPYING:
                    case WBD.OccupyStatus.OCCUPIED:
                        SetOccupyStatus(WBD.OccupyStatus.NOOCCUPY);
                        break;
                    default:
                        break;
                }
            }
        }

        public override void RivalOutRange(GameObject colTarget, WE.WarEleType colType, WE.FactionType faction)
        {
            if(_isSelled == true)
                return;

            if(colType == WE.WarEleType.BUILDING)
                return;

            DelObjFromList(colTarget, faction);
        }

        //出售
        public void SellMine()
        {
            if(_isSelled == true)
                return;
            WarResCtrl.Instance.GoldIncomePerSecChange(_goldMineConf.gs_goldAddPerSec, GD.CalDeltaType.SUB); //增加工资
            WarResCtrl.Instance.AddRes(WarResDefine.ResTypes.GOLDCOIN, _goldMineConf.gs_sellPrice);
            BdDestroy();
        }

        //查询入侵者
        public WarEleParent GetInvader(out WarFieldElements.WarEleType type)
        {
            type = WE.WarEleType.MIN;
            if (_occupyStatus != WBD.OccupyStatus.OCCUPIED)
            {
                if (_targetInRange[(int)WE.FactionType.FRIENDLY].Count > 0) //没有被占领的时敌人是人族
                {
                    WarEleParent ele = _targetInRange[(int)WE.FactionType.FRIENDLY].GetByIndex(0).GetComponent<WarEleParent>();
                    type = ele.gs_warEleType;
                    return ele;
                }
            }
            else
            {
                if (_targetInRange[(int)WE.FactionType.ENEMY].Count > 0) //占领之后其他种族是敌人
                {
                    WarEleParent ele = _targetInRange[(int)WE.FactionType.ENEMY].GetByIndex(0).GetComponent<WarEleParent>();
                    type = ele.gs_warEleType;
                    return ele;
                }
            }
            return null;
        }

        public void ProtectorDie(IProtector protector)
        {
            _protectors.Remove(protector);
        }

#endregion

#region private functions
        protected override void OnBdWork(float deltaTime)
        {
            if(_isSelled == true)
                return;

            switch (_occupyStatus)
            {
                case WBD.OccupyStatus.NOOCCUPY:
                    if (_protectors.Count == 0)
                    {
                        lock (_listLock)
                        {
                            if (_targetInRange[(int)WE.FactionType.FRIENDLY].Count > 0 && _targetInRange[(int)WE.FactionType.ENEMY].Count == 0)
                                SetOccupyStatus(WBD.OccupyStatus.OCCUPYING);
                        }
                    }

                    break;
                case WBD.OccupyStatus.OCCUPYING:
                    if (_occupyCycle > 0)
                        _occupyCycle--;
                    else //成功占领
                    {
                        if (_mapId != WE.OnGroundMapIndex && WarMapCtrl.Instance.GetMapByIndex(_mapId).gs_isOccupy == true)
                            _goldPerSec = _goldMineConf.gs_goldAddPerSec * _goldMineConf.gs_caveProduceAdd; //洞穴被占领可能会有别的加成
                        else
                            _goldPerSec = _goldMineConf.gs_goldAddPerSec;

                        SetOccupyStatus(WBD.OccupyStatus.OCCUPIED);
                    }
                    break;
                case WBD.OccupyStatus.OCCUPIED:
                    break;
                default:
                    break;
            }
        }

        private void SetOccupyStatus(WBD.OccupyStatus value)
        {
            if(_occupyStatus == value)
                return;

            var prvStatus = _occupyStatus;
            _occupyStatus = value;
            if(_occupyStatus == WBD.OccupyStatus.OCCUPYING)
                _occupyCycle = _occupyCycleMax;
            else if(_occupyStatus == WBD.OccupyStatus.OCCUPIED)
            {
                _bdSpriteRenderer.color = Color.white;
                WarResCtrl.Instance.GoldIncomePerSecChange(Mathf.RoundToInt(_goldPerSec), GD.CalDeltaType.ADD); //增加工资
            }

            if (prvStatus == WBD.OccupyStatus.OCCUPIED)
            {
                _bdSpriteRenderer.color = Color.gray;
                WarResCtrl.Instance.GoldIncomePerSecChange(Mathf.RoundToInt(_goldPerSec), GD.CalDeltaType.SUB); //丢失金矿，减少工资
            }
        }

        private void AddObjIntoList(GameObject obj, WE.FactionType type)
        {
            lock (_listLock)
            {
                _targetInRange[(int)type].AddItem(obj);
            }
        }

        private void DelObjFromList(GameObject obj, WE.FactionType type)
        {
            lock (_listLock)
            {
                _targetInRange[(int)type].RemoveItem(obj);
            }
        }

        private List<Vector2> GenerateProtectorPos(Vector2 center, float r, float d, int count)
        {
            List<Vector2> points = new List<Vector2>();

            float rMin = Mathf.Max(0f, r - d);
            float rMax = r + d;

            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(0f, 2f * Mathf.PI);

                // 按面积均匀分布（平方再开方）
                float radius = Mathf.Sqrt(Random.Range(rMin * rMin, rMax * rMax));

                Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                points.Add(point);
            }

            return points;
        }

        protected override void OnConfUpgradeNotification(string changeName, float oriValue)
        {
            if(_occupyStatus != WBD.OccupyStatus.OCCUPIED)
                return;

            //在占领状态下提升金矿的每秒生产能力
            if (changeName == "goldAddPerSec" || changeName == "caveProduceAdd")
            {
                float goldAddPerSec;
                if (_mapId != WE.OnGroundMapIndex && WarMapCtrl.Instance.GetMapByIndex(_mapId).gs_isOccupy == true)
                    goldAddPerSec = _goldMineConf.gs_goldAddPerSec * _goldMineConf.gs_caveProduceAdd; //洞穴被占领可能会有别的加成
                else
                    goldAddPerSec = _goldMineConf.gs_goldAddPerSec;
                float delta = goldAddPerSec - _goldPerSec;
                WarResCtrl.Instance.GoldIncomePerSecChange((int)delta, GD.CalDeltaType.ADD);
            }
        }

#endregion
    }
}

