using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using ED = EffectDefines;
    using UD = UIDefines;

    //指示技能释放位置或者方向的特效
    public class EffectIndicator : MonoBehaviour, IoObserverIntf
    {
#region public parameters

#endregion

#region private parameters

        [SerializeField] protected ED.SkillIndicatorType _type;

        protected SpriteRenderer _radarSprite; //有radar的特效的sprite
        protected MaterialPropertyBlock _radarMat;
        protected Transform _transform;

        protected EffectIndicatorCb _scrIndicatorCb; //触发indicator的skill的回调
        private IoObserver _ioObserver;
        protected bool _active;
        protected Camera _mainCamera;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        protected virtual void Awake()
        {
            _transform = transform;
            _radarSprite = _transform.Find("Radar").GetComponent<SpriteRenderer>();
            _radarMat = new MaterialPropertyBlock();
            _radarSprite.GetPropertyBlock(_radarMat);
            _active = false;
            _ioObserver = new IoObserver(UD.UIEventGroupType.SKILLINDICATOR);
            _mainCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if(_active == true)
                OnUpdate();
        }

#endregion

#region public functions

        //size : 对于area是 (最远距离,覆盖范围)
        //       对于diraction是 (x长度,y长度)
        public bool ActiveEffectIndicator(EffectIndicatorCb cb)
        {
            if(_active == true)
                return false;
            if(UiIoTask.Instance.gs_occupiedGroupType != UD.UIEventGroupType.MIN) //IO被占用
                return false;

            _scrIndicatorCb = cb;
            _active = OnActive();
            if (_active == true)
            {
                _ioObserver.RegisterListener(this, KeyCode.Escape, "ESC", UD.UiIoEventType.KEYDOWN, UD.IoEvtScheduleType.STACK);
                _ioObserver.RegisterListener(this, KeyCode.Mouse0, "MouseLeft", UD.UiIoEventType.KEYDOWN, UD.IoEvtScheduleType.STACK);
                _ioObserver.RegisterListener(this, KeyCode.Mouse1, "MouseRight", UD.UiIoEventType.KEYDOWN, UD.IoEvtScheduleType.STACK);
                UiIoTask.Instance.OccupyExclusiveOwnerShip(UD.UIEventGroupType.SKILLINDICATOR);
                gameObject.SetActive(true); //必须先设置active才能执行携程
                StartCoroutine(UpdateLater(true));
            }

            return _active;
        }

        public void DeactivateSkillIndicator()
        {
            if(_active == false)
                return;

            UiIoTask.Instance.ReleaseExclusiveOwnerShip(UD.UIEventGroupType.SKILLINDICATOR);
            _ioObserver.UnregisterListener(this, KeyCode.Escape, "ESC", UD.UiIoEventType.KEYDOWN, UD.IoEvtScheduleType.STACK);
            _ioObserver.UnregisterListener(this, KeyCode.Mouse0, "MouseLeft", UD.UiIoEventType.KEYDOWN, UD.IoEvtScheduleType.STACK);
            _ioObserver.UnregisterListener(this, KeyCode.Mouse1, "MouseRight", UD.UiIoEventType.KEYDOWN, UD.IoEvtScheduleType.STACK);

            _radarMat.SetFloat("_isLoop", 0f);
            _radarSprite.SetPropertyBlock(_radarMat);

            StartCoroutine(UpdateLater(false));
            OnDeactive();
            _active = false;
        }

        public void OnIoEvtNotification(string keyAlias, UIDefines.UiIoEventType evtType)
        {
            switch (keyAlias, evtType)
            {
                case { keyAlias: "MouseLeft", evtType: UD.UiIoEventType.KEYDOWN }:
                    OnMouseRightDown();
                    DeactivateSkillIndicator();
                    break;
                case { keyAlias: "MouseRight", evtType: UD.UiIoEventType.KEYDOWN }:
                case { keyAlias: "ESC", evtType: UD.UiIoEventType.KEYDOWN }:
                    OnGiveUp();
                    _scrIndicatorCb.GiveUpEffect(); //通知src放弃释放技能
                    DeactivateSkillIndicator();
                    break;
                default:
                    GameLogger.LogWarning($"Receive unknown key {keyAlias} {evtType}");
                    break;
            }
        }
#endregion

#region private functions

        //因为UpdateObserverList必须与UnregisterListener 分开在两帧执行,导致SetActive必须在协程中执行
        private IEnumerator UpdateLater(bool active)
        {
            yield return null;
            if(active == false)
                gameObject.SetActive(active);
        }

        protected virtual bool OnActive()
        {
            return true;
        }

        protected virtual void OnGiveUp() { }

        protected virtual void OnMouseRightDown() { }

        protected virtual void OnUpdate() { }

        protected virtual void OnDeactive() { }
#endregion
    }
}

