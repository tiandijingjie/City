using System;
using System.Collections;
using System.Collections.Generic;
using UI_Spline_Renderer;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Splines;
using UnityEngine.UI;

namespace WarField
{
    using WE = WarFieldElements;
    using UD = UIDefines;

    //portal的传送方向以及状态显示
    public class UIPortalTransportation : MonoBehaviour, UIMouthActivityIntf, IoObserverIntf
    {
#region public parameters

#endregion

#region private parameters
        private Portal _portal = null;
        private Image _statusImage = null; //显示portal状态
        private Sprite _wordingSprite, _notOccupySprite, _pauseSprite;
        private bool _isOccupied = false; //portal是否被占领
        private bool _canTransmit = false; //是否能够工作，包括是否占领，用户是否开启传送，下一跳是否接收
        private bool _workable = true; //玩家选择是不是关闭传送，与Portal的gs_workable对应
        private Image _portalIconImage; //portal 不同状态也会影响portal icon的颜色
        private RectTransform _rectTransform;
        private UIMiniPortalControlPanel _uiMiniPortalControlPanel;

        private UIPortalTransportation[] _nextPair; //下一级的所有传送点图标，最后一级为NULL
        private bool[] _nextPairCanReceive; //记录下一跳每个portal可接受的情况
        private int _curTargetIndex; //目前下一跳的目的地，是_nextPair的index，最后一级是-1

        private IoObserver _ioObserver;
        private bool _isSelected = false; //用户是否选中portal
        private Rect _bounds; //包括control panel的包围盒大小，用来检测鼠标左键是不是点击在范围内，portal被选中或者滚动的时候需要重新计算
        private List<string> _boundExclude = new List<string>{"Cover"}; //计算总的包围盒是排除在外的节点名

        private int _pairIndex = -1; //记录portal属于第几pair

        private UiMiniWarFieldMap _parent;
        private GameObject _cover; //显示覆盖范围的节点
        private bool _isShowed = false;
#endregion

#region private parameters' get set

        public Portal gs_portal
        {
            get { return _portal; }
        }

        public bool gs_canTransmit
        {
            get { return _canTransmit; }
        }

        public UIPortalTransportation gs_nextPortal
        {
            get
            {
                if(_canTransmit == false)
                    return null;
                return _nextPair[_curTargetIndex];
            }
        }

        public RectTransform gs_rectTransform
        {
            get { return _rectTransform; }
        }

        public int gs_pairIndex
        {
            get { return _pairIndex; }
        }
#endregion

#region Unity callbacks

#endregion

#region public functions

        public bool InitPortalTransportation(Portal portal, UiMiniWarFieldMap parent)
        {
            _parent = parent;
            _portal = portal;

            //set portal's default target
            _nextPair =  _parent.GetNextPortalPair(this);
            if (_nextPair == null) //最后一级
            {
                _curTargetIndex = -1;
                _canTransmit = false;
            }
            else
            {
                int order = _portal.gs_order;
                _portal.SetTargetPotal(_nextPair[order].gs_portal);
                _curTargetIndex = order;
            }

            _ioObserver = new IoObserver(UD.UIEventGroupType.MINIMAP);
            _rectTransform = GetComponent<RectTransform>();
            _wordingSprite = Resources.Load<Sprite>("Textures/UI/WarField/MiniMap/PortalIconWorking");
            _notOccupySprite = Resources.Load<Sprite>("Textures/UI/WarField/MiniMap/PortalIconNotOccupy");
            _pauseSprite = Resources.Load<Sprite>("Textures/UI/WarField/MiniMap/PortalIconPaused");
            _portalIconImage = transform.Find("ElePic").GetComponent<Image>();

            //动态增加显示状态的image,并设置位置
            {
                GameObject status = new GameObject("StatusImg", typeof(RectTransform));
                status.transform.SetParent(_rectTransform);

                _statusImage = status.AddComponent<Image>();
                //将_statusImage图标显示在最图层的最下面
                _statusImage.transform.SetAsFirstSibling();
                _statusImage.raycastTarget = false;
                RectTransform rt = status.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(25, 25); //设置状态image的大小
                // 把锚点和 pivot 都设为左上角
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);

                RectTransform eleRt = _portalIconImage.GetComponent<RectTransform>();
                // ElePic 的左上角 = 它的中心位置 + 偏移
                Vector2 eleSize = eleRt.rect.size;
                Vector2 eleTopLeft = new Vector2(
                    eleRt.anchoredPosition.x - eleSize.x * eleRt.pivot.x,
                    eleRt.anchoredPosition.y + eleSize.y * (1 - eleRt.pivot.y)
                );
                //_statusImage与_portalIconImage左上角重合
                rt.anchoredPosition = eleTopLeft;
            }

            //给_portalIconImage增加鼠标检测
            if(_nextPair != null) //最后一组portal 不添加鼠标检测
            {
                _portalIconImage.raycastTarget = true;
                UIMouthCheck checker = _portalIconImage.gameObject.AddComponent<UIMouthCheck>();
                checker.RegisterReceiver(this);
                checker.SetValue("portalIcon");
            }

            _nextPairCanReceive = new bool[3];
            for (int i = 0; i < _nextPairCanReceive.Length; i++)
            {
                if (_nextPair == null)
                    _nextPairCanReceive[i] = false;
                else
                    _nextPairCanReceive[i] = _nextPair[i].gs_portal.CanReceive();
            }

            _uiMiniPortalControlPanel = null;

            SetStatus();
            return true;
        }

        public void UpdatePortalStatus()
        {
            if(_isShowed == false)
                return;

            bool changed = false;
            if (_portal.gs_isOcuppied != _isOccupied)
            {
                _isOccupied = _portal.gs_isOcuppied;
                SetStatus();
                changed = true;
            }

            if (_nextPair != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (_nextPair[i].gs_portal.CanReceive() != _nextPairCanReceive[i])
                    {
                        _nextPairCanReceive[i] = !_nextPairCanReceive[i];
                        if(i == _curTargetIndex)
                            SetStatus();
                        changed = true;
                    }
                }
            }

            if(changed == true && ReferenceEquals(_uiMiniPortalControlPanel, null) == false)
                _uiMiniPortalControlPanel.PortalStatusChanged();
        }

        //用户控制portal是否工作
        public bool ChangeWorkStatus()
        {
            _workable = !_workable;
            _portal.ChangeTransmitStatus(_workable);
            SetStatus();
            return _workable;
        }

        //如果下一跳没有占领，不能选择
        public bool ChangePortalTarget(int nextOrder)
        {
            if(_nextPair == null)
                return false;
            if(_isSelected == false)
                return false;
            if(_nextPairCanReceive[nextOrder] == false)
                return false;

            _curTargetIndex = nextOrder;
            _portal.SetTargetPotal(_nextPair[_curTargetIndex].gs_portal);
            SetStatus();
            return true;
        }

        public void OnIoEvtNotification(string keyAlias, UIDefines.UiIoEventType evtType)
        {
            bool needUnregister = false;
            switch (keyAlias, evtType)
            {
                case { keyAlias: "ESC", evtType: UD.UiIoEventType.KEYDOWN }:
                    BeUnselect();
                    needUnregister = true;
                    break;
                case { keyAlias: "MouseLeft", evtType: UD.UiIoEventType.KEYDOWN }:
                    if (_bounds.Contains(Input.mousePosition) == false)
                    {
                        BeUnselect();
                        needUnregister = true;
                    }
                    break;
                default:
                    break;
            }

            if (needUnregister == true)
            {
                _ioObserver.UnregisterListener(this, KeyCode.Escape, "ESC", UD.UiIoEventType.KEYDOWN, UD.IoEvtScheduleType.STACK);
                _ioObserver.UnregisterListener(this, KeyCode.Mouse0, "MouseLeft", UD.UiIoEventType.KEYDOWN, UD.IoEvtScheduleType.LIST);
            }
        }

        public void MouthEnter(string value, PointerEventData eventData) { }

        public void MouthExit(string value, PointerEventData eventData) { }

        public void MouthClick(string value, PointerEventData eventData)
        {
            if (value == "portalIcon")
            {
                if(_isSelected == true)
                    return;

                if(_nextPair != null && _isOccupied == true) //没有占领或者最后的portal不弹出选项面板
                {
                    _ioObserver.RegisterListener(this, KeyCode.Escape, "ESC", UD.UiIoEventType.KEYDOWN, UD.IoEvtScheduleType.STACK);
                    _ioObserver.RegisterListener(this, KeyCode.Mouse0, "MouseLeft", UD.UiIoEventType.KEYDOWN, UD.IoEvtScheduleType.LIST);
                    BeSelect();
                }
            }
        }

        public void MouthUp(string value, PointerEventData eventData) { }

        //地图滚动
        public void OnMapScroll()
        {
            if(_isSelected == true)
                _bounds = Utils.CalculateWorldBounds(_rectTransform, _boundExclude);
        }

        public void OnShow()
        {
            _isShowed = true;
        }

        public void OnHide()
        {
            if (_isSelected == true)
            {
                BeUnselect();
            }
            _isShowed = false;
        }

        public void SetPairIndex(int pairIndex)
        {
            _pairIndex = pairIndex;
        }

        public void ShowRangeInMinimap()
        {
            if (_cover == null)
                _cover = transform.Find("Cover").gameObject;
            _cover.SetActive(true);
        }

        public void HideRangeInMinimap()
        {
            if (_cover == null)
                _cover = transform.Find("Cover").gameObject;
            _cover.SetActive(false);
        }
#endregion

#region private functions

        private void SetStatus()
        {
            if (_portal.gs_isOcuppied == true)
            {
                _isOccupied = true;
                _portalIconImage.color = Color.white;
                if (_nextPair == null) //最后一组
                {
                    _canTransmit = false;
                    _statusImage.sprite = _wordingSprite;
                }
                else
                {
                    if (_workable == true && _nextPairCanReceive[_curTargetIndex] == true)
                    {
                        _canTransmit = true;
                        _statusImage.sprite = _wordingSprite;
                    }
                    else
                    {
                        _canTransmit = false;
                        _statusImage.sprite = _pauseSprite;
                    }
                }
            }
            else
            {
                _isOccupied = false;
                _statusImage.sprite = _notOccupySprite;
                _canTransmit = false;
                _portalIconImage.color = Color.gray;
            }
        }

        private void BeSelect()
        {
            if (_isSelected == false)
            {
                if (ReferenceEquals(_uiMiniPortalControlPanel, null) == true)
                {
                    _bounds = Utils.CalculateWorldBounds(_rectTransform, _boundExclude);
                    GameObject controlPanelPfb = Resources.Load<GameObject>("Prefabs/UI/WarField/MiniMap/MiniMapPortalControlPanel");
                    var obj = Instantiate(controlPanelPfb, transform);
                    obj.GetComponent<RectTransform>().localPosition =
                        _rectTransform.InverseTransformPoint(new Vector3(_bounds.max.x + 5, _bounds.min.y, 0));
                    _uiMiniPortalControlPanel = obj.GetComponent<UIMiniPortalControlPanel>();
                    _uiMiniPortalControlPanel.InitControlPanel(this, _curTargetIndex, _nextPair);
                }
                _uiMiniPortalControlPanel.ShowPortalStatusPanel();
                LayoutRebuilder.ForceRebuildLayoutImmediate(_rectTransform);
                _bounds = Utils.CalculateWorldBounds(_rectTransform, _boundExclude);
                _isSelected = true;
            }
        }

        private void BeUnselect()
        {
            if (_isSelected == true)
            {
                _uiMiniPortalControlPanel.HidePortalStatusPanel();
                _isSelected = false;
            }
        }
#endregion
    }
}

