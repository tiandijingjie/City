using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UIMiniMapDefines;
using UnityEngine.UI;

namespace WarField
{
	using SD = SoldierDefines;
	using UD = UIDefines;
	using WE = WarFieldElements;

    public class UiMiniMapCtrl : MonoBehaviour, IoObserverIntf, ICfgCbIntf
    {
#region public parameters
        public static UiMiniMapCtrl Instance;
#endregion

#region private parameters

        //小地图列表item
        private class MiniMapListItem
        {
            public int p_mapId; //对应的levelmap
            public Camera p_uiCamera; //投影camera
            public UIMiniMapMacro _macroMiniMap;
        }

        [SerializeField] private Transform _macroMapScrollContent;
        [SerializeField] private Transform _miniMapList; //minimap 生成在这个节点下
        [SerializeField] private Text _curMapNameText;
        [SerializeField] private GameObject _scrollViewBt; //显示、隐藏scroll view的button
        [SerializeField] private RectTransform _scrollView;
        [SerializeField] private RectTransform _miniObjects; //所有minimap中建筑和士兵的object的父节点

        private Image _miniMapBgImg;
        private GameObject _miniMapPfb;
	    private Dictionary<int, UIMiniMap> _miniMapDict; // <mapid, UIMiniMap>
        private GameObject _macroMapPfb;
        private Dictionary<int, UIMiniMapMacro> _macroMapDic; //<mapid, UIMiniMapMacro> 记录minimap选择下拉菜单中的每个item, 只包含已经被打开的map

	    private IoObserver _ioObserver;
	    private KeyCode _curKey;
        private Vector2 _initMiniMapAnchoredPos; //隐藏时的位置
        private bool _isActive;
        private int _curMiniMapId;
        private Vector2 _scrollViewShowPos; //minimap macro list 在显示的时候的位置

        private Dictionary<int, SdSpawnInfoInMiniMap>[] _sdSpawnInfos; //<spawnIndex, >[troop type]  记录barrack每个生产spot在各个地图中位置信息

        //gameobject pool
        private GameObject _miniPfbGameObject;
        private DataPool<GameObject> _miniPrefabMemPool ;

        private Sprite _soldierIcon; //小地图中士兵图标
        private Sprite _rangeCoverTex; //用来显示portal/cave 在minimap中的覆盖范围

        private bool _miniMapListShowed = false;
        private bool _beInited = false;
#endregion

#region private parameters' get set

        public Sprite gs_soldierIcon
        {
            get { return _soldierIcon; }
        }

        public Sprite gs_rangeCoverTex
        {
            get { return _rangeCoverTex; }
        }
#endregion

#region Unity callbacks
        private void Awake()
        {
	        if(Instance != null)
	        {
		        Destroy(gameObject);
		        return;
	        }
	        Instance = this;
            _miniMapBgImg = gameObject.GetComponent<Image>();
            _miniMapBgImg.enabled = false;
            _soldierIcon = Resources.Load<Sprite>("Textures/UI/WarField/MiniMap/SoldierIcon");
            _rangeCoverTex = Resources.Load<Sprite>("Textures/UI/WarField/MiniMap/CoverRangeInMiniMap");
	        _beInited = false;
        }

        public void MiniMapListBtEvent()
        {
            if (_miniMapListShowed == false)
                ShowMiniMapMacroList();
            else
                HideMiniMapMacroList();
        }
#endregion

#region public functions
        public bool InitMiniCtrl()
        {
	        if(_beInited)
		        return false;

            _miniPfbGameObject = Resources.Load<GameObject>("Prefabs/UI/WarField/MiniMap/MiniGameElementIcon");
            _miniPrefabMemPool = new DataPool<GameObject>(true);
	        var systemCfgItem = SystemCfgMgr.Instance.GetSysCfgItem(SysCfgDefines.SectionTypes.HOTKEY, "MiniMap"); //get key
	        _curKey = (KeyCode)systemCfgItem.GetCfgValue();
	        systemCfgItem.AddCallback(this);

	        _ioObserver = new IoObserver(UD.UIEventGroupType.MINIMAP);
	        _ioObserver.RegisterListener(this, _curKey, "MiniMap", UD.UiIoEventType.KEYDOWN, UD.IoEvtScheduleType.LIST);
	        _ioObserver.RegisterListener(this, KeyCode.Escape, "ESC", UD.UiIoEventType.KEYDOWN, UD.IoEvtScheduleType.STACK);
	        _initMiniMapAnchoredPos = transform.position;

            _miniMapDict = new Dictionary<int, UIMiniMap>();
            _macroMapDic = new Dictionary<int, UIMiniMapMacro>();

            _miniMapPfb = Resources.Load<GameObject>("Prefabs/UI/WarField/MiniMap/MiniMap");
            _macroMapPfb = Resources.Load<GameObject>("Prefabs/UI/WarField/MiniMap/MinimapMacro");

            Vector2 gap = new Vector2(100, 100);
            //on ground
            UIMiniMap miniMap = Instantiate(_miniMapPfb, new Vector3(Screen.width + gap.x, 0, 0), Quaternion.identity, _miniMapList).GetComponent<UIMiniMap>();
            miniMap.InitMiniMap(WarMapCtrl.Instance.GetMapByIndex(WE.OnGroundMapIndex)); //地面
            _miniMapDict.Add(WE.OnGroundMapIndex, miniMap);
            _curMiniMapId = WE.OnGroundMapIndex; //默认显示地面的小地图
            UIMiniMapMacro macroMap = Instantiate(_macroMapPfb, _macroMapScrollContent).GetComponent<UIMiniMapMacro>();
            macroMap.InitMiniMapMacro(WE.OnGroundMapIndex, true);
            _macroMapDic.Add(WE.OnGroundMapIndex, macroMap);

            //under ground
            var mapDict = WarMapCtrl.Instance.gs_mapDict;
            foreach (var map in mapDict.Values) //地下
            {
                int mapId = map.gs_mapIndex;
                if(mapId == WE.OnGroundMapIndex)
                    continue;

                miniMap = Instantiate(_miniMapPfb, new Vector3(Screen.width + gap.x, (Screen.height + gap.y) * (mapId + 1), 0), Quaternion.identity,
                    _miniMapList).GetComponent<UIMiniMap>();
                miniMap.InitMiniMap(map);
                _miniMapDict.Add(mapId, miniMap);

                if (map.gs_isOpened == true) //应该此时没有opened的
                {
                    macroMap = Instantiate(_macroMapPfb, _macroMapScrollContent).GetComponent<UIMiniMapMacro>();
                    macroMap.InitMiniMapMacro(mapId, false);
                    _macroMapDic.Add(mapId, macroMap);
                }
            }

            _sdSpawnInfos = new Dictionary<int, SdSpawnInfoInMiniMap>[(int)SD.TroopType.MAX];

            //获取起始的生产士兵
            for (int i = 1; i < (int)SD.TroopType.MAX; i++)
            {
                _sdSpawnInfos[i] = new Dictionary<int, SdSpawnInfoInMiniMap>();
                FriendlyBarrack barrack = WarBuildingCtrl.Instance.GetFriendlyBarrackByTroop((SD.TroopType)i);
                var arr = barrack.gs_spawnArr;
                for (int j = 0; j < arr.Length; j++)
                {
                    AddSdToMiniMap((SD.TroopType)i, j, arr[j], WE.OnGroundMapIndex);
                }
            }

            _curMapNameText.text = "Surface"; //默认显示地面
            _curMapNameText.gameObject.SetActive(false);
            _scrollViewBt.SetActive(false);
            _scrollViewShowPos = _scrollView.position;
            _scrollView.position = new Vector2(10000, 10000);

	        _isActive = false;
	        _beInited = true;
	        return true;
        }

        public void OnSysCfgChanged(SystemCfgItem changedItem)
        {
	        _ioObserver.UnregisterListener(this, _curKey, "MiniMap", UD.UiIoEventType.KEYDOWN, UD.IoEvtScheduleType.LIST);
	        _curKey = (KeyCode)changedItem.GetCfgValue();
	        _ioObserver.RegisterListener(this, _curKey, "MiniMap", UD.UiIoEventType.KEYDOWN, UD.IoEvtScheduleType.LIST);
        }

        public void OnIoEvtNotification(string keyAlias, UIDefines.UiIoEventType evtType)
        {
	        switch (keyAlias,evtType)
	        {
		        case { keyAlias: "MiniMap", evtType: UD.UiIoEventType.KEYDOWN }:
		        {
			        if (_isActive == false)
			        {
				        //独占IO输入
				        if (UiIoTask.Instance.OccupyExclusiveOwnerShip(UD.UIEventGroupType.MINIMAP) == false)
				        {
					        GameLogger.LogError($"MiniMap can not occupy the io");
					        return;
				        }
				        transform.position = new Vector2(Screen.width / 2, Screen.height / 2);
				        ShowMiniMap();
				        _isActive = true;
			        }
			        else
			        {
				        transform.position = _initMiniMapAnchoredPos;
				        HideMiniMap();
				        //解除占用
				        UiIoTask.Instance.ReleaseExclusiveOwnerShip(UD.UIEventGroupType.MINIMAP);
				        _isActive = false;
			        }
			        break;
		        }
		        case { keyAlias: "ESC", evtType: UD.UiIoEventType.KEYDOWN }:
			        if (_isActive == true)
			        {
				        transform.position = _initMiniMapAnchoredPos;
				        HideMiniMap();
				        //解除占用
				        UiIoTask.Instance.ReleaseExclusiveOwnerShip(UD.UIEventGroupType.MINIMAP);
				        _isActive = false;
			        }
			        break;
		        default:
			        GameLogger.LogWarning($"Receive unknown key {keyAlias} {evtType}");
			        break;
	        }
        }

        public void ShowMiniMap()
        {
            _miniMapDict[_curMiniMapId].ShowMiniMap();

            _curMapNameText.gameObject.SetActive(true);
            _scrollViewBt.SetActive(true);

            //update minimap list items
            var mapDict = WarMapCtrl.Instance.gs_mapDict;
            foreach (var map in mapDict.Values)
            {
                int mapId = map.gs_mapIndex;
                if(_macroMapDic.ContainsKey(mapId) == true)
                    continue;

                if (map.gs_isOpened == true)  //更新macro list
                {
                    UIMiniMapMacro macroMap = Instantiate(_macroMapPfb, _macroMapScrollContent).GetComponent<UIMiniMapMacro>();
                    macroMap.InitMiniMapMacro(mapId, false);
                    _macroMapDic.Add(mapId, macroMap);
                }
            }
            _miniMapBgImg.enabled = true;
        }

        public void HideMiniMap()
        {
            _curMapNameText.gameObject.SetActive(false);
            _scrollViewBt.SetActive(false);
            _miniMapDict[_curMiniMapId].HideMiniMap();
            _miniMapBgImg.enabled = false;
        }

        //增加一种士兵到minimap produce panel
        public bool AddSdToMiniMap(SD.TroopType troopType, int spawnIndex, SoldierConf conf, int mapId)
        {
            var dic = _sdSpawnInfos[(int)troopType];
            if (mapId == WE.OnGroundMapIndex) //onground map, 其实就是barrack新增一个spawn spot
            {
                if (dic.ContainsKey(spawnIndex) == true)
                {
                    GameLogger.LogError($"map {mapId} already has this soldier {troopType} {spawnIndex}");
                    return false;
                }

                SdSpawnInfoInMiniMap info = new SdSpawnInfoInMiniMap(troopType, spawnIndex, conf);
                dic.Add(spawnIndex, info);
                //先处理ongroud地图，这里会处理portal，cave
                info.p_trailInMaps.Add(WE.OnGroundMapIndex, _miniMapDict[WE.OnGroundMapIndex].AddSDEnter(troopType, spawnIndex, conf));
            }
            else //underground map, 必须已经在onground map中添加过
            {
                SdSpawnInfoInMiniMap info = dic[spawnIndex];
                if (info == null)
                {
                    GameLogger.LogError($"this soldier {troopType} {spawnIndex} not added yet");
                    return false;
                }
                info.p_trailInMaps.Add(mapId, _miniMapDict[mapId].AddSDEnter(troopType, spawnIndex, conf));
            }
            return true;
        }

        //从minimap  produce panel删除一种士兵, 是在计算spawn spot的时候调用的
        public bool RmSdFromMiniMap(SD.TroopType troopType, int spawnIndex, SoldierConf conf, int mapId)
        {
            if (mapId == WE.OnGroundMapIndex)
            {
                GameLogger.LogError("Can not remove soldier from onground map");
                return false;
            }

            if(_miniMapDict[mapId].RemoveSD(troopType, spawnIndex) == true)
                _sdSpawnInfos[(int)troopType][spawnIndex].p_trailInMaps.Remove(mapId);
            return true;
        }

        //spawn spot发生变化：士兵类型变了
        public void UpdateSpawnSpot(SD.TroopType troopType, int spawnIndex, SoldierConf conf)
        {
            var dic = _sdSpawnInfos[(int)troopType];
            if (dic.ContainsKey(spawnIndex) == false)
            {
                GameLogger.LogError($"this spawn spot {troopType} {spawnIndex} not added yet");
                return;
            }

            var maps = _sdSpawnInfos[(int)troopType][spawnIndex].p_trailInMaps;
            foreach (var mapId in maps)
            {
                if (_miniMapDict[mapId.Key].UpdateSD(troopType, spawnIndex, conf) == false)
                {
                    GameLogger.LogError($"Update soldier {troopType} {conf.p_name} of spot {spawnIndex} in map {mapId} failed");
                }
            }
            return;
        }

        public SdTrailInfoInSingleMap GetSDInfoInSingleMap(SD.TroopType troopType, int spawnIndex, int mapId)
        {
            return _sdSpawnInfos[(int)troopType][spawnIndex].p_trailInMaps[mapId];
        }

        //minimap 下拉菜单中一个item被选中
        public void MacroMiniMapChoosed(int mapId)
        {
            if(mapId == _curMiniMapId)
                return;
            _miniMapDict[_curMiniMapId].HideMiniMap();
            _macroMapDic[_curMiniMapId].BeSelected(false);
            _curMiniMapId = mapId;
            _miniMapDict[_curMiniMapId].ShowMiniMap();
            _macroMapDic[_curMiniMapId].BeSelected(true);
            _scrollView.position = new Vector2(10000, 10000);
            _curMapNameText.text = WarMapCtrl.Instance.GetMapByIndex(_curMiniMapId).gs_mapName;
            _miniMapListShowed = false;
        }

        public GameObject GetMiniPrefabObjFromMemPool()
        {
            if (_miniPrefabMemPool.Count == 0)
                return SpawnGameElementPrefabObj();

            return _miniPrefabMemPool.PopOut();
        }

        public void ReleasePrefabObjToMemPool(GameObject obj)
        {
            _miniPrefabMemPool.AddItem(obj);
        }

        public UIMiniMap GetMinimapByMapId(int mapId)
        {
            return _miniMapDict?[mapId];
        }
#endregion

#region private functions

        //显示minimap macro的列表
        private void ShowMiniMapMacroList()
        {
            foreach (var macroMap in _macroMapDic)
            {
                macroMap.Value.ShowMacroMap();
            }

            _scrollView.position = _scrollViewShowPos;
            _miniMapListShowed = true;
        }

        private void HideMiniMapMacroList()
        {
            _scrollView.position = new Vector2(10000, 10000);
            _miniMapListShowed = false;
        }

        private GameObject SpawnGameElementPrefabObj()
        {
            var prefabInstance = Instantiate(_miniPfbGameObject, _miniObjects);
            if (prefabInstance == null)
            {
                GameLogger.LogError($"GameElementPrefabOb fail to be instantiated");
            }
            else
            {
                prefabInstance.SetActive(false);
                prefabInstance.transform.position = new Vector3(-10000, -10000, 0);
            }
            return prefabInstance;
        }
#endregion
    }
}

