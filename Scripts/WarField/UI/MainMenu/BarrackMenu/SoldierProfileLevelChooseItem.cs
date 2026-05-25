using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WarField
{
    using SD = SoldierDefines;
    using WE = WarFieldElements;
    using UD = UIDefines;

    public class SoldierProfileLevelChooseItem : MonoBehaviour, UIMouthActivityIntf
    {
#region public parameters

#endregion

#region private parameters
        [SerializeField] private UIMouthCheck _mouthCheck;
        [SerializeField] private Image _icon;
        [SerializeField] private GameObject _upgrade, _downgrade;

        private SoldierConf _conf;
        private UIBarrackSpawnSpot _spawnSpot;
        private bool _tipShowed = false;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        private void Awake()
        {
            _mouthCheck.RegisterReceiver(this);
        }

        private void OnDisable()
        {
            HideTip();
        }

        private void OnDestroy()
        {
            HideTip();
        }

#endregion

#region public functions

        public void Init(SoldierConf conf, UIBarrackSpawnSpot spawnSpot)
        {
            _conf = conf;
            _icon.sprite = Resources.Load<Sprite>($"Textures/UI/WarField/SoldierIcons/UpgradeIcons/{_conf.p_name}");
            _spawnSpot = spawnSpot;
        }

        public void MouthEnter(string value, PointerEventData eventData)
        {
            if(_conf.p_level == SD.SoldierLevel.HIGHLEVEL)
                _upgrade.SetActive(true);
            else
                _downgrade.SetActive(true);
            Invoke("ShowSoldierProfile", UD.TipShowLatency);
        }

        public void MouthExit(string value, PointerEventData eventData)
        {
            if(_conf.p_level == SD.SoldierLevel.HIGHLEVEL)
                _upgrade.SetActive(false);
            else
                _downgrade.SetActive(false);

            HideTip();
        }

        public void MouthClick(string value, PointerEventData eventData)
        {
            _spawnSpot.ChangeSpawnSoldier(_conf);
            HideTip();
        }

        public void MouthUp(string value, PointerEventData eventData) { }
#endregion

#region private functions

        private void ShowSoldierProfile()
        {
            string value = $"{_conf.p_name}\n{_conf.p_description}";
            UITip.Instance.ShowTip(value, Input.mousePosition + new Vector3(0f, 60f, 0));
            _tipShowed = true;
        }

        private void HideTip()
        {
            if (_tipShowed == true)
            {
                UITip.Instance.HideTip();
                _tipShowed = false;
            }
            else
                CancelInvoke();
        }
#endregion
    }
}
