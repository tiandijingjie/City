using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UIMiniMapDefines;

namespace WarField
{
    using GD = GlobalDefines;
    using SD = SoldierDefines;

    public class UiMiniWarFieldPanel : MonoBehaviour
    {
#region public parameters

#endregion

#region private parameters
        [SerializeField] private UiMiniWarFieldMap _warFieldMiniMap;
        private bool _beInited = false;
#endregion

#region private parameters' get set

        public UiMiniWarFieldMap gs_warFieldMiniMap
        {
            get { return _warFieldMiniMap; }
        }

#endregion

#region Unity callbacks
        private void Awake()
        {
            _beInited = false;
        }
#endregion

#region public functions
        public bool InitWarFieldPanel(LevelMap levelMap, Vector2 hideOffset)
        {
            _warFieldMiniMap.InitWarFieldMap(levelMap, hideOffset);
            _beInited = true;
            return true;
        }

        public void ShowMiniWarField()
        {
            _warFieldMiniMap.ShowWarFieldMiniMap();
        }

        //offset:隐藏时位置
        public void HideMiniWarField()
        {
            HideSdSpawnTrail();
            _warFieldMiniMap.HideWarFieldMiniMap();
        }

        public void ShowTrailAt(float yPoint)
        {
            _warFieldMiniMap.ShowTrailAt(yPoint);
        }

        public void HideSdSpawnTrail()
        {
            _warFieldMiniMap.HideTrails(true);
        }

        //计算trail经过的portal和cave
        public void CalculateTrail(SdTrailInfoInSingleMap trailInfo, float yPoint, SD.TroopType troop, int spawnIndex, SoldierConf conf)
        {
            _warFieldMiniMap.CalculateTrail(trailInfo, yPoint, troop, spawnIndex, conf);
        }
#endregion

#region private functions

#endregion
    }
}

