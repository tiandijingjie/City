using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WarField
{
    using WE = WarFieldElements;

    //minimap list 下拉菜单的item
    public class UIMiniMapMacro : MonoBehaviour, UIMouthActivityIntf
    {
#region public parameters

#endregion

#region private parameters

        [SerializeField] private RawImage _macroMapImg;
        [SerializeField] private Text _macroMapName;

        private LevelMap _levelMap;
        private Camera _mapCamera; //拍摄地图的camera
        private RenderTexture _renderTexture; //camera投影纹理
        private Image _bgImg; //背景image，与levelmap是否占领相关
        private bool _beSelected; //是否被选中
        private bool _isOccupuy;

#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

#endregion

#region public functions

        public bool InitMiniMapMacro(int mapId, bool beSelected)
        {
            _levelMap = WarMapCtrl.Instance.GetMapByIndex(mapId);
            _beSelected = beSelected;

            if (_levelMap.gs_isOpened == false)
            {
                GameLogger.LogError($"Can create minimap macro for map not opened ");
                return false;
            }

            _macroMapName.text = _levelMap.gs_mapName;

            var rect = GetComponent<RectTransform>();
            _renderTexture = new RenderTexture((int)rect.sizeDelta.x, (int)rect.sizeDelta.y, 24);
            _renderTexture.name = "MiniMapMacro_" + mapId;

            _mapCamera = CameraCtrl.Instance.CreateCamera("MapCamera_" + mapId, _levelMap.gs_leftMidPos, false, _levelMap.transform, _renderTexture);
            _macroMapImg.texture = _renderTexture;

            GetComponent<UIMouthCheck>().RegisterReceiver(this);

            SetBg();
            return true;
        }

        public void ShowMacroMap()
        {
            _mapCamera.Render();
            SetBg();
        }

        public void BeSelected(bool beSelected)
        {
            _beSelected = beSelected;
        }

        public void MouthEnter(string value, PointerEventData eventData) { }

        public void MouthExit(string value, PointerEventData eventData) { }

        public void MouthClick(string value, PointerEventData eventData)
        {
            if(_beSelected == false)
                UiMiniMapCtrl.Instance.MacroMiniMapChoosed(_levelMap.gs_mapIndex);
        }

        public void MouthUp(string value, PointerEventData eventData) { }
#endregion

#region private functions

        private void SetBg()
        {
            if(_isOccupuy == true) //已经占领之后不用改背景颜色了
                return;

            if (_isOccupuy != _levelMap.gs_isOccupy)
            {
                _isOccupuy = !_isOccupuy;
                if (_isOccupuy == false)
                {
                    _bgImg.color = Color.yellow;
                    _macroMapName.color = Color.yellow;
                }
                else
                {
                    _bgImg.color = Color.green;
                    _macroMapName.color = Color.green;
                }
            }
        }

#endregion
    }
}
