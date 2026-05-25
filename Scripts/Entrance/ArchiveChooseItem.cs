using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using WarArchive;

namespace Entrance
{
    public class ArchiveChooseItem : MonoBehaviour
    {
#region public parameters

#endregion

#region private parameters

        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _timeText;
        [SerializeField] private TextMeshProUGUI _emptyText;
        [SerializeField] private GameObject _modifyBt, _deleteBt;
        [SerializeField] private Image _bgImg;
        [SerializeField] private Sprite _bgSelectPic, _bgUnSelectPic;

        private bool _beSelected;
        private int _index; //在存档列表中的序号
        private ArcSaveRecord _arcSaveRecord = null;
        private bool _beInited = false;
#endregion

#region private parameters' get set

        public ArcSaveRecord gs_arcSaveRecord
        {
            get { return _arcSaveRecord; }
        }
#endregion

#region Unity callbacks

        private void Awake()
        {
            _index = -1;
            _arcSaveRecord = null;
            _bgImg.sprite = _bgUnSelectPic;
            _beInited = false;
        }

        public void ButtonEvent(string btName)
        {
            switch (btName)
            {
                case "Select":
                    BeSelected();
                    break;
                case "ModifyBt":
                    ArchiveWindow.Instance.ShowArchiveNameEditorWindows(_arcSaveRecord.p_displayName);
                    break;
                case "DeleteBt":
                    Archive.Instance.DeleteArchive(_arcSaveRecord);
                    _arcSaveRecord = null;
                    ShowArchiveInfo();
                    ArchiveWindow.Instance.ArchiveItemDelete(this);
                    break;
                default:
                    break;
            }
        }
#endregion

#region public functions

        public bool InitArchiveChooseItem(ArcSaveRecord record, int index)
        {
            if(_beInited == true)
                return false;

            _index = index;
            _arcSaveRecord = record;
            ShowArchiveInfo();
            _beInited = true;
            return true;
        }

        public bool SetArchive(string archiveName)
        {
            if(_beSelected == false)
                return false;

            if (_arcSaveRecord == null && string.IsNullOrEmpty(archiveName) == false)
            {
                if (CreateArchive(archiveName) == true)
                {
                    _modifyBt.SetActive(true);
                    _deleteBt.SetActive(true);
                    return true;
                }
                return false;
            }
            else
            {
                if (Archive.Instance.ChangeArchiveName(_arcSaveRecord, archiveName) == true)
                {
                    ShowArchiveInfo();
                    return true;
                }
                return false;
            }
        }

        public void BeSelected()
        {
            if (_arcSaveRecord == null)
            {
                _bgImg.sprite = _bgSelectPic;
                ArchiveWindow.Instance.ShowArchiveNameEditorWindows(null); //弹出创建存档窗口
            }
            else
            {
                _bgImg.sprite = _bgSelectPic;
                _modifyBt.SetActive(true);
                _deleteBt.SetActive(true);
            }
            _beSelected = true;
        }

        public void BeUnSelected()
        {
            if(_beSelected == false)
                return;
            _beSelected = false;
            _bgImg.sprite = _bgUnSelectPic;
            _modifyBt.SetActive(false);
            _deleteBt.SetActive(false);
        }
#endregion

#region private functions

        private void ShowArchiveInfo()
        {
            if (_arcSaveRecord == null)
            {
                _titleText.gameObject.SetActive(false);
                _timeText.gameObject.SetActive(false);
                _modifyBt.SetActive(false);
                _deleteBt.SetActive(false);

                _emptyText.gameObject.SetActive(true);
            }
            else
            {
                _titleText.gameObject.SetActive(true);
                _timeText.gameObject.SetActive(true);
                _titleText.text = _arcSaveRecord.p_displayName;
                _timeText.text = "时间: " + _arcSaveRecord.p_createdAt;

                _emptyText.gameObject.SetActive(false);
            }
        }

        private bool CreateArchive(string arcName)
        {
            ArcSaveRecord record = Archive.Instance.CreateArchive(arcName, _index);
            if (record == null)
            {
                GameLogger.LogError($"Failed to create archive for {arcName}");
                return false;
            }

            _arcSaveRecord = record;
            ShowArchiveInfo();
            return true;
        }

#endregion
    }
}
