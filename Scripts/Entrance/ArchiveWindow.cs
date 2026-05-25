using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using WarArchive;
using WarUpgrade;
using Random = UnityEngine.Random;

namespace Entrance
{
    public class ArchiveWindow : PopOutWindow
    {
#region public parameters

        public static ArchiveWindow Instance = null;
#endregion

#region private parameters
        [SerializeField] private ArchiveChooseItem[] _archiveChooseItems; //存档列表
        [SerializeField] private TextMeshProUGUI _title;
        [SerializeField] private Image _runBtImg, _runBtFireImg;
        [SerializeField] private Sprite _runBtActiveSprite, _runBtInactiveSprite;
        [SerializeField] private Button _runBt;

        //input editor
        [SerializeField] private TMP_InputField _inputField; // 文本输入
        [SerializeField] private GameObject _inputEditor; //编辑存档名字弹窗
        [SerializeField] private TextMeshProUGUI _inputEditorTitle;

        private ArchiveChooseItem _curArchiveItem;
        private bool _archivedNameEditorShowed;
        private Vector2 _showPos, _hidePos;
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
            _archivedNameEditorShowed = false;
            _showPos = transform.position;
            _hidePos = new Vector2(10000, 10000);
            transform.position = _hidePos;
            _inputEditor.SetActive(false);
        }

        private void Update()
        {
            if (_archivedNameEditorShowed == false)
            {
                if (Input.GetKeyDown(KeyCode.Escape) == true)
                {
                    CloseWindow();
                }
            }
        }

        public void ButtonEvent(string btName)
        {
            bool changeArchive = false;
            int newArcIndex = -1;
            switch (btName)
            {
                case "RunBt":
                    if (_curArchiveItem != null && _curArchiveItem.gs_arcSaveRecord != null)
                    {
                        StartArchive();
                    }
                    break;
                case "CloseBt":
                    CloseWindow();
                    break;
                case "Archive_0":
                    ArchiveItemSelect(0);
                    break;
                case "Archive_1":
                    ArchiveItemSelect(1);
                    break;
                case "Archive_2":
                    ArchiveItemSelect(2);
                    break;
                case "Archive_3":
                    ArchiveItemSelect(3);
                    break;
                case "Archive_4":
                    ArchiveItemSelect(4);
                    break;
                case "InputEditorComfirm":
                    HideArchiveNameEditorWindows(true);
                    break;
                case "InputEditorCancel":
                    HideArchiveNameEditorWindows(false);
                    break;
                default:
                    break;
            }

        }

#endregion

#region public functions

        public override void InitPopOutWindow(MonoBehaviour parentUI)
        {
            base.InitPopOutWindow(parentUI);

            List<ArcSaveRecord> archList = Archive.Instance.GetAllSaveRecords();
            foreach (ArcSaveRecord arc in archList)
            {
                int index = arc.p_index;
                ArchiveChooseItem item = _archiveChooseItems[index];
                if (item.InitArchiveChooseItem(arc, index) == false)
                {
                    GameLogger.LogError($"Fail to init archive choose item: {index} with archive {arc}");
                }
            }

            for (int i = 0; i < _archiveChooseItems.Length; i++)
            {
                if(_archiveChooseItems[i].gs_arcSaveRecord == null)
                    _archiveChooseItems[i].InitArchiveChooseItem(null, i);
            }
            SetRunBt();
        }

        //显示编辑存档名字,创建存档的弹窗
        public void ShowArchiveNameEditorWindows(string showName)
        {
            _inputField.text = String.Empty;
            _inputEditor.SetActive(true);
            if(string.IsNullOrEmpty(showName) == false)
                _inputField.text = showName;
        }

        public void HideArchiveNameEditorWindows(bool changeName)
        {
            if (changeName == true)
            {
                string content = _inputField.text;
                if (string.IsNullOrEmpty(content) == false)
                {
                    if (_curArchiveItem.SetArchive(content) == true)
                    {
                        SetRunBt();
                        _inputEditor.SetActive(false);
                    }
                    else
                    {
                        StopAllCoroutines();
                        StartCoroutine(ShakeCoroutine()); //抖动,提示出错
                    }
                }
                else
                    _inputEditor.SetActive(false);
            }
            else
                _inputEditor.SetActive(false);
        }

        //存档被删除
        public void ArchiveItemDelete(ArchiveChooseItem item)
        {
            if(_curArchiveItem != item)
                return;
            SetRunBt(); //更新run button状态
        }

        public override void ShowWindow()
        {
            transform.position = _showPos;
        }

        public override void CloseWindow()
        {
            transform.position = _hidePos;
            EntranceScene.Instance.PopOutWindowClose(this);
        }

#endregion

#region private functions
        //根据选择的不同存档,改变runbutton的显示状态
        private void SetRunBt()
        {
            if (_curArchiveItem != null && _curArchiveItem.gs_arcSaveRecord != null)
            {
                _runBtFireImg.gameObject.SetActive(true);
                _runBtImg.sprite = _runBtActiveSprite;
                _runBt.interactable = true;
            }
            else
            {
                _runBtFireImg.gameObject.SetActive(false);
                _runBtImg.sprite = _runBtInactiveSprite;
                _runBt.interactable = false;
            }
        }

        //选中存档
        private void ArchiveItemSelect(int index)
        {
            if (_curArchiveItem == _archiveChooseItems[index])
            {
                _curArchiveItem.BeSelected();//空存档需要再次弹出创建存档界面
                return;
            }

            if(_curArchiveItem != null)
                _curArchiveItem.BeUnSelected();
            _curArchiveItem = _archiveChooseItems[index];
            _curArchiveItem.BeSelected();
            SetRunBt();
        }

        private bool StartArchive()
        {
            if(Archive.Instance.EnterArchive(_curArchiveItem.gs_arcSaveRecord) == false)
                return false;
            UpgradeDatabase.Instance.LoadUpgradeByArchive(); //应该放到loading的阶段
            return true;
        }

        private IEnumerator ShakeCoroutine()
        {
            float shakeDuration = 0.15f; // 持续时间
            float shakeStrength = 20f;  // 抖动幅度

            // 1. 记录原始的锚点坐标 (这是最安全的做法)
            // 如果 targetUI 为空，就获取当前物体
            RectTransform targetUI = _inputEditor.GetComponent<RectTransform>();

            Vector2 originalPos = targetUI.anchoredPosition;
            float elapsed = 0.0f;

            while (elapsed < shakeDuration)
            {
                // 2. 计算一个随机偏移量
                // Random.insideUnitCircle 返回半径为1的圆内随机点
                float x = Random.Range(-1f, 1f) * shakeStrength;
                // 如果只想左右抖动，Y轴设为0；如果想乱抖，把下面这行改成 originalPos.y + Random.Range(-1f, 1f) * shakeStrength
                float y = originalPos.y;

                // 3. 应用坐标
                targetUI.anchoredPosition = new Vector2(originalPos.x + x, y);

                elapsed += Time.deltaTime;
                yield return null; // 等待下一帧
            }

            // 4. 关键：强制归位！确保没有任何残留偏移
            targetUI.anchoredPosition = originalPos;
        }
#endregion
    }
}
