using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

using WarUpgrade;

namespace Entrance
{
    public class EntranceScene : MonoBehaviour
    {
#region public parameters
        public static EntranceScene Instance;
#endregion

#region private parameters
        [SerializeField] private TextMeshProUGUI _startBtText;
        [SerializeField] private TextMeshProUGUI _optionBtText;
        [SerializeField] private TextMeshProUGUI _exitBtText;

        [SerializeField] private ArchiveWindow _archiveWindow;

        private PopOutWindow _curPopWindow;
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
            _curPopWindow = null;
            UpgradeDatabase.Instance.InitUpgradeDatabase("Conf/UpgradeConf");
        }

        private void Start()
        {
            ArchiveWindow.Instance.InitPopOutWindow(this);
        }

        public void ButtonEvent(string btName)
        {
            if (btName == "StartBt")
            {
                _curPopWindow = _archiveWindow;
                _curPopWindow.ShowWindow();
            }
            else if (btName == "OptionBt")
            {

            }
            else if (btName == "ExitBt")
            {

            }
        }

#endregion

#region public functions

        public void PopOutWindowClose(PopOutWindow window)
        {
            if(_curPopWindow != window)
                return;
            _curPopWindow = null;
        }
#endregion

#region private functions

#endregion
    }
}
