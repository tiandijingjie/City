using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UIMiniMapDefines;

namespace WarField
{
    using SD = SoldierDefines;
    using WE = WarFieldElements;

    //一个地图对应的minimap
    //包括两个部分，_producePanel对应与出兵路线
    //            _warFieldPanel对于与小地图上的士兵，建筑
    public class UIMiniMap : MonoBehaviour
    {
#region public parameters

#endregion

#region private parameters
        [SerializeField] private UiMiniMapProducePanel _producePanel;
        [SerializeField] private UiMiniWarFieldPanel _warFieldPanel;

        private LevelMap _realMap;
        private Vector2 _hidePos; //不显示时位置
        private RectTransform _rectTransform;

        private bool _isShowed;
        private bool _beInited;
#endregion

#region private parameters' get set

        public UiMiniMapProducePanel gs_producePanel
        {
            get { return _producePanel; }
        }

        public UiMiniWarFieldPanel gs_warFieldPanel
        {
            get { return _warFieldPanel; }
        }
#endregion

#region Unity callbacks

#endregion

#region public functions

        public bool InitMiniMap(LevelMap levelMap)
        {
            _rectTransform = GetComponent<RectTransform>();
            _hidePos = _rectTransform.position;
            _warFieldPanel.InitWarFieldPanel(levelMap, _hidePos); //必须先init map
            _producePanel.InitProducePanel(this);
            _beInited = true;
            _isShowed = false;
            gameObject.name = "Minimap_" + levelMap.gs_mapIndex;
            _realMap = levelMap;
            return true;
        }

        public void ShowMiniMap()
        {
            _rectTransform.position = Vector3.zero;
            _producePanel.ShowProducePanel();
            _warFieldPanel.ShowMiniWarField();
            _isShowed = true;
        }

        public void HideMiniMap()
        {
            _rectTransform.position = _hidePos;
            _producePanel.HideProducePanel();
            _warFieldPanel.HideMiniWarField();
            _isShowed = false;
        }

        //index是trial的id，是一个troop type和spawn index的组合
        public void ShowSdSpawnTrail(float yPoint)
        {
            _warFieldPanel.ShowTrailAt(yPoint);
        }

        public void HideSDSpawnTrail(SD.TroopType troop, int spawnIndex, float trailY, SoldierConf conf)
        {
            _warFieldPanel.HideSdSpawnTrail();
            //释放鼠标的时候重新计算trail
            SdTrailInfoInSingleMap singleMap = UiMiniMapCtrl.Instance.GetSDInfoInSingleMap(troop, spawnIndex, _realMap.gs_mapIndex);
            _warFieldPanel.CalculateTrail(singleMap, trailY, troop, spawnIndex, conf);
        }

        //增加一种新的进入地图的士兵
        //如果是onground地图需要生成计算portal和cave
        public SdTrailInfoInSingleMap AddSDEnter(SD.TroopType troop, int spawnIndex, SoldierConf conf)
        {
            SdTrailInfoInSingleMap singleMap = new SdTrailInfoInSingleMap(_realMap, this);
            _producePanel.AddSDToProduce(troop, spawnIndex, conf);

            //因为地下map不会经过portal和cave，所以不需要计算
            if(_realMap.gs_mapType == WE.MapType.UNDERGROUND)
                _realMap.AddEnterSd(troop, spawnIndex);
            else //onground minimap
            {
                singleMap.p_passedCaves = new List<CaveTran>();
                singleMap.p_passedPortals = new List<UIPortalTransportation>();
                _warFieldPanel.CalculateTrail(singleMap, _rectTransform.rect.height / 2.0f, troop, spawnIndex, conf); //默认位置就是屏幕y轴中心点
            }

            return singleMap;
        }

        //onground map不能调用这个api
        public bool RemoveSD(SD.TroopType troop, int spawnIndex)
        {
            if (_realMap.gs_mapType == WE.MapType.ONGROUND)
            {
                GameLogger.LogError($"onground map can not remove soldier");
                return false;
            }

            _producePanel.RemoveSDFromProduce(troop, spawnIndex);
            _realMap.RmEnterSd(troop, spawnIndex);
            return true;
        }

        //spawn spot发生变化：士兵类型变了
        public bool UpdateSD(SD.TroopType troop, int spawnIndex, SoldierConf conf)
        {
            return _producePanel.UpdateSdToProduce(troop, spawnIndex, conf);
        }
#endregion

#region private functions

#endregion
    }
}

