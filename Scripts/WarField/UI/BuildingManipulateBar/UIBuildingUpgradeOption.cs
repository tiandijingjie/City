using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WarField
{
    using WBD = WarBuildingDefines;
    using WRD = WarResDefine;
    using WE = WarFieldElements;

    public class UIBuildingUpgradeOption : MonoBehaviour, UIMouthActivityIntf, ITask
    {
#region public parameters

#endregion

#region private parameters

        [SerializeField] private Image _bdIcon;
        [SerializeField] private UIMouthCheck _mouseCheck;

        private Image _bgImage = null;
        private bool _isShowed = false;
        private BuildingConf _curConf; //the conf that building want to upgrade to
        private WarBuilding _targetBd; //the building is be selected
        private UIBuildingManipulateBar _bar;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        private void Awake()
        {
            _bgImage = GetComponent<Image>();
            _mouseCheck.RegisterReceiver(this);
        }

#endregion

#region public functions

        public bool InitOption(BuildingConf conf, WarBuilding targetBd, UIBuildingManipulateBar bar)
        {
            transform.localScale = Vector3.zero;
            _targetBd = targetBd;

            _curConf = conf;
            _bdIcon.sprite = Resources.Load<Sprite>($"Textures/UI/WarField/BuildingIcons/{_curConf.gs_name.Replace(" ", "")}Icon");
            _bar = bar;

            if (_targetBd.CanUpgrade() == false)
            {
                _bdIcon.color = Color.gray;
                _bgImage.color = Color.gray;
            }
            else
            {
                if (WarResCtrl.Instance.GetResInStore(WRD.ResTypes.GOLDCOIN) >= _curConf.gs_price)
                {
                    _bdIcon.color = Color.white;
                    _bgImage.color = Color.white;
                }
                else
                {
                    _bdIcon.color = Color.gray;
                    _bgImage.color = Color.gray;
                }
            }

            return true;
        }

        public void RunNormalTask(float deltaTime)
        {
            Color color;
            if(_targetBd.CanUpgrade() == false)
                color = Color.gray;
            else
            {
                if (WarResCtrl.Instance.GetResInStore(WRD.ResTypes.GOLDCOIN) >= _curConf.gs_price)
                    color = Color.white;
                else
                    color = Color.gray;
            }

            if (_bdIcon.color != color)
            {
                _bdIcon.color = color;
                _bgImage.color = color;
            }
        }

        public void ShowOption()
        {
            iTween.ScaleTo(gameObject, iTween.Hash(
                "scale", Vector3.one,
                "time", 0.8f,
                "easetype", iTween.EaseType.easeOutElastic,
                "oncomplete", "OnShow",
                "oncompletetarget", gameObject  // 回调函数所在对象
            ));
        }

        public void MouthEnter(string value, PointerEventData eventData) { }

        public void MouthExit(string value, PointerEventData eventData) { }

        public void MouthClick(string value, PointerEventData eventData)
        {
            //不要判断_isShowed，否则会出现图像已经显示出来了但是还要等一下才能点击的问题
            // if(_isShowed == false)
            //     return;

            if (_targetBd.CanUpgrade() == true)
            {
                if (WarResCtrl.Instance.ConsumeRes(WRD.ResTypes.GOLDCOIN, _curConf.gs_price, false, null, out var index) == true)
                {
                    if (WarBuildingCtrl.Instance.BuildingUpgrade(_targetBd, _curConf) == true)
                        _bar.OptionBeSelected();
                    else
                        WarResCtrl.Instance.AddRes(WRD.ResTypes.GOLDCOIN, _curConf.gs_price);
                }
            }
        }

        public void MouthUp(string value, PointerEventData eventData) { }

        public void HideOption()
        {
            gameObject.SetActive(false);
            WarFieldGameManager.Instance.SuspendTask(this, WE.TaskType.NORMAL);
            WarFieldGameManager.Instance.UnregisterTask(this, WE.TaskType.NORMAL);
            _isShowed = false;
        }

        public void RunFixTask(float deltaTime)
        {
            throw new NotImplementedException();
        }
#endregion

#region private functions
        private void OnShow()
        {
            if(_isShowed == true)
                return;
            _isShowed = true;
            WarFieldGameManager.Instance.RegisterTask(this, WE.TaskType.NORMAL);
            WarFieldGameManager.Instance.ActiveTask(this, WE.TaskType.NORMAL);
        }
#endregion
    }
}

