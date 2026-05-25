using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WarField
{
    using UD = UIDefines;
    using WE = WarFieldElements;
    using GD = GlobalDefines;
    using SCD = SysCfgDefines;

    public class UICtrl : MonoBehaviour
    {
#region public parameters

        public static UICtrl Instance;

#endregion

#region private parameters

        [SerializeField] private DrawCard _drawCardView;
        [SerializeField] private UIGameMainMenu _mainMenu;

        private bool _beInited;

#endregion

#region private parameters' get set

        public bool gs_beInited
        {
            get {return  _beInited; }
        }
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
            _beInited = false;

            //_drawCardView.HideDrawCardView();
            _mainMenu.HideMenu();
        }

        public void ButtonEvent(int btEvent)
        {
            switch ((UD.UiEvent)btEvent)
            {
                case UD.UiEvent.SHOWDRAWCARD:
                    ShowDrawCard();
                    break;
                case UD.UiEvent.SHOWSTATUS:
                    ShowStatusView();
                    break;
                default:
                    break;
            }

            return;
        }

#endregion

#region public functions

        public bool InitUiCtrl()
        {
            //IO必须先构建
            new UiIoTask(); //will register self into _uiTaskVector
            UIConstructTask.Instance.InitConstructTask();
            _mainMenu.InitMenu();
            _drawCardView.InitView();

            UiMiniMapCtrl.Instance.InitMiniCtrl();

            //add building upgrade bar to the scene
            GameObject bdUpdateBarPfb = Resources.Load<GameObject>("Prefabs/UI/WarField/BuildingUpgradeBar/UIBuildingUpgradeBar");
            Instantiate(bdUpdateBarPfb, transform);
            SelectionManager.Instance.InitSelectionManager();
            _beInited = true;
            return true;
        }

#endregion

#region private functions

        private void ShowDrawCard()
        {
            _drawCardView.ShowDrawCardView();
        }

        private void ShowStatusView()
        {
            _mainMenu.ShowMenu();
        }

#endregion
    }
}

