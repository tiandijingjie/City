using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WarField
{
    //panel用来选择portal的工作状态以及下一跳portal的控制的面板
    public class UIMiniPortalControlPanel : MonoBehaviour
    {
#region public parameters

#endregion

#region private parameters
        private Image _workableImg;
        private Image[] _tranImg; //传送指向的button图标
        private Button[] _tranBtn;
        private Sprite _potalWorkSprite, _portalPauseSprite;
        private int _targetOrder; //指向的下一跳的order
        private UIPortalTransportation _portalTransportation; //parent
        private UIPortalTransportation[] _nextPair; //下一级的所有传送点图标，不可能为null
        private bool _isShowed = false;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        private void Awake()
        {
            var t = transform.Find("WorkableBt");
            _workableImg = transform.Find("WorkableBt").GetComponent<Image>();

            _tranImg = new Image[3];
            _tranImg[0] = transform.Find("UpTranBt").GetComponent<Image>();
            _tranImg[1] = transform.Find("MidTranBt").GetComponent<Image>();
            _tranImg[2] = transform.Find("DownTranBt").GetComponent<Image>();

            _tranBtn = new Button[3];
            _tranBtn[0] = transform.Find("UpTranBt").GetComponent<Button>();
            _tranBtn[1] = transform.Find("MidTranBt").GetComponent<Button>();
            _tranBtn[2] = transform.Find("DownTranBt").GetComponent<Button>();

            _potalWorkSprite = Resources.Load<Sprite>("Textures/UI/WarField/MiniMap/MiniPortalWork");
            _portalPauseSprite = Resources.Load<Sprite>("Textures/UI/WarField/MiniMap/MiniPortalPause");
            _workableImg.sprite = _portalPauseSprite; //默认处于工作状态
        }

        //是否暂停传送button
        public void WorkableBtEvent()
        {
            if (_portalTransportation.ChangeWorkStatus() == true)
                _workableImg.sprite = _portalPauseSprite;
            else
                _workableImg.sprite = _potalWorkSprite;
        }

        //选择传送目标
        public void TransimitTargetBtEvent(int nextOrder)
        {
            if(_portalTransportation.ChangePortalTarget(nextOrder) == false)
                return;
            _targetOrder = nextOrder;
            SetImages();
        }
#endregion

#region public functions

        public void InitControlPanel(UIPortalTransportation transportation, int targetOrder, UIPortalTransportation[] nextPair)
        {
            _portalTransportation = transportation;
            _targetOrder = targetOrder;
            _nextPair = nextPair;
        }

        public void ShowPortalStatusPanel()
        {
            if(_isShowed == true)
                return;

            SetImages();
            gameObject.SetActive(true);
            _isShowed = true;
        }

        public void HidePortalStatusPanel()
        {
            _isShowed = false;
            gameObject.SetActive(false);
        }

        public void PortalStatusChanged()
        {
            if(_isShowed == false)
                return;
            SetImages();
        }
#endregion

#region private functions
        //设置各个image显示情况
        private void SetImages()
        {
            for (int i = 0; i < _tranImg.Length; i++)
            {
                if (_portalTransportation.gs_portal.gs_isOcuppied == false)
                {
                    _tranImg[i].color = Color.grey;
                    _tranBtn[i].interactable = false;
                }
                else
                {
                    if (_nextPair[i].gs_portal.CanReceive() == true)
                    {
                        if(_targetOrder == i)
                            _tranImg[i].color = Color.green;
                        else
                            _tranImg[i].color = Color.yellow;
                        _tranBtn[i].interactable = true;
                    }
                    else
                    {
                        _tranImg[i].color = Color.grey;
                        _tranBtn[i].interactable = false;
                    }
                }
            }
        }
#endregion
    }
}
