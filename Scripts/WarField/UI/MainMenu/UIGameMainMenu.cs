using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace  WarField
{
    using WE = WarFieldElements;
    using UD = UIDefines;
    using SD = SoldierDefines;

    public class UIGameMainMenu : MonoBehaviour, UIMouthActivityIntf
    {
#region public parameters

        static public UIGameMainMenu Instance = null;
#endregion

#region private parameters

        private enum MenuType
        {
            MIN = 0,
            SOLDIERMENU,
            BUILDINGMENU,
            HEROMENU,
            ITEMMENU,
            MAX,
        }

        [SerializeField] private SubMenu[] _menuArray;
        [SerializeField] private UIMouthCheck[] _mouthCheckArray;
        [SerializeField] private Image[] _selectTitles;
        [SerializeField] private RectTransform _selectBar;

        private MenuType _curMenuType;
        private bool _beInied;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _beInied = false;
            _curMenuType = MenuType.MIN;
        }

        //exit button event
        public void Exit()
        {
            if(_beInied == false)
                return;

            HideMenu();
        }

#endregion

#region public functions

        public bool InitMenu()
        {
            if (_beInied == true)
                return false;

            _menuArray[(int)MenuType.SOLDIERMENU].InitSubMenu();
            _menuArray[(int)MenuType.BUILDINGMENU].InitSubMenu();
            _menuArray[(int)MenuType.HEROMENU].InitSubMenu();
            _menuArray[(int)MenuType.ITEMMENU].InitSubMenu();

            _menuArray[(int)MenuType.SOLDIERMENU].HideMenu();
            _menuArray[(int)MenuType.BUILDINGMENU].HideMenu();
            _menuArray[(int)MenuType.HEROMENU].HideMenu();
            _menuArray[(int)MenuType.ITEMMENU].HideMenu();

            for (int i = 0; i < _mouthCheckArray.Length; i++)
            {
                _mouthCheckArray[i].RegisterReceiver(this);
            }

            _beInied = true;
            for (int i = 1; i < _menuArray.Length; i++)
            {
                _menuArray[i].transform.position = new Vector3(-10000, 0, 0);
            }
            ChooseMenu(MenuType.BUILDINGMENU); //must call after _beInied be set
            HideMenu();
            return _beInied;
        }

        public void ShowMenu()
        {
            if(_beInied == false)
                return;

            for (int i = 1; i < (int)MenuType.MAX; i++)
            {
                if (i == (int)_curMenuType)
                {
                    _menuArray[i].ShowMenu();
                    break;
                }
            }

            transform.localPosition = Vector3.zero;
            _menuArray[(int)_curMenuType].transform.position = new Vector3(0, 0, 0);  //init阶段ChooseMenu并没有设置正确的位置，显示的时候需要再设置一下
        }

        public void HideMenu()
        {
            if(_beInied == false)
                return;
            _menuArray[(int)_curMenuType].HideMenu();
            transform.localPosition = new Vector3(0, 10000, 0);
        }

        public void MouthEnter(string value, PointerEventData eventData) { }

        public void MouthExit(string value, PointerEventData eventData) { }

        public void MouthClick(string value, PointerEventData eventData)
        {
            MenuType index = Enum.Parse<MenuType>(value);// 传过来的是_selectTitles的index
            ChooseMenu(index);
        }

        public void MouthUp(string value, PointerEventData eventData) { }
#endregion

#region private functions

        private void ChooseMenu(MenuType type)
        {
            if(_beInied == false)
                return;

            if(type == _curMenuType)
                return;

            if (_curMenuType != MenuType.MIN) //_curMenuType == MenuType.MIN 是初始化
            {
                _selectTitles[(int)_curMenuType].color = Color.gray;
                _selectTitles[(int)_curMenuType].transform.localScale = new Vector3(1, 1, 1);
                _menuArray[(int)_curMenuType].HideMenu();
                _menuArray[(int)_curMenuType].transform.position = new Vector3(-10000, 0, 0);
            }

            _curMenuType = type;
            if (type != MenuType.MIN) //type == MenuType.MIN 是隐藏mainmenu
            {
                _selectTitles[(int)_curMenuType].color = Color.white;
                _selectTitles[(int)_curMenuType].transform.localScale = new Vector3(1.2f, 1.2f, 1);
                _selectBar.position = new Vector2(_selectTitles[(int)_curMenuType].transform.position.x, _selectBar.position.y);
                _menuArray[(int)_curMenuType].ShowMenu();
                _menuArray[(int)_curMenuType].transform.position = new Vector3(0, 0, 0);
            }
        }

#endregion
    }
}

