using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WarField
{
    using WBD = WarBuildingDefines;
    using UD = UIDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;

    //only use for friendly building
    public class UIBarrackSection : MonoBehaviour, UIMouthActivityIntf
    {
#region public parameters

#endregion

#region private parameters

        [SerializeField] private SD.TroopType _troopType;

        //profile
        [SerializeField] private GameObject _upgradeButton, _downgradeButton, _skillButton;
        [SerializeField] private Text _sdData, _sdDescription, _sdName;
        [SerializeField] private Image _sdIcon;

        //level choose
        [SerializeField] private GameObject _levelChooseList;
        [SerializeField] private Transform _levelChooseListContent;
        [SerializeField] private GameObject _levelChooseListItem;

        //skill choose list
        [SerializeField] private Image _skillChooseButtonImage;
        [SerializeField] private GameObject _skillChooseList;
        [SerializeField] private Transform _skillChooseListContent;
        [SerializeField] private GameObject _skillChooseListItem;

        //spawn spot list
        [SerializeField] private UIBarrackSpawnSpot[] _spawnSpots;

        //tips checker, when mouth move in these checkers will show tip window
        [SerializeField] private List<UIMouthCheck> _mouthChecks;

        private bool _isShowed = false;
        private bool _beInited;
        private bool _tipShowed = false;

        //profile showed
        private UIBarrackSpawnSpot _profileSpot = null;//the  spawn spot which profile is showing
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks
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

        public bool Init()
        {
            if (_troopType == SD.TroopType.MIN)
            {
                GameLogger.LogError($"Troop Type not set for the section {name}");
                return false;
            }

            SoldierConf[] soldierConf = WarBuildingCtrl.Instance.GetFriendlyBarrackByTroop(_troopType).gs_spawnArr;
            int count = _spawnSpots.Length, length = soldierConf.Length;
            for (int i = 0; i < count; i++)
            {
                if(i < length)
                    _spawnSpots[i].Init(_troopType, soldierConf[i], this, i);
                else
                    _spawnSpots[i].Init(_troopType, null, this, -1);
            }

            foreach (var tmp in _mouthChecks)
            {
                tmp.RegisterReceiver(this);
            }

            _beInited = true;
            return true;
        }

        public void ShowMenu()
        {
            _isShowed = true;
            _tipShowed = false;
            SpawnSpotBeSelect(_spawnSpots[0], false); //default show 0 spot
        }

        public void HideMenu()
        {
            _isShowed = false;
            HideTip();
        }

        public void UpgradeButtonEvent()
        {
            if (_levelChooseList.activeSelf == true) //hide
            {
                foreach (Transform child in _levelChooseListContent)
                {
                    GameObject.Destroy(child.gameObject);
                }
                _levelChooseList.SetActive(false);
                _sdDescription.gameObject.SetActive(true);
            }
            else
            {
                _sdDescription.gameObject.SetActive(false);
                _levelChooseList.SetActive(true);
                List<SoldierConf> confs = SoldierCtrl.Instance.GetHighLevelTypeSoldiers(WE.RaceType.Human, _troopType);
                foreach (var conf in confs)
                {
                    // if(SoldierCtrl.Instance.IsSoldierAvailable(conf.p_race, conf.p_troop, conf.p_soldierType) == false)
                    //     continue;
                    SoldierProfileLevelChooseItem item = Instantiate(_levelChooseListItem, _levelChooseListContent)
                        .GetComponent<SoldierProfileLevelChooseItem>();
                    item.Init(conf, _profileSpot);
                }
            }
        }

        public void DowngradeButtonEvent()
        {
            if (_levelChooseList.activeSelf == true) //hide
            {
                int count = _levelChooseListContent.childCount;
                foreach (Transform child in _levelChooseListContent)
                {
                    GameObject.Destroy(child.gameObject);
                }
                _levelChooseList.SetActive(false);
                _sdDescription.gameObject.SetActive(true);
            }
            else
            {
                _sdDescription.gameObject.SetActive(false);
                _levelChooseList.SetActive(true);
                var conf = SoldierCtrl.Instance.GetHumanBasicTypeSoldier(_troopType);
                SoldierProfileLevelChooseItem item = Instantiate(_levelChooseListItem, _levelChooseListContent)
                    .GetComponent<SoldierProfileLevelChooseItem>();
                item.Init(conf, _profileSpot);

            }
        }

        public void SkillButtonEvent()
        {
            if (_skillChooseList.activeSelf == true) //hide
            {
                int count = _skillChooseListContent.childCount;
                foreach (Transform child in _skillChooseListContent)
                {
                    GameObject.Destroy(child.gameObject);
                }
                _skillChooseList.SetActive(false);
                _sdDescription.gameObject.SetActive(true);
            }
            else
            {
                _sdDescription.gameObject.SetActive(false);
                _skillChooseList.SetActive(true);

                var items = SoldierCtrl.Instance.GetSdIndividualData(WE.RaceType.Human, _troopType, _profileSpot.gs_conf.p_soldierType)
                    .gs_individualItems;
                for (int i = 2; i < items.Length; i++)
                {
                    // if(items[i].IsEnabled() == false) //技能是否已经获取
                    //     continue;
                    if(_profileSpot.gs_skillId == i)
                        continue;

                    SoldierProfileSkillChooseItem script = Instantiate(_skillChooseListItem, _skillChooseListContent)
                        .GetComponent<SoldierProfileSkillChooseItem>();
                    script.Init(items[i], _profileSpot, (uint)i);
                }
            }
        }

        public void SpawnSpotBeSelect(UIBarrackSpawnSpot spot, bool isUpdated)
        {
            if(_isShowed == false)
                return;

            if (spot != _profileSpot || isUpdated == true)
            {
                _profileSpot = spot;
                _sdName.text = _profileSpot.gs_conf.p_name;
                _sdIcon.sprite = Resources.Load<Sprite>($"Textures/UI/WarField/SoldierIcons/BodyIcons/{_profileSpot.gs_conf.p_name}");
                _sdData.text = $"HP:      {_profileSpot.gs_conf.p_health} + {_profileSpot.gs_conf.p_hpInc}\n" +
                               $"攻击力:   {_profileSpot.gs_conf.p_damage}\n" +
                               $"攻击速度: {_profileSpot.gs_conf.p_attackSpeed}\n" +
                               $"护甲:    {_profileSpot.gs_conf.p_phyArmor}\n" +
                               $"移动速度: {_profileSpot.gs_conf.p_moveSpeed}\n" +
                               $"生产时间: {_profileSpot.gs_conf.p_spawnTime}\n";
                _sdDescription.text = _profileSpot.gs_conf.p_description;
                if (_profileSpot.gs_conf.p_level == SD.SoldierLevel.BASICLEVEL)
                {
                    _downgradeButton.SetActive(false);
                    _upgradeButton.SetActive(true);
                    _skillButton.SetActive(false);
                }
                else //高级兵
                {
                    _downgradeButton.SetActive(true);
                    _upgradeButton.SetActive(false);
                    _skillButton.SetActive(true);

                    string talentDes = _profileSpot.gs_individualData.gs_individualItems[1].GetDescription().p_levelDescription;
                    _sdDescription.text += "\n天赋:" + talentDes;

                    if (_profileSpot.gs_skillId != 0)
                    {
                        var skillDescription = _profileSpot.gs_individualData.gs_individualItems[_profileSpot.gs_skillId].GetDescription();
                        _skillChooseButtonImage.sprite = Resources.Load<Sprite>($"Textures/UI/WarField/SkillIcons" +
                                                                        $"/{_profileSpot.gs_conf.p_name}/{skillDescription.p_name}");
                    }
                    else
                    {
                        _skillChooseButtonImage.sprite =
                            Resources.Load<Sprite>($"Textures/UI/WarField/SkillIcons/SkillNone");
                    }
                }
            }
            foreach (Transform child in _levelChooseListContent)
            {
                GameObject.Destroy(child.gameObject);
            }
            _skillChooseList.SetActive(false);
            foreach (Transform child in _skillChooseListContent)
            {
                GameObject.Destroy(child.gameObject);
            }
            _levelChooseList.SetActive(false);
            _sdDescription.gameObject.SetActive(true);//update 的时候需要重现显示出来
        }

        public void MouthEnter(string value, PointerEventData eventData)
        {
            if (_isShowed == false)
                return;

            switch (value)
            {
                case "Skill":
                    Invoke("ShowSkillTip", UD.TipShowLatency);
                    break;
                case "Upgrade":
                    Invoke("ShowUpgradeTip", UD.TipShowLatency);
                    break;
                case "Downgrade":
                    Invoke("ShowDowngradeTip", UD.TipShowLatency);
                    break;
                default:
                    break;
            }
        }

        public void MouthExit(string value, PointerEventData eventData)
        {
            //not check _isShowed,because maybe the menu close by keyboard
            if (_tipShowed == true)
                UITip.Instance.HideTip();
            else
                CancelInvoke();
            _tipShowed = false;
        }

        public void MouthClick(string value, PointerEventData eventData)
        {
            //do nothing
        }

        public void MouthUp(string value, PointerEventData eventData) { }
#endregion

#region private functions

        private void ShowSkillTip()
        {
            _tipShowed = true;
            if (_profileSpot.gs_skillId > 0)
            {
                var description = _profileSpot.gs_individualData.gs_individualItems[_profileSpot.gs_skillId].GetDescription();
                string value = $"{description.p_name}  level:{description.p_level}\n{description.p_levelDescription}";
                UITip.Instance.ShowTip(value, Input.mousePosition + new Vector3(0f, 40f, 0));
            }
            else
            {
                UITip.Instance.ShowTip("No Skill", Input.mousePosition + new Vector3(0f, 40f, 0));
            }
        }

        private void ShowUpgradeTip()
        {
            _tipShowed = true;
            UITip.Instance.ShowTip("Show Upgrade list", Input.mousePosition + new Vector3(0f, 40f, 0));
        }

        private void ShowDowngradeTip()
        {
            _tipShowed = true;
            UITip.Instance.ShowTip("Show Downgrade list", Input.mousePosition + new Vector3(0f, 40f, 0));
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

