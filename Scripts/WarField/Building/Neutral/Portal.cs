using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WarField
{
    using WE = WarFieldElements;
    using WBD = WarBuildingDefines;

    //传送点
    public class Portal : WarBuilding
    {
#region public parameters

#endregion

#region private parameters
        [SerializeField] private PortalConf _portalConf; //just the _bdConf

        private DataPool<GameObject>[] _targetInRange;  //[WE.FactionType]
        private WBD.OccupyStatus _occupyStatus;
        private float _occupyCycle, _occupyCycleMax;
        private bool _workable; //占领之后玩家选择是不是关闭传送,占领或者丢失并不会改变这个值

        private Portal _targetPortal;
        private Vector3 _targetPosOffset; //两个portal位置的差距
        private float _transmitCycle, _transmitCycleMax;

        //cover
        private Transform _cover;
        private MaterialPropertyBlock _coverRadarMat, _coverMat;
        private static int _radarX0Propoty = 0; //radar的x0变量
        private SpriteRenderer _coverRadarSprite, _coverSprite;

        //portal按照y从小到大的排序,_order越大说明是同一列中y越大
        [SerializeField] private int _order; //应该只有0，1，2对应与下中上3个位置的portal

        private object _listLock;
#endregion

#region private parameters' get set

        //占领之后玩家选择是不是关闭传送,占领或者丢失并不会改变这个值
        public bool gs_workable
        {
            get { return _workable; }
        }

        public bool gs_isOcuppied //获取占领状态，只有占领和未占领两种
        {
            get
            {
                if(_occupyStatus == WBD.OccupyStatus.OCCUPIED)
                    return true;
                return false;
            }
        }

        public Portal gs_targetPortal
        {
            get { return _targetPortal; }
        }

        public int gs_order
        {
            get { return _order; }
            set { _order = value; }
        }
#endregion

#region Unity callbacks
        protected override void Awake()
        {
            base.Awake();
            _targetInRange = new DataPool<GameObject>[(int)WE.FactionType.MAX];
            for (int i = 1; i < (int)WE.FactionType.MAX; i++)
            {
                _targetInRange[i] = new DataPool<GameObject>(false);
            }

            _listLock = new object();
            _coverMat = new MaterialPropertyBlock();
            _coverRadarMat = new MaterialPropertyBlock();
        }
#endregion

#region public functions

        public virtual bool InitBuilding(BuildingConf conf, byte mapId)
        {
            _bdConf = new PortalConf(conf);
            if(base.InitBuilding(mapId) == false)
                return false;

            _targetPortal = null;
            SetOccupyStatus(WBD.OccupyStatus.NOOCCUPY);
            _portalConf = _bdConf as PortalConf;
            _rangeCollider.radius = _portalConf.gs_range;
            _occupyCycleMax = _occupyCycle = Utils.CountOfFixUpdate(_portalConf.gs_occupyTime);
            _transmitCycle = _transmitCycleMax = Utils.CountOfFixUpdate(+_portalConf.gs_transmitInterval);

            //set cover
            _cover = _transform.Find("Cover");
            _cover.localScale = new Vector3(_portalConf.gs_range * 2, _portalConf.gs_range * 2, 1);
            _coverSprite = _transform.Find("Cover/CoverRange").GetComponent<SpriteRenderer>();
            _coverSprite.GetPropertyBlock(_coverMat);
            _coverMat.SetColor("_Color", WE.CoverColorDict["Green"]);
            _coverSprite.SetPropertyBlock(_coverMat);

            _coverRadarSprite = _transform.Find("Cover/Radar").GetComponent<SpriteRenderer>();
            _coverRadarSprite.GetPropertyBlock(_coverRadarMat);
            _coverRadarMat.SetColor("_Color", WE.CoverColorDict["Green"]);
            _coverRadarSprite.SetPropertyBlock(_coverRadarMat);

            _cover.gameObject.SetActive(false);
            if(_radarX0Propoty == 0)
                _radarX0Propoty = Shader.PropertyToID("_x0");
            _workable = true;//默认开启传送
            _bdSpriteRenderer.color = Color.gray;
            return true;
        }

        //因为portal本身是NEUTRAL类型的，Friendly是rival,
        public override void RivalInRange(GameObject colTarget, WE.WarEleType colType, WE.FactionType faction)
        {
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
            if(colType == WE.WarEleType.BUILDING)
                return;

            DelObjFromList(colTarget, faction);
        }

        public Portal Receive(GameObject passenger, ref Vector3 offset)
        {
            if (CanTransmit() == true) //中转
            {
                offset = _targetPosOffset + offset;
                return _targetPortal;
            }

            return null;//传送终点
        }

        //玩家从界面选择是否开启传送
        public void ChangeTransmitStatus(bool value)
        {
            if(_occupyStatus == WBD.OccupyStatus.OCCUPIED)
                _workable = value;
        }

        public void SetCoverRange(bool value)
        {
            _cover.gameObject.SetActive(value);
        }

        public void SetTargetPotal(Portal target)
        {
            _targetPortal = target;
            _targetPosOffset = target.gs_transform.position - _transform.position;
        }

        //能够接受士兵的传入
        public bool CanReceive()
        {
            if(_occupyStatus != WBD.OccupyStatus.OCCUPIED)
                return false;
            return true;
        }
#endregion

#region private functions

        protected override void OnBdWork(float deltaTime)
        {
            switch (_occupyStatus)
            {
                case WBD.OccupyStatus.NOOCCUPY:
                    lock (_listLock)
                    {
                        if (_targetInRange[(int)WE.FactionType.FRIENDLY].Count > 0 && _targetInRange[(int)WE.FactionType.ENEMY].Count == 0)
                            SetOccupyStatus(WBD.OccupyStatus.OCCUPYING);
                    }

                    break;
                case WBD.OccupyStatus.OCCUPYING:
                    if (_occupyCycle > 0)
                        _occupyCycle--;
                    else
                    {
                        SetOccupyStatus(WBD.OccupyStatus.OCCUPIED);
                        _transmitCycle = 0;
                    }
                    break;
                case WBD.OccupyStatus.OCCUPIED:
                    if (_workable == true)
                    {
                        if (_transmitCycle <= 0)
                        {
                            _transmitCycle = _transmitCycleMax;
                            DoTransmit();
                        }
                        else
                            _transmitCycle--;
                    }
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
                _bdSpriteRenderer.color = Color.white;

            if (prvStatus == WBD.OccupyStatus.OCCUPIED)
                _bdSpriteRenderer.color = Color.gray;
        }

        private void DoTransmit()
        {
            if(CanTransmit()== false)
                return;

            lock (_listLock)
            {
                var list = _targetInRange[(int)WE.FactionType.FRIENDLY];
                int cnt = list.Count;
                if (cnt == 0)
                    return;

                for (int i = 0; i < cnt; i++)
                {
                    Soldier soldier = list.GetByIndex(i).GetComponent<Soldier>(); //因为_targetInRange没有加锁，所以可以这样遍历
                    if (soldier.gs_isHero == false)
                        ((FriendlySoldier)soldier).StartTransport(_targetPortal, _targetPosOffset);
                }
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

        //是否能够传送士兵
        private bool CanTransmit()
        {
            if(_occupyStatus != WBD.OccupyStatus.OCCUPIED)
                return false;

            if(_workable == false)
                return false;

            if(ReferenceEquals(_targetPortal, null) == true)
                return false;

            return _targetPortal.CanReceive() != false;
        }

        protected override void BindToMiniMapObj()
        {
            base.BindToMiniMapObj();

            //增加覆盖范围的显示
            UIPortalTransportation portalTran = _miniMapObj.AddComponent<UIPortalTransportation>();
            UiMiniMapCtrl.Instance.GetMinimapByMapId(_mapId).gs_warFieldPanel.gs_warFieldMiniMap.AddPortalInMiniMapList(portalTran, this);
            //增加覆盖范围的显示
            GameObject cover = new GameObject("Cover", typeof(RectTransform));
            cover.transform.SetParent(_miniMapObjTransform, false);
            Image image = cover.AddComponent<Image>();
            image.sprite = UiMiniMapCtrl.Instance.gs_rangeCoverTex;
            //设置cover的大小
            RectTransform rt = cover.GetComponent<RectTransform>();
            float range = _portalConf.gs_range * Screen.height/ WarMapCtrl.Instance.GetMapByIndex(_mapId).gs_passablePart.size.y; //将世界坐标下的范围转成屏幕坐标下的范围
            rt.sizeDelta = new Vector2(_portalConf.gs_range * 2, _portalConf.gs_range * 2);
            rt.anchoredPosition = Vector2.zero;
            cover.SetActive(false);

            portalTran.InitPortalTransportation(this, UiMiniMapCtrl.Instance.GetMinimapByMapId(_mapId).gs_warFieldPanel.gs_warFieldMiniMap);
        }
#endregion
    }
}

