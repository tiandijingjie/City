using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WarField
{
    using SD = SoldierDefines;

    public class UiMiniMapProducePanel : MonoBehaviour, UIMouthActivityIntf
    {

#region public parameters

#endregion

#region private parameters

        [SerializeField] private UiMiniMapProdBarrack[] _barracks;
        [SerializeField] private Color[] _barrackColors;

        //panel移入、移出屏幕
        [SerializeField] private UIMouthCheck[] _checkers;
        [SerializeField] private Image _moveBg, _moveDirImg;

        private bool _isInScreen; //panel是否在屏幕范围内

        private Vector3 _oriPos;
        private RectTransform _rect;
        private Sprite _outArrowSprite, _inArrowSprite;

        private UIMiniMap _miniMap; //parent
        private bool _isShowed = false; //这个表示整个minimap有没有显示
        private bool _isMoving = false;
        private bool _beInited = false;

#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        private void Awake()
        {
            _beInited = false;
            _isInScreen = true;
            _moveBg.enabled = false;
            _moveDirImg.enabled = false;
            _outArrowSprite = Resources.Load<Sprite>("Textures/UI/WarField/MiniMap/SpawnPanelOutArrow");
            _inArrowSprite = Resources.Load<Sprite>("Textures/UI/WarField/MiniMap/SpawnPanelInArrow");


            _moveDirImg.sprite = _inArrowSprite;
        }

#endregion

#region public functions

        public bool InitProducePanel(UIMiniMap miniMap)
        {
            if (_beInited)
                return false;

            _rect = GetComponent<RectTransform>();
            _oriPos = _rect.localPosition;
            int cnt = _checkers.Length;
            for (int i = 0; i < cnt; i++)
                _checkers[i].RegisterReceiver(this);
            _miniMap = miniMap;
            for (int i = 0; i < _barracks.Length; i++)
            {
                if (ReferenceEquals(_barracks[i], null) == false)
                    _barracks[i].InitUiMiniMapProdBarrack(_barrackColors[i], this);
            }

            return true;
        }

        public void ShowProducePanel()
        {
            for (int i = 0; i < _barracks.Length; i++)
            {
                if (ReferenceEquals(_barracks[i], null) == false)
                    _barracks[i].ShowProdBarrack();
            }
            _isShowed = true;
        }

        public void HideProducePanel()
        {
            for (int i = 0; i < _barracks.Length; i++)
            {
                if (ReferenceEquals(_barracks[i], null) == false)
                    _barracks[i].HideProdBarrack();
            }
            _isShowed = false;
        }

        public void SpawnSpotInBarrackSelect(SD.TroopType uiBarrack, float trailY)
        {
            _miniMap.ShowSdSpawnTrail(trailY);
        }

        public void SpawnSpotInBarrackUnselect(SD.TroopType troop, int spawnIndex, float trailY, SoldierConf conf)
        {
            _miniMap.HideSDSpawnTrail(troop, spawnIndex, trailY, conf);
        }

        //地图中增加一种新的士兵
        public void AddSDToProduce(SD.TroopType troop, int spawnIndex, SoldierConf conf)
        {
            _barracks[(int)troop].AddSdIntoBarrack(spawnIndex, conf);
        }

        public void RemoveSDFromProduce(SD.TroopType troop, int spawnIndex)
        {
            _barracks[(int)troop].RemoveSdFromBarrack(spawnIndex);
        }

        public bool UpdateSdToProduce(SD.TroopType troop, int spawnIndex, SoldierConf conf)
        {
            return _barracks[(int)troop].UpdateSdInBarrack(spawnIndex, conf);
        }

        public void MouthEnter(string value, PointerEventData eventData)
        {
            if (value == "MovePanel")
            {
                //_moveBg.color = Color.gray;
                _moveBg.enabled = true;
                _moveDirImg.enabled = true;
            }
            else if (value == "MoveBt")
            {
                //_moveBg.color = new Color32(168, 54, 54, 255);
            }
        }

        public void MouthExit(string value, PointerEventData eventData)
        {
            if (value == "MovePanel")
            {
                //_moveBg.color = Color.gray;
                _moveBg.enabled = false;
                _moveDirImg.enabled = false;
            }
        }

        public void MouthClick(string value, PointerEventData eventData)
        {
            if(_isMoving == true)
                return;

            if (value == "MoveBt")
            {
                if (_isInScreen == true)
                {
                    Vector3 targetPos = new Vector3(-_rect.rect.width, _rect.localPosition.y, 0);
                    _isMoving = true;
                    // iTween 动画
                    iTween.MoveTo(gameObject, iTween.Hash(
                        "position", targetPos,
                        "time", 0.2f,
                        "oncomplete", "OnMoveOutScreen",
                        "islocal", true, // 用 localPosition
                        "easetype", iTween.EaseType.easeInOutQuad
                    ));
                }
                else
                {
                    _isMoving = true;
                    // iTween 动画
                    iTween.MoveTo(gameObject, iTween.Hash(
                        "position", _oriPos,
                        "time", 0.2f,
                        "oncomplete", "OnMoveInScreen",
                        "islocal", true, // 用 localPosition
                        "easetype", iTween.EaseType.easeInOutQuad
                    ));
                }
            }
        }

        public void MouthUp(string value, PointerEventData eventData) { }

#endregion

#region private functions

        //itween 回调
        private void OnMoveOutScreen()
        {
            _isInScreen = false;
            _moveDirImg.sprite = _outArrowSprite;
            _isMoving = false;
        }

        private void OnMoveInScreen()
        {
            _isInScreen = true;
            _moveDirImg.sprite = _inArrowSprite;
            _isMoving = false;
        }
#endregion


    }
}

