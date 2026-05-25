using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace WarField
{
    using UD = UIDefines;
    using SD = SoldierDefines;
    using GD = GlobalDefines;
    using WE = WarFieldElements;

    public class UiMiniMapMoveManeuverCtrl : MonoBehaviour, ITask, IoObserverIntf, UIMouthActivityIntf
    {
        private struct MiniMapManeuverDataInfoMgr
        {
            public bool p_isLeftButtonManeuverActivated;
            public bool p_isRightButtonManeuverActivated;
            public bool p_isMouseDownManeuverActivated;

            public struct ButtonManeuverDataType
            {
                public float p_startSpeed;
                public float p_acceleration;
                public bool p_isHolding;
                public float p_currentSpeed;
                public bool p_moveToLeft;
                public bool p_moveToRight;
            }

            public ButtonManeuverDataType p_btnManeuverData;

            public struct MouseManeuverData
            {
                public bool p_dragMapEnabled;
                public Vector2 p_localMouseManeuverStartPos;
                public float p_scrollMoveSpeed;
            }

            public MouseManeuverData p_mouseManeuverData;

            public struct MapManeuverBound
            {
                public float p_xMin;
                public float p_xMax;
            }

            public MapManeuverBound p_mapManeuverBound;

            public MiniMapManeuverDataInfoMgr(bool left, bool right, bool mouse)
            {
                p_isLeftButtonManeuverActivated = left;
                p_isRightButtonManeuverActivated = right;
                p_isMouseDownManeuverActivated = mouse;

                p_btnManeuverData = new ButtonManeuverDataType
                {
                    p_startSpeed = 20f,
                    p_acceleration = 2000f,
                    p_isHolding = false,
                    p_currentSpeed = 0f,
                    p_moveToLeft = false,
                    p_moveToRight = false
                };

                p_mouseManeuverData = new MouseManeuverData
                {
                    p_dragMapEnabled = false,
                    p_localMouseManeuverStartPos = Vector2.zero,
                    p_scrollMoveSpeed = 50f
                };

                p_mapManeuverBound = new MapManeuverBound
                {
                    p_xMin = 0f,
                    p_xMax = 0f
                };
            }
        }

#region public parameters

#endregion

#region private parameters

        [SerializeField] private List<UIMouthCheck> _mouthCheckList = new List<UIMouthCheck>();
        [SerializeField] private Slider _mapSlider;

        private bool _beInited = false;
        private bool _maneuverActived = true;
        private RectTransform _rectTransform = null;
        private Vector2 _lastMapAnchoredPos = Vector2.zero;
        private IoObserver _ioObserver;
        private MiniMapManeuverDataInfoMgr _maneuverDataInfoMgr;
        private bool _isUITaskStarted = false;
        private CanvasGroup _canvasGroupOfSlider;
        private int _mapId;

#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        private void Awake()
        {
            _beInited = false;
        }

#endregion

#region public functions
        public void RunNormalTask(float deltaTime)
        {
            OnDragMiniMap();
            OnMouseScrollMoveMiniMap();
            OnMoveMiniMapToLeftDir();
            OnMoveMiniMapToRightDir();

            OnMiniMapSliderControl();
            OnUpdateTheLastMiniMapAnchoredPos();
        }

        public void OnIoEvtNotification(string keyAlias, UD.UiIoEventType evtType)
        {
            switch (keyAlias, evtType)
            {
                case { keyAlias: "MouseLeft", evtType: UD.UiIoEventType.KEYUP }:
                {
                    OnEndDragMiniMap();
                    OnEndMoveMiniMapByHoldingDirKey();
                    break;
                }
                default:
                    GameLogger.LogWarning($"Receive unknown key {keyAlias} {evtType}");
                    break;
            }
        }

        public bool Init(int mapId)
        {
            if (_beInited)
            {
                GameLogger.LogWarning("UiMiniMapManeuverTask already inited!");
                return false;
            }

            _mapId = mapId;
            _maneuverDataInfoMgr = new MiniMapManeuverDataInfoMgr(true, true, true);

            foreach (var item in _mouthCheckList)
            {
                item.RegisterReceiver(this);
            }

            _rectTransform = gameObject.GetComponent<RectTransform>();
            if (ReferenceEquals(_rectTransform, null) == true)
            {
                GameLogger.LogError("UiMiniMapManeuverTask could not find RectTransform component!");
                return false;
            }

            _ioObserver = new IoObserver(UD.UIEventGroupType.MINIMAP);
            _ioObserver.RegisterListener(this, KeyCode.Mouse0, "MouseLeft", UD.UiIoEventType.KEYUP, UD.IoEvtScheduleType.LIST);

            _mapSlider.minValue = 0;
            _mapSlider.maxValue = 1f;

            _lastMapAnchoredPos = _rectTransform.anchoredPosition;

            _canvasGroupOfSlider = _mapSlider.GetComponent<CanvasGroup>();
            if (_canvasGroupOfSlider == null)
            {
                _canvasGroupOfSlider = _mapSlider.AddComponent<CanvasGroup>();
            }

            _canvasGroupOfSlider.alpha =　0.0f;

            var parentRect = _rectTransform.parent as RectTransform;
            _maneuverDataInfoMgr.p_mapManeuverBound.p_xMin = parentRect.rect.width - _rectTransform.sizeDelta.x;
            _maneuverDataInfoMgr.p_mapManeuverBound.p_xMax = 0f;

            WarFieldGameManager.Instance.RegisterTask(this, WE.TaskType.NORMAL);
            WarFieldGameManager.Instance.ActiveTask(this, WE.TaskType.NORMAL);
            _beInited = true;
            return true;
        }

        public void MouthEnter(string value, PointerEventData eventData)
        {
        }

        public void MouthExit(string value, PointerEventData eventData)
        {
            if (value == "MiniMap")
            {
                OnEndDragMiniMap();
            }
            else if (value == "LeftButton")
            {
                OnEndMoveMiniMapByHoldingLeftDirKey();
            }
            else if (value == "RightButton")
            {
                OnEndMoveMiniMapByHoldingRightDirKey();
            }
        }

        public void MouthClick(string dir, PointerEventData eventData)
        {
            if (_maneuverActived == false)
            {
                GameLogger.LogError("UiMiniMap maneuver is inactive!");
                return;
            }

            if (dir == "LeftButton" || dir == "RightButton")
            {
                OnMiniMapManeuverByBtnDirKey(dir);
            }
            else if (dir == "MiniMap")
            {
                OnMiniMapManeuverByMouseKey(dir);
            }
            else
            {
                GameLogger.LogWarning(dir + " is not a valid direction!");
                return;
            }
        }

        public void MouthUp(string value, PointerEventData eventData) { }

        public void RunFixTask(float deltaTime)
        {
            throw new NotImplementedException();
        }
#endregion

#region private functions

        private void OnMiniMapManeuverByBtnDirKey(string dir)
        {
            switch (dir)
            {
                case "LeftButton":
                {
                    if (_maneuverDataInfoMgr.p_isLeftButtonManeuverActivated == false)
                    {
                        return;
                    }

                    _maneuverDataInfoMgr.p_btnManeuverData.p_moveToLeft = true;
                    break;
                }
                case "RightButton":
                {
                    if (_maneuverDataInfoMgr.p_isRightButtonManeuverActivated == false)
                    {
                        return;
                    }

                    _maneuverDataInfoMgr.p_btnManeuverData.p_moveToRight = true;
                    break;
                }
                default:
                {
                    GameLogger.LogWarning(dir + " is not a valid direction!");
                    return;
                }
            }

            _maneuverDataInfoMgr.p_btnManeuverData.p_currentSpeed = _maneuverDataInfoMgr.p_btnManeuverData.p_startSpeed;
            _maneuverDataInfoMgr.p_btnManeuverData.p_isHolding = true;

            OnStartUpUITask();
            WarFieldGameManager.Instance.ActiveTask(this, WE.TaskType.NORMAL);
        }

        private void OnMiniMapManeuverByMouseKey(string dir)
        {
            if (_maneuverDataInfoMgr.p_isMouseDownManeuverActivated == false)
            {
                return;
            }

            OnStartDragMiniMap();
        }

        private void OnStartUpUITask()
        {
            if (!_isUITaskStarted)
            {
                //   ((IUITask)this).StartTask();
                _isUITaskStarted = true;
            }
        }

        private void OnStartDragMiniMap()
        {
            _maneuverDataInfoMgr.p_mouseManeuverData.p_dragMapEnabled = true;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform,
                Input.mousePosition,
                null,
                out _maneuverDataInfoMgr.p_mouseManeuverData.p_localMouseManeuverStartPos
            );

            OnStartUpUITask();
            WarFieldGameManager.Instance.ActiveTask(this, WE.TaskType.NORMAL);
        }

        private void OnDragMiniMap()
        {
            if (!_maneuverDataInfoMgr.p_mouseManeuverData.p_dragMapEnabled)
            {
                return;
            }

            var origAnchoredPositionPos = _rectTransform.anchoredPosition;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform,
                Input.mousePosition,
                null,
                out var localMouseManeuverCurPos
            );

            var offset = localMouseManeuverCurPos - _maneuverDataInfoMgr.p_mouseManeuverData.p_localMouseManeuverStartPos;
            var newAnchoredPosition = origAnchoredPositionPos + new Vector2(offset.x, 0);
            _rectTransform.anchoredPosition = ClampMiniMapAnchoredPosition(newAnchoredPosition);
        }

        private void OnEndDragMiniMap()
        {
            _maneuverDataInfoMgr.p_mouseManeuverData.p_dragMapEnabled = false;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        private void OnMouseScrollMoveMiniMap()
        {
            // float scrollValue = Input.GetAxis("Mouse ScrollWheel");
            // if (Mathf.Abs(scrollValue) > 0.01f)
            // {
            //     var newAnchoredPosition = _rectTransform.anchoredPosition;
            //     newAnchoredPosition += new Vector2(scrollValue * _maneuverDataInfoMgr.p_mouseManeuverData.p_scrollMoveSpeed, 0f);
            //     _rectTransform.anchoredPosition = ClampMiniMapAnchoredPosition(newAnchoredPosition);
            // }
        }

        private void OnMoveMiniMapToLeftDir()
        {
            if (_maneuverDataInfoMgr.p_btnManeuverData.p_moveToRight &&
                _maneuverDataInfoMgr.p_btnManeuverData.p_isHolding)
            {
                return;
            }

            if (_maneuverDataInfoMgr.p_btnManeuverData.p_moveToLeft && _maneuverDataInfoMgr.p_btnManeuverData.p_isHolding)
            {
                var newAnchoredPositionPos = _rectTransform.anchoredPosition;
                _maneuverDataInfoMgr.p_btnManeuverData.p_currentSpeed += _maneuverDataInfoMgr.p_btnManeuverData.p_acceleration * Time.deltaTime;
                newAnchoredPositionPos += Utils.GetNormalizedDirVec(GlobalDefines.DirDef.LDir) *
                                          (_maneuverDataInfoMgr.p_btnManeuverData.p_currentSpeed * Time.deltaTime);
                _rectTransform.anchoredPosition = ClampMiniMapAnchoredPosition(newAnchoredPositionPos);
            }
            else
            {
                _maneuverDataInfoMgr.p_btnManeuverData.p_currentSpeed = _maneuverDataInfoMgr.p_btnManeuverData.p_startSpeed;
            }
        }

        private void OnMoveMiniMapToRightDir()
        {
            if (_maneuverDataInfoMgr.p_btnManeuverData.p_moveToLeft &&
                _maneuverDataInfoMgr.p_btnManeuverData.p_isHolding)
            {
                return;
            }

            if (_maneuverDataInfoMgr.p_btnManeuverData.p_moveToRight && _maneuverDataInfoMgr.p_btnManeuverData.p_isHolding)
            {
                var newAnchoredPositionPos = _rectTransform.anchoredPosition;
                _maneuverDataInfoMgr.p_btnManeuverData.p_currentSpeed += _maneuverDataInfoMgr.p_btnManeuverData.p_acceleration * Time.deltaTime;
                newAnchoredPositionPos += Utils.GetNormalizedDirVec(GlobalDefines.DirDef.RDir) *
                                          _maneuverDataInfoMgr.p_btnManeuverData.p_currentSpeed * Time.deltaTime;
                _rectTransform.anchoredPosition = ClampMiniMapAnchoredPosition(newAnchoredPositionPos);
            }
            else
            {
                _maneuverDataInfoMgr.p_btnManeuverData.p_currentSpeed = _maneuverDataInfoMgr.p_btnManeuverData.p_startSpeed;
            }
        }

        private void OnEndMoveMiniMapByHoldingDirKey()
        {
            OnEndMoveMiniMapByHoldingLeftDirKey();
            OnEndMoveMiniMapByHoldingRightDirKey();
        }

        private void OnEndMoveMiniMapByHoldingLeftDirKey()
        {
            _maneuverDataInfoMgr.p_btnManeuverData.p_isHolding = false;
            _maneuverDataInfoMgr.p_btnManeuverData.p_moveToLeft = false;
        }

        private void OnEndMoveMiniMapByHoldingRightDirKey()
        {
            _maneuverDataInfoMgr.p_btnManeuverData.p_isHolding = false;
            _maneuverDataInfoMgr.p_btnManeuverData.p_moveToRight = false;
        }

        private void OnMiniMapSliderControl()
        {
            if (_lastMapAnchoredPos.x != _rectTransform.anchoredPosition.x)
            {
                OnRestoreMiniMapSlider();

                _mapSlider.value =
                    Mathf.Clamp(_rectTransform.anchoredPosition.x / _maneuverDataInfoMgr.p_mapManeuverBound.p_xMin, 0f, 1f);

                Invoke("OnMiniMapSliderFadeOut", 1f);
            }
        }

        private void OnUpdateTheLastMiniMapAnchoredPos()
        {
            _lastMapAnchoredPos = _rectTransform.anchoredPosition;
        }

        private void OnMiniMapSliderFadeOut()
        {
            iTween.ValueTo(gameObject, iTween.Hash(
                "from", 1f,
                "to", 0f,
                "time", 3f,
                "onupdate", "OnUpdateAlpha",
                "onupdatetarget", gameObject,
                "easetype", iTween.EaseType.linear
            ));
        }

        private void OnUpdateAlpha(object alpha)
        {
            _canvasGroupOfSlider.alpha = (float)alpha;
        }

        private void OnRestoreMiniMapSlider()
        {
            iTween.Stop(gameObject);
            CancelInvoke("OnMiniMapSliderFadeOut");

            _canvasGroupOfSlider.alpha = 1f;
        }

        private bool IsMiniMapOutOfLeftBound()
        {
            return _rectTransform.anchoredPosition.x >= _maneuverDataInfoMgr.p_mapManeuverBound.p_xMax;
        }

        private bool IsMinMapOutOfRightBound()
        {
            return _rectTransform.anchoredPosition.x <= _maneuverDataInfoMgr.p_mapManeuverBound.p_xMin;
        }

        private Vector2 ClampMiniMapAnchoredPosition(Vector2 newAnchoredPosition)
        {
            newAnchoredPosition.x = Mathf.Clamp(
                newAnchoredPosition.x,
                _maneuverDataInfoMgr.p_mapManeuverBound.p_xMin,
                _maneuverDataInfoMgr.p_mapManeuverBound.p_xMax
            );
            return newAnchoredPosition;
        }

#endregion
    }
}

