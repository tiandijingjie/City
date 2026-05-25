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

    public class SoldierProfileSkillChooseItem : MonoBehaviour, UIMouthActivityIntf
    {
#region public parameters

#endregion

#region private parameters
        [SerializeField] private UIMouthCheck _mouthCheck;
        [SerializeField] private Image _icon;

        private IndividualItem _conf;
        private UIBarrackSpawnSpot _spawnSpot;
        private IndividualItemDescription _description;
        private uint _skillID;
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

        public void Init(IndividualItem conf, UIBarrackSpawnSpot spawnSpot, uint skillId)
        {
            _conf = conf;
            _spawnSpot = spawnSpot;
            _description = conf.GetDescription();
            _skillID = skillId;
            _icon.sprite = Resources.Load<Sprite>($"Textures/UI/WarField/SkillIcons/{_spawnSpot.gs_conf.p_name}/{_description.p_name}");
        }

        public void MouthEnter(string value, PointerEventData eventData)
        {
            Invoke("ShowSkillInfo", UD.TipShowLatency);
        }

        public void MouthExit(string value, PointerEventData eventData)
        {
            HideTip();
        }

        public void MouthClick(string value, PointerEventData eventData)
        {
            _spawnSpot.ChangeSoldierSkill(_skillID);
            HideTip();
        }

        public void MouthUp(string value, PointerEventData eventData) { }
#endregion

#region private functions

        private void ShowSkillInfo()
        {
            string value = $"{_description.p_name}  level:{_description.p_level}\n{_description.p_levelDescription}";
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

