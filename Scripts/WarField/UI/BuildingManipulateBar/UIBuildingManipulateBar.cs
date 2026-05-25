using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace WarField
{
    using WE = WarFieldElements;
    using WBD = WarBuildingDefines;
    using UD = UIDefines;

    public class UIBuildingManipulateBar : MonoBehaviour, UIMouthActivityIntf, IoObserverIntf
    {
#region public parameters

        public static UIBuildingManipulateBar Instance;

#endregion

#region private parameters

        [SerializeField] private GameObject _optionPfb;
        [SerializeField] private int _maxOptionCnt;
        [SerializeField] private float _radius;
        [SerializeField] private float _startAngle;
        [SerializeField] private float _stepAngle;

        private UIBuildingUpgradeOption[] _options;
        private List<int> _optionsShowed; //store option index
        private WarBuilding _curbd;
        private bool _isShowed = false;
        private bool _ignorClickInFrame = false; //

        private bool _isMouseIn; //鼠标是否在bar的范围内
        private IoObserver _ioObserver;
        private bool _keySet = false;

        //click check
        private Camera _camera;
        private Collider2D[] _clickCheckRet;
        private Vector2 _checkSize = new Vector2(3, 3); //检查的区域大小
        private LayerMask _bdLayerMask, _heroLayerMask;

#endregion

#region private parameters' get set

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
            transform.position = new Vector2(0, 10000);
            _optionsShowed = new List<int>();
            _options = new UIBuildingUpgradeOption[_maxOptionCnt];
            for (int i = 0; i < _maxOptionCnt; i++)
            {
                _options[i] = Instantiate(_optionPfb, transform).GetComponent<UIBuildingUpgradeOption>();
                _options[i].gameObject.SetActive(false);
            }

            _ioObserver = new IoObserver(UD.UIEventGroupType.BUIDINGUPGRADE);
            _isMouseIn = false;

            _camera = Camera.main;
            _clickCheckRet = new Collider2D[50];
            _bdLayerMask = LayerMask.GetMask("FriendlybuildingClick");
            _heroLayerMask = LayerMask.GetMask("FriendlySoldierBody");

            GetComponent<UIMouthCheck>().RegisterReceiver(this);
        }

#endregion

#region public functions

        public void ShowBuildingManipulateBar(WarBuilding bd)
        {
            if (ReferenceEquals(bd, null) == true)
                return;
            if (_isShowed == true && bd == _curbd)
                return;

            var conf = bd.gs_bdConf;
            if (conf.gs_race != WE.RaceType.Human)
                return;

            transform.position = Camera.main.WorldToScreenPoint(bd.transform.position);
            _curbd = bd;

            int optionCnt = 0;
            if (conf.gs_mode == WBD.BuildingMode.DEFENCE)
            {
                if (conf.gs_subType == (int)HumanDefines.DefenceType.BASICTOWER)
                {
                    //遍历高级防御建筑
                    for (int i = (int)HumanDefines.DefenceType.BASICTOWER + 1; i < (int)HumanDefines.DefenceType.MAX; i++)
                    {
                        conf = WarBuildingCtrl.Instance.GetBdConf(WE.RaceType.Human, WBD.BuildingMode.DEFENCE, i);
                        if (conf.IsContructable() == true)
                        {
                            _options[optionCnt].gameObject.SetActive(true);
                            _options[optionCnt].InitOption(conf, _curbd, this);
                            _options[optionCnt].ShowOption();
                            _optionsShowed.Add(optionCnt);
                            optionCnt++;
                        }
                    }

                    ArrangeOptions();
                }
            }

            transform.localScale = Vector3.one * 0.3f;
            iTween.ScaleTo(gameObject, iTween.Hash(
                "scale", Vector3.one,
                "time", 0.3f,
                "easetype", iTween.EaseType.easeOutElastic,
                "oncomplete", "",
                "oncompletetarget", gameObject // 回调函数所在对象
            ));

            StartCoroutine(IgnorClickInFrame());
            if (_keySet == false)
            {
                _keySet = true;
                _ioObserver.RegisterListener(this, KeyCode.Mouse0, "MouseLeft", UD.UiIoEventType.KEYDOWN, UD.IoEvtScheduleType.LIST);
                _ioObserver.RegisterListener(this, KeyCode.Escape, "ESC", UD.UiIoEventType.KEYDOWN, UD.IoEvtScheduleType.LIST);
                StartCoroutine(SetKeyMonitor(true));
            }

            _isMouseIn = true; //默认鼠标在区域内
        }

        public void HideBuildingUpgrade(bool setKey)
        {
            _isMouseIn = false;
            _isShowed = false;
            transform.position = new Vector2(0, 10000);
            for (int i = _optionsShowed.Count - 1; i >= 0; i--)
            {
                _options[_optionsShowed[i]].HideOption();
            }

            _optionsShowed.Clear();

            if (setKey == true)
            {
                _keySet = false;
                StartCoroutine(IgnorClickInFrame());
                _ioObserver.UnregisterListener(this, KeyCode.Mouse0, "MouseLeft", UD.UiIoEventType.KEYDOWN, UD.IoEvtScheduleType.LIST);
                _ioObserver.UnregisterListener(this, KeyCode.Escape, "ESC", UD.UiIoEventType.KEYDOWN, UD.IoEvtScheduleType.LIST);
                StartCoroutine(SetKeyMonitor(false));
            }
        }

        public void OptionBeSelected()
        {
            HideBuildingUpgrade(true);
        }

        public void MouthEnter(string value, PointerEventData eventData)
        {
            _isMouseIn = true;
        }

        public void MouthExit(string value, PointerEventData eventData)
        {
            _isMouseIn = false;
        }

        public void MouthClick(string value, PointerEventData eventData)
        {
        }

        public void MouthUp(string value, PointerEventData eventData)
        {
        }

        public void OnIoEvtNotification(string keyAlias, UIDefines.UiIoEventType evtType)
        {
            switch (keyAlias, evtType)
            {
                case { keyAlias: "MouseLeft", evtType: UD.UiIoEventType.KEYDOWN }:
                    if (_ignorClickInFrame == true)
                        return;
                    if (_isMouseIn == false) //在范围内的点击忽略
                    {
                        switch (CheckClickOverTargetObj(Input.mousePosition, out var script))
                        {
                            case WE.WarEleType.MIN:
                                HideBuildingUpgrade(true);
                                break;
                            case WE.WarEleType.BUILDING: //点击在另一个建筑上
                                HideBuildingUpgrade(true); //不要解除key的占用
                                ((WarBuilding)script).UserSelected(true);
                                break;
                            default:
                                break;
                        }
                    }
                    else //再次点击在同一个建筑上
                        _curbd.UserSelected(false);

                    break;

                case { keyAlias: "ESC", evtType: UD.UiIoEventType.KEYDOWN }:
                    HideBuildingUpgrade(true);
                    break;
                default:
                    GameLogger.LogWarning($"Receive unknown key {keyAlias} {evtType}");
                    break;
            }
        }

#endregion

#region private functions

        private void ArrangeOptions()
        {
            int cnt = _optionsShowed.Count;
            if (cnt == 0)
                return;

            Vector2[] points = new Vector2[cnt];
            float mid = (cnt - 1) / 2f;
            for (int i = 0; i < cnt; i++)
            {
                float angle = _startAngle + (i - mid) * _stepAngle; // 对称分布
                float rad = angle * Mathf.Deg2Rad;
                float x = _radius * Mathf.Cos(rad);
                float y = _radius * Mathf.Sin(rad);
                points[i] = new Vector2(x, y);

                _options[_optionsShowed[i]].transform.localPosition = points[i];
            }
        }

        private IEnumerator SetKeyMonitor(bool isOccupy)
        {
            yield return null; //延迟一帧，因为如果马上解除占用，DrawSelectionBox那边按照顺序又会收到点击导致又显示出来
            if (isOccupy == true)
                UiIoTask.Instance.OccupyExclusiveOwnerShip(UIDefines.UIEventGroupType.BUIDINGUPGRADE);
            else
                UiIoTask.Instance.ReleaseExclusiveOwnerShip(UIDefines.UIEventGroupType.BUIDINGUPGRADE);
        }

        //检测点击是不是在某个可被点击物体上，如果在可点击的物体上时触发相关时间，不进行后续的画框
        //return true：选中了物体，不画框
        private WE.WarEleType CheckClickOverTargetObj(Vector2 mouthPos, out MonoBehaviour script)
        {
            script = null;
            Vector2 mouseWorldPos = _camera.ScreenToWorldPoint(mouthPos);
            Vector2 center = new Vector2(mouseWorldPos.x, mouseWorldPos.y);

            //check building
            int count = Physics2D.OverlapBoxNonAlloc(center, _checkSize, 0f, _clickCheckRet, _bdLayerMask);
            if (count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    WarBuilding bd = _clickCheckRet[i].GetComponent<WarBuilding>();
                    if (ReferenceEquals(bd, null) == false)
                    {
                        if (bd.IsPosInBuilding(mouseWorldPos) == true)
                        {
                            script = bd;
                            return WE.WarEleType.BUILDING;
                        }
                    }
                }
            }

            return WE.WarEleType.MIN;
        }

        //在显示/隐藏的过程中忽略 bar上的点击
        private IEnumerator IgnorClickInFrame()
        {
            _ignorClickInFrame = true;
            yield return null;
            yield return null;
            _ignorClickInFrame = false;
        }

#endregion
    }
}

