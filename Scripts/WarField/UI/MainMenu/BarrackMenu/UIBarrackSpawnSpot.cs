using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Image = UnityEngine.UI.Image;

namespace WarField
{
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using UD = UIDefines;

    public class UIBarrackSpawnSpot : MonoBehaviour, UIMouthActivityIntf
    {
#region public parameters

#endregion

#region private parameters

        [SerializeField] private GameObject _lock, _unlock;
        [SerializeField] private Image _sdIcon, _skillIcon, _talentIcon;
        [SerializeField] private GameObject _startButton, _pauseButton;
        [SerializeField] private Text _sdName;
        [SerializeField] private UIMouthCheck[] _mouthCheckArr;
        [SerializeField] private Image _progressPic; //训练士兵的环形进度条

        private bool _isUnlocked = false;
        private int _index; //表示在barrack中生产的spot的index， 在没有解锁的时候是-1
        private UIBarrackSection _parrent = null;
        private SoldierConf _conf = null;
        private IndividualData _individualData = null;
        private IndividualItemDescription _skillDescription = null, _talentDescription = null;
        private uint _skillId = 0; //IndividualDataType
        private FriendlyBarrack _barrack = null;
        private SD.TroopType _troopType = 0;
        private bool _beInited = false;

        private bool _tipShowed = false;
        private string _tipValue = null;
#endregion

#region private parameters' get set

        public SoldierConf gs_conf
        {
            get { return _conf; }
        }

        public IndividualData gs_individualData
        {
            get { return _individualData; }
        }

        public uint gs_skillId
        {
            get { return _skillId; }
        }
#endregion

#region Unity callbacks

        private void Awake()
        {
            foreach (var tmp in _mouthCheckArr)
            {
                tmp.RegisterReceiver(this);
            }
            _startButton.SetActive(false);
        }

        private void OnDisable()
        {
            HideTip();
        }

        private void OnDestroy()
        {
            HideTip();
        }

        private void Update()
        {
            if(_isUnlocked == false)
                return;

            //update progress
            float progress = 1 - _barrack.gs_spawnTimeLeft[_index] / _conf.p_spawnTimeInCycle;
            _progressPic.fillAmount = progress;
        }

        //在解锁状态下被选中unlock上的button
        public void BeSelectedOnUnlocked()
        {
            if(_isUnlocked == false)
                return;
            _parrent.SpawnSpotBeSelect(this, false);
        }

        public void UnlockButton()
        {
            if (_isUnlocked == true)
            {
                GameLogger.LogError("Is already unlocked");
                return;
            }
            SoldierConf conf = SoldierCtrl.Instance.GetHumanBasicTypeSoldier(_troopType);
            if(EnableSpawnSpot(conf) == true)
                _parrent.SpawnSpotBeSelect(this, false);
        }

#endregion

#region public functions

        public bool Init(SD.TroopType troop, SoldierConf conf, UIBarrackSection parrent, int index)
        {
            if (_beInited == true)
                return false;

            _index = index;
            _troopType = troop;
            _barrack = WarBuildingCtrl.Instance.GetFriendlyBarrackByTroop(troop);
            if (conf != null)
            {
                _isUnlocked = true;
                _lock.SetActive(!_isUnlocked);
                _unlock.SetActive(_isUnlocked);

                ShowInfo(conf);
            }
            else
            {
                _isUnlocked = false;
                _lock.SetActive(!_isUnlocked);
                _unlock.SetActive(_isUnlocked);
            }
            _parrent = parrent;
            _beInited = true;
            return true;
        }

        //upgrade / downgrade to another soldier
        public void ChangeSpawnSoldier(SoldierConf conf)
        {
            if(conf == _conf || conf.p_level == _conf.p_level)
                return;

            if (conf.p_level == SD.SoldierLevel.HIGHLEVEL)
                _barrack.UpgradeSpawnSpot(_index, conf.p_soldierType);
            else
                _barrack.DowngradeSpawnSpot(_index);
            ShowInfo(conf);
            _parrent.SpawnSpotBeSelect(this, true);
        }

        public void ChangeSoldierSkill(uint skillId)
        {
            _barrack.ChangeSkill(_index, skillId);
            ShowInfo(_conf);
            _parrent.SpawnSpotBeSelect(this, true);
        }

        public void MouthEnter(string value, PointerEventData eventData)
        {
            Invoke("ShowTip", UD.TipShowLatency);
            _tipValue = value;
        }

        public void MouthExit(string value, PointerEventData eventData)
        {
            HideTip();
        }

        public void MouthClick(string value, PointerEventData eventData)
        {
            switch (value)
            {
                case "Pause":
                    if (_barrack != null)
                    {
                        _barrack.ChangeSpawnActivity(_index, false);
                        _pauseButton.SetActive(false);
                        _startButton.SetActive(true);
                    }
                    break;
                case "Start":
                    if (_barrack != null)
                    {
                        _barrack.ChangeSpawnActivity(_index, true);
                        _startButton.SetActive(false);
                        _pauseButton.SetActive(true);
                    }
                    break;
                default:
                    break;
            }
        }

        public void MouthUp(string value, PointerEventData eventData) { }
#endregion

#region private functions

        private bool EnableSpawnSpot(SoldierConf conf)
        {
            if(_isUnlocked == true)
                return false;

            int index = _barrack.AddNewSpawnSpot();
            if (index < 0)
            {
                GameLogger.LogError($"Enable spawn spot {conf.p_name} failed");
                return false;
            }
            _index = index;
            _isUnlocked = true;
            _lock.SetActive(!_isUnlocked);
            _unlock.SetActive(_isUnlocked);

            ShowInfo(conf);
            return true;
        }

        private void ShowInfo(SoldierConf conf)
        {
            _conf = conf;
            if (_conf.p_level == SD.SoldierLevel.HIGHLEVEL)
            {
                _individualData = SoldierCtrl.Instance.GetSdIndividualData(WE.RaceType.Human, _conf.p_troop, _conf.p_soldierType);
                _talentDescription = _individualData.gs_individualItems[1].GetDescription();
                var talentPic = Resources.Load<Sprite>($"Textures/UI/WarField/SkillIcons/{_conf.p_name}/Talent");
                if (ReferenceEquals(talentPic, null) == false)
                {
                    _talentIcon.gameObject.SetActive(true);
                    _talentIcon.sprite = talentPic;
                }
                else
                    _talentIcon.gameObject.SetActive(false);
            }
            else
            {
                _individualData = null;
                _talentDescription = null;
                _talentIcon.gameObject.SetActive(false);
            }

            _sdName.text = _conf.p_name;
            _sdIcon.sprite = Resources.Load<Sprite>($"Textures/UI/WarField/SoldierIcons/BodyIcons/{_conf.p_name}");
            if (_barrack.gs_spawnEnable[_index] == true)
            {
                _startButton.SetActive(false);
                _pauseButton.SetActive(true);
            }
            else
            {
                _startButton.SetActive(true);
                _pauseButton.SetActive(false);
            }
            UpdateData();
        }

        //update data of the talent and skill description, for basic soldiers don't have skill/talent
        private void UpdateData()
        {
            if (_conf.p_level == SD.SoldierLevel.HIGHLEVEL)
            {
                uint tmpId = _barrack.gs_skillOfSpot[_index];
                if (_skillId != tmpId && tmpId != 0)
                {
                    _skillId = tmpId;
                    _skillDescription = _individualData.gs_individualItems[_skillId].GetDescription();
                    _skillIcon.sprite = Resources.Load<Sprite>($"Textures/UI/WarField/SkillIcons/{_conf.p_name}/{_skillDescription.p_name}");
                    _skillIcon.gameObject.SetActive(true);
                }
            }
            else
            {
                _skillId = 0;
                _skillDescription = null;
                _skillIcon.gameObject.SetActive(false);
            }
        }

        private void ShowTip()
        {
            _tipShowed = true;
            switch (_tipValue)
            {
                case "Progress":
                    UITip.Instance.ShowTip("Soldier Trainning Progress", Input.mousePosition + new Vector3(0f, 20f, 0));
                    break;
                case "Pause":
                    UITip.Instance.ShowTip("Pause Trainning", Input.mousePosition + new Vector3(0f, 20f, 0));
                    break;
                case "Start":
                    UITip.Instance.ShowTip("Start Trainning", Input.mousePosition + new Vector3(0f, 20f, 0));
                    break;
                case "SkillIcon":
                {
                    var description = _individualData.gs_individualItems[_skillId].GetDescription();
                    string value = $"{description.p_name}  level:{description.p_level}\n{description.p_levelDescription}";
                    UITip.Instance.ShowTip(value, Input.mousePosition + new Vector3(0f, 20f, 0));
                }
                    break;
                case "TalentIcon":
                {
                    string value = "Talent\n" + _individualData.gs_individualItems[1].GetDescription().p_levelDescription;
                    UITip.Instance.ShowTip(value, Input.mousePosition + new Vector3(0f, 20f, 0));
                }
                    break;
                default:
                    break;
            }
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

