using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WarField
{
    using SD = SoldierDefines;

    public class UiMiniMapProdBarrack : MonoBehaviour
    {
#region public parameters

#endregion

#region private parameters
        private struct SpawnSpotInfo
        {
            public SoldierConf p_conf;
            public UiMiniMapProdSpawnSpot p_prodItem; //每一个出兵位置
        }

        [SerializeField] private SD.TroopType _troopType;
        [SerializeField] private Color _mainColor;

        private GameObject _prodSpawnSpotPfb;
        private FriendlyBarrack _barrackRef;
        private Dictionary<int, SpawnSpotInfo> _spawnSpotInfos; //[spawn spot index in barrack, SpawnSpotInfo]

        private UiMiniMapProducePanel _parentPanel; //父节点
        private bool _isActive;
        private bool _beInited;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        private void Awake()
        {
            _prodSpawnSpotPfb = Resources.Load<GameObject>("Prefabs/UI/WarField/MiniMap/MiniMapProdSpawnSpot");
            _spawnSpotInfos = new Dictionary<int, SpawnSpotInfo>();
            _beInited = false;
        }

#endregion

#region public functions

        public bool InitUiMiniMapProdBarrack(Color color, UiMiniMapProducePanel parent)
        {
            if (_beInited == true)
            {
                GameLogger.LogError($"UiMiniMapProdBarrack {_troopType} has already been inited");
                return false;
            }

            gameObject.name = _troopType.ToString();
            _mainColor = color;
            _parentPanel = parent;
            //transform.Find("HighLight").GetComponent<Image>().color = _mainColor;
            _barrackRef = WarBuildingCtrl.Instance.GetFriendlyBarrackByTroop(_troopType);

            _isActive = false;
            _beInited = true;
            return true;
        }

        public void ShowProdBarrack()
        {

        }

        public void HideProdBarrack()
        {
            //恢复层级机构
            for (int i = 0; i < _spawnSpotInfos.Count; i++)
            {
                _spawnSpotInfos[i].p_prodItem.transform.SetSiblingIndex(i);
            }
        }

        //增加一种soldier
        public void AddSdIntoBarrack(int spawnIndex, SoldierConf conf)
        {
            if (_beInited == false)
            {
                GameLogger.LogError($"Minimap prod barrack {_troopType} has not been inited");
                return;
            }

            if (_spawnSpotInfos.ContainsKey(spawnIndex) == false)
            {
                SpawnSpotInfo tmp = new SpawnSpotInfo();
                tmp.p_conf = conf;
                var gameObj = Instantiate(_prodSpawnSpotPfb, transform);
                gameObj.GetComponent<RectTransform>().localPosition = new Vector3(0, GetComponent<RectTransform>().rect.height / 2, 0); //默认y轴上位于中点
                tmp.p_prodItem = gameObj.GetComponent<UiMiniMapProdSpawnSpot>();
                tmp.p_prodItem.InitProdSpawnSpot(spawnIndex, this, _barrackRef, tmp.p_conf);
                _spawnSpotInfos.Add(spawnIndex, tmp);
            }
            else
            {
                GameLogger.LogError("already has this spawn spot soldier");
            }
        }

        public void RemoveSdFromBarrack(int spawnIndex)
        {
            if (_spawnSpotInfos.ContainsKey(spawnIndex) == true)
            {
                Destroy(_spawnSpotInfos[spawnIndex].p_prodItem.gameObject);
                _spawnSpotInfos.Remove(spawnIndex);
            }
            else
            {
                GameLogger.LogError("not has this spawn spot soldier");
            }
        }

        public bool UpdateSdInBarrack(int spawnIndex, SoldierConf conf)
        {
            if (_beInited == false)
            {
                GameLogger.LogError($"Minimap prod barrack {_troopType} has not been inited");
                return false;
            }

            if (_spawnSpotInfos.ContainsKey(spawnIndex) == false)
            {
                GameLogger.LogError($"Can not update of not exist spot {spawnIndex}");
                return false;
            }
            var tmp = _spawnSpotInfos[spawnIndex];
            tmp.p_conf = conf;
            return tmp.p_prodItem.UpdateSpawnSpot(tmp.p_conf);
        }

        public void SpawnSpotSelected(int index, float trailY)
        {
            _parentPanel.SpawnSpotInBarrackSelect(_troopType, trailY);
        }

        public void SpawnSpotUnselected(int index, float yPosition, SoldierConf conf)
        {
            _parentPanel.SpawnSpotInBarrackUnselect(_troopType, index, yPosition, conf);
        }
#endregion

#region private functions

#endregion
    }
}
