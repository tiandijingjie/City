using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    public class UIDefines
    {
        public enum UiIoEventType
        {
            MIN = 0,
            KEYDOWN,
            KEYUP,
            MAX,
        }

        public enum IoEvtScheduleType
        {
            MIN = 0,
            LIST,  //队列的所有元素都可以收到通知
            STACK, //只有队列的最后可以收到通知
            FIFO,  //只有队列的第一个可以收到通知
            MAX
        }

        public enum UIEventGroupType
        {
            MIN = 0,
            MAINMENU,
            MINIMAP,
            SELECTIONMANAGER,
            CONSTRUCT,
            DRAWCARD,
            DRAWBOX,
            BUIDINGUPGRADE, //建筑升级
            SKILLINDICATOR, //技能释放位置或者方向的指示
            CAMERA,
            MAX,
        }

        public enum UiEvent
        {
            MIN = 0,
            SHOWDRAWCARD,  //show draw card view
            SHOWSTATUS, //show status view
            MAX,
        }

        public static float TipShowLatency = 1.2f; //显示tip的延迟
    }

    //通知是及时通知的，但是按键监控的增删实际是在轮询的过程中完成的，避免UiIoTask在列表遍历的时候修改列表
    public interface IoObserverIntf
    {
        public void OnIoEvtNotification(string keyAlias, UIDefines.UiIoEventType evtType);
    }

    public interface IoGroupChangeIntf
    {
        public void OnGroupStatusChange(bool isEnable);
    }

    public class IoObserver
    {
        private UIDefines.UIEventGroupType _groupType;

        public IoObserver(UIDefines.UIEventGroupType groupType)
        {
            _groupType = groupType;
        }

        public void RegisterListener(IoObserverIntf intf, KeyCode key, string keyAlias, UIDefines.UiIoEventType evtType,
            UIDefines.IoEvtScheduleType scheType)
        {
            UiIoTask.Instance.AddIoEvtTask(_groupType, intf, key, keyAlias, evtType, scheType);
        }

        public void UnregisterListener(IoObserverIntf intf, KeyCode key, string keyAlias, UIDefines.UiIoEventType evtType,
            UIDefines.IoEvtScheduleType scheType)
        {
            UiIoTask.Instance.RemoveIoEvtTask(_groupType, intf, key, keyAlias, evtType, scheType);
        }

        //必须在UiIoTask创建之后调用
        public bool RegisterGroupListener(IoGroupChangeIntf groupChangeListener)
        {
            return UiIoTask.Instance.RegisterGroupStatusListener(_groupType, groupChangeListener);
        }
    }
}

namespace UIMiniMapDefines
{
    using WarField;

    //记录一个生产位置在一个minimap中的行进的轨迹
    public class SdTrailInfoInSingleMap
    {
        public LevelMap p_realMap;
        public UIMiniMap p_miniMap;
        public List<UIPortalTransportation> p_passedPortals; //经过的portal
        public List<CaveTran> p_passedCaves; //经过的cave

        public SdTrailInfoInSingleMap(LevelMap realMap, UIMiniMap miniMap)
        {
            p_realMap = realMap;
            p_miniMap = miniMap;

            //只有在onground地图才会赋值
            p_passedPortals = null;
            p_passedCaves = null;
        }
    }

    //记录每一个生产位置在所有minimap中的信息
    public class SdSpawnInfoInMiniMap
    {
        public SoldierDefines.TroopType p_troopType;
        public int p_spawnIndex; //对应于barrack的spawnarr
        public SoldierConf p_sdConf;
        public Dictionary<int,SdTrailInfoInSingleMap> p_trailInMaps; //<mapID, >

        public SdSpawnInfoInMiniMap(SoldierDefines.TroopType troopType, int spawnIndex, SoldierConf sdConf)
        {
            p_troopType = troopType;
            p_spawnIndex = spawnIndex;
            p_sdConf = sdConf;
            p_trailInMaps = new Dictionary<int,SdTrailInfoInSingleMap>();
        }
    }
}

