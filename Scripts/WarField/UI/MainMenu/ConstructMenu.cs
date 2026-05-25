using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WarField
{
    using WE = WarFieldElements;
    using WBD = WarBuildingDefines;

    public class ConstructMenu : SubMenu
    {
#region public parameters
        public static ConstructMenu Instance;
#endregion

#region private parameters
        [SerializeField] private GameObject _bdListContent;
        [SerializeField] private GameObject _bdChooseItemPfb;
        [SerializeField] private Image _infoIcon;
        [SerializeField] private Text _nameText, _descriptionText, _dataText;
        [SerializeField] private RectTransform _constructBt;
        [SerializeField] private RectTransform _viewPort;

        private List<BuildingChooseItem> _itemList;
        private BuildingChooseItem _curItem = null;
        private RectTransform _curItemRect;
        private Vector2 _itemSize = new Vector2(0, 0);

#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        protected override void Awake()
        {
            if (ReferenceEquals(Instance, null) == false)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            base.Awake();
        }

#endregion

#region public functions

        protected override void OnShowMenu()
        {
            BuildingConf conf;
            //更新defence种类 其他建筑不会有什么更新
            for (int i = 1; i < (int)HumanDefines.DefenceType.MAX; i++)
            {
                conf = WarBuildingCtrl.Instance.GetBdConf(WE.RaceType.Human, WBD.BuildingMode.DEFENCE, i);
                bool find = false;
                foreach (var tmp in _itemList)
                {
                    if (tmp.gs_bdConf == conf)
                    {
                        find = true;
                        break;
                    }
                }
                if(find == false)
                    AddBuildingChooseItem(conf);
            }

            foreach (var tmp in _itemList)
            {
                tmp.ShowItem();
            }

            SetConstructBt();
        }

        protected override void OnHideMenu()
        {
            foreach (var tmp in _itemList)
            {
                tmp.HideItem();
            }
        }

        //item通知被选中
        public void ItemBeSelected(BuildingChooseItem item, bool canBuild)
        {
            if(ReferenceEquals(_curItem, null) == false && _curItem != item)
                _curItem.SetSelectStatus(false);
            _curItem = item;
            _curItemRect = _curItem.GetComponent<RectTransform>();
            ShowBdInfo(_curItem.gs_bdConf);
            SetConstructBt();
        }

        public void ConstructEvent()
        {
            if(_curItem.BuildingBeChoose() == false) //没有成功开始建设
                return;
            UIGameMainMenu.Instance.HideMenu();
        }

        public void OnScroll()
        {
            SetConstructBt();
        }
#endregion

#region private functions

        protected override void OnInit()
        {
            _itemList = new List<BuildingChooseItem>();

            //fortress
            BuildingConf conf;
            for (int i = 1; i < (int)HumanDefines.FortressType.MAX; i++)
            {
                conf = WarBuildingCtrl.Instance.GetBdConf(WE.RaceType.Human, WBD.BuildingMode.FORTRESS, i);
                var item = AddBuildingChooseItem(conf);
                if (ReferenceEquals(item, null) == false) //默认显示fortress
                {
                    item.SetSelectStatus(true);
                    ItemBeSelected(item, false);
                }
            }

            //barrack
            for (int i = 1; i < (int)HumanDefines.BarrackType.MAX; i++)
            {
                conf = WarBuildingCtrl.Instance.GetBdConf(WE.RaceType.Human, WBD.BuildingMode.BARRACK, i);
                AddBuildingChooseItem(conf);
            }

            //portal
            for (int i = 1; i < (int)NeutralDefines.PortalType.MAX; i++)
            {
                conf = WarBuildingCtrl.Instance.GetBdConf(WE.RaceType.Neutral, WBD.BuildingMode.PORTAL, i);
                AddBuildingChooseItem(conf);
            }

            //defence
            for (int i = 1; i < (int)HumanDefines.DefenceType.MAX; i++)
            {
                conf = WarBuildingCtrl.Instance.GetBdConf(WE.RaceType.Human, WBD.BuildingMode.DEFENCE, i);
                AddBuildingChooseItem(conf);
            }

            foreach (var tmp in _itemList)
            {
                tmp.ShowItem();
            }
        }

        private BuildingChooseItem AddBuildingChooseItem(BuildingConf conf)
        {
            if(conf == null || conf.IsContructable() == false)//部分防御塔需要通过抽卡获取
                return null;

            BuildingChooseItem chooseItem = Instantiate(_bdChooseItemPfb, _bdListContent.transform).GetComponent<BuildingChooseItem>();
            if (ReferenceEquals(chooseItem, null) == true)
            {
                GameLogger.LogError($"Fail to add building choose item {conf.gs_name}");
                return null;
            }

            chooseItem.InitChooseItem(conf);
            _itemList.Add(chooseItem);
            return chooseItem;
        }

        private void ShowBdInfo(BuildingConf conf)
        {
            _nameText.text = conf.gs_name;
            _descriptionText.text = conf.gs_description;
            _dataText.text = conf.GetDataInfo();
        }

        private void SetConstructBt()
        {
            if (_curItem == null || _curItem.gs_canBuild == false) // can not build this building
            {
                _constructBt.gameObject.SetActive(false);
                return;
            }

            if (_itemSize.x == 0)
                _itemSize = new Vector2(_curItemRect.sizeDelta.x, _curItemRect.sizeDelta.y * 1 / 3);
            _constructBt.position = (Vector2)_curItemRect.position + _itemSize;

            Vector3[] corners = new Vector3[4];
            _constructBt.GetWorldCorners(corners);

            if (RectTransformUtility.RectangleContainsScreenPoint(_viewPort, corners[0], null) == false ||
                RectTransformUtility.RectangleContainsScreenPoint(_viewPort, corners[1], null) == false)
                _constructBt.gameObject.SetActive(false);
            else
                _constructBt.gameObject.SetActive(true);
        }
#endregion
    }
}

