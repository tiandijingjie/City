using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WarField
{
    using WE = WarFieldElements;
    using WBD = WarBuildingDefines;
    using WRD = WarResDefine;
    using GD = GlobalDefines;

    public class BuildingChooseItem : MonoBehaviour, UIMouthActivityIntf, ITask
    {
#region public parameters

#endregion

#region private parameters
        [SerializeField] private Image _beChooseImage;//选中时显示
        [SerializeField] private Sprite _beChooseSprite, _notChooseSprite; //选中和没被选中的时候的背景sprite
        [SerializeField] private Image _buildingImage;
        [SerializeField] private Sprite[] _modeSprites; //WBD.BuildingMode
        [SerializeField] private Image _modeIcon;

        private UIMouthCheck _mouthChecker;
        private Image _bgImage;
        private BuildingConf _bdConf;
        private BuildingConf _conf;
        private float _lastClickTime; //双击检测

        private bool _canBuild;
        private bool _beInited;
        private bool _beSelected;
#endregion

#region private parameters' get set
        public BuildingConf gs_bdConf
        {
            get { return _conf; }
        }

        public bool gs_canBuild
        {
            get { return _canBuild; }
        }
#endregion

#region Unity callbacks

        private void Awake()
        {
            _beInited = false;
            _beSelected = false;
            _mouthChecker = GetComponent<UIMouthCheck>();
            _mouthChecker.RegisterReceiver(this);
            _beChooseImage.enabled = false;
            _bgImage = GetComponent<Image>();
            _bgImage.sprite = _notChooseSprite;
        }

#endregion

#region public functions

        public bool InitChooseItem(BuildingConf conf)
        {
            if(_beInited == true)
                return false;

            _conf = conf;
            _modeIcon.sprite = _modeSprites[(int)_conf.gs_mode];
            _buildingImage.sprite = _conf.gs_pic;
            _lastClickTime = -1;
            WarFieldGameManager.Instance.RegisterTask(this, WE.TaskType.NORMAL);
            _beInited = true;
            return true;
        }

        public void RunNormalTask(float deltaTime)
        {
            bool canBuild = _canBuild;

            //监控资源，当前是不是有足够资源建造
            if (WarResCtrl.Instance.GetResInStore(WRD.ResTypes.GOLDCOIN) >= _conf.gs_price)
            {
                if (_conf.gs_mode == WBD.BuildingMode.BARRACK || _conf.gs_mode == WBD.BuildingMode.FORTRESS)
                {
                    canBuild = false; //兵营和城堡只能修复，不能建造新的
                }
                else
                    canBuild = true;
            }
            else
                canBuild = false;

            if (canBuild != _canBuild)
            {
                _canBuild = canBuild;
                if(_canBuild == true)
                    _buildingImage.color = Color.white;
                else
                    _buildingImage.color = Color.gray;
            }
        }

        public void RunFixTask(float deltaTime)
		{
			throw new NotImplementedException();
		}

        public void MouthEnter(string value, PointerEventData eventData){ }

        public void MouthExit(string value, PointerEventData eventData){ }

        public void MouthClick(string value, PointerEventData eventData)
        {
            if(_beInited == false)
                return;

            if (_beSelected == false) //第一次被点击，显示详情
            {
                SetSelectStatus(true);
            }
            else //双击，开始建造
            {
                if(Time.time - _lastClickTime < GD.DoubleClickTimeInterval) //两次点击之间的时间间隔小于0.4s认为是双击
                    if(BuildingBeChoose() == true)
                        UIGameMainMenu.Instance.HideMenu();
            }
            _lastClickTime = Time.time;
        }

        public void MouthUp(string value, PointerEventData eventData) { }

        public bool BuildingBeChoose()
        {
            if(_canBuild == false)
                return false;
            if (WarResCtrl.Instance.ConsumeRes(WRD.ResTypes.GOLDCOIN, _conf.gs_price, false, null, out var res) == false) //not has enough gold coin
            {
                _canBuild = false;
                _buildingImage.color = Color.gray;
                return false;
            }

            _beSelected = false;
            _beChooseImage.enabled = false;
            _bgImage.sprite = _notChooseSprite;
            UIConstructTask.Instance.StartConstructTask(WarBuildingCtrl.Instance.GetBdConf(_conf.gs_race, _conf.gs_mode, _conf.gs_subType));
            return true;
        }

        //显示，判断是不是需要置灰
        public void ShowItem()
        {
            if (_conf.gs_mode == WBD.BuildingMode.DEFENCE)
            {
                if (_conf.gs_subType == (int)HumanDefines.DefenceType.BASICTOWER) //只有基础塔可以直接建造，其他的塔都是需要基础塔设计获取
                {
                    if(WarResCtrl.Instance.GetResInStore(WRD.ResTypes.GOLDCOIN) >= _conf.gs_price)
                        _canBuild = false;
                    else
                        _canBuild = true;
                    WarFieldGameManager.Instance.ActiveTask(this, WE.TaskType.NORMAL);
                }
                else
                    _canBuild = false;
            }
            else
                _canBuild = false;

            if(_canBuild == true)
                _buildingImage.color = Color.white;
            else
                _buildingImage.color = Color.gray;
        }

        public void HideItem()
        {
            WarFieldGameManager.Instance.SuspendTask(this, WE.TaskType.NORMAL);
        }

        //ConstructMenu可以来的设置选中状态
        public void SetSelectStatus(bool value)
        {
            if (value == true)
            {
                if (_beSelected == false) //第一次被点击，显示详情
                {
                    _beSelected = true;
                    _beChooseImage.enabled = true;
                    _bgImage.sprite = _beChooseSprite;
                }
                ConstructMenu.Instance.ItemBeSelected(this, _canBuild);
            }
            else
            {
                _beSelected = false;
                _beChooseImage.enabled = false;
                _bgImage.sprite = _notChooseSprite;
            }
        }
#endregion

#region private functions

#endregion
    }
}

