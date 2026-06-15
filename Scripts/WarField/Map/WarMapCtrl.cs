using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Unity.Jobs;
using UnityEngine;

namespace WarField
{
    using WE = WarFieldElements;
    using WBD = WarBuildingDefines;

    public class WarMapCtrl : MonoBehaviour
    {
#region public parameters

        static public WarMapCtrl Instance;

#endregion

#region private parameters

        [SerializeField] private float _mapSpace = -20; //上面地图下边缘与下面地图上边缘的距离

        private Dictionary<int, LevelMap> _mapDict;

        private Dictionary<int, PathFinderMap> _pathFinderMapDict;
        private Vector2 _nextLevelMapGap = Vector2.zero; //地图左上角的位置
        private int _mapCnt = 0;

        private byte _curMapId; //当前观看的map
        private bool _beInited;
#endregion

#region private parameters' get set

        public Dictionary<int, LevelMap> gs_mapDict
        {
            get { return _mapDict; }
        }

        public byte gs_curMapId
        {
            get { return _curMapId; }
        }

#endregion

#region Unity callbacks

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(Instance);
                return;
            }

            Instance = this;
            _mapDict = new Dictionary<int, LevelMap>();
            _pathFinderMapDict = new Dictionary<int, PathFinderMap>();
            _beInited = false;
        }

        private void OnDestroy()
        {
            ResourceGrid.Instance.Dispose();
        }

#endregion

#region public functions

        public bool InitMapCtrl(int level, WE.Difficulty difficulty)
        {
            if (_beInited == true)
                return false;
            if (LoadLevelConf(level) == false)
                return false;
            _curMapId = WE.OnGroundMapIndex;

            //在map中加入grid
            SpatialGridManager.Instance.InitSpatialGridManager();
            foreach (LevelMap map in _mapDict.Values)
                SpatialGridManager.Instance.AddGrid(map.gs_mapIndex, map.gs_passablePart, 3); //cell size == 3
            new ResourceGrid();
            ResourceGrid.Instance.InitResGrid(GetMapByIndex(WE.OnGroundMapIndex).gs_passablePart); //只在地面地图生成资源grid

            foreach (var tmp in _pathFinderMapDict)
            {
                int mapId = tmp.Key;
                PathFinderMap ffMap = tmp.Value;
                ffMap.InitPathFinderMap(mapId, _mapDict[mapId].gs_passablePart);
            }

            WarBuildingCtrl.Instance.ForceBuildingsPoolFlush(); //强制从level中添加的建筑异步添加到pool中

            var initialBuildings = FindObjectsOfType<WarBuilding>();
            foreach (var bd in initialBuildings)
            {
                if (_pathFinderMapDict.TryGetValue(bd.gs_mapId, out PathFinderMap ffMap))
                {
                    // 顺手将初始建筑的数据塞入动态待添加队列
                    // 这样它们在游戏第一帧的 LateUpdate 刷新时，就会被完美识别为动态障碍！
                    ffMap.AddEntity(bd.gs_entityData);
                }
            }

            _beInited = true;
            return true;
        }

        public LevelMap GetMapByIndex(int index)
        {
            return _mapDict[index];
        }

        public PathFinderMap GetPathFinderMapByIndex(int mapId)
        {
            if (_pathFinderMapDict.TryGetValue(mapId, out PathFinderMap map))
                return map;
            return null;
        }

        //在lastupdte里面调用
        public JobHandle FlushFlowFieldMaps()
        {
            JobHandle jobHandle = default;
            foreach (var map in _pathFinderMapDict.Values)
            {
                var job = map.FlushFlowFieldMap();
                if(job != default)
                    jobHandle = JobHandle.CombineDependencies(jobHandle, job);
            }
            return jobHandle;
        }
#endregion

#region private functions

        private bool LoadLevelConf(int level)
        {
            XmlDocument xmlDocument = Utils.LoadXmlFile("Conf/Level/Level_" + level);
            if (xmlDocument == null)
                return false;

            //load map node firstly
            XmlNode levelMapNode = xmlDocument.SelectSingleNode("levelConfs/levelMap");
            if (levelMapNode == null || levelMapNode.HasChildNodes == false)
                GameLogger.LogError($"Failed to load level conf, it not has map node");

            foreach (XmlNode mapNode in levelMapNode.ChildNodes)
            {
                if (mapNode.NodeType == XmlNodeType.Comment)
                    continue;

                if (AnalyseMap(mapNode) == false)
                {
                    GameLogger.LogError($"Failed to load level conf, load map failed");
                    return false;
                }

                _nextLevelMapGap.y += _mapSpace;
            }

            return CheckMapsValidation();
        }

        private bool CheckMapsValidation()
        {
            if (_mapDict.ContainsKey(WE.OnGroundMapIndex) == false)
            {
                GameLogger.LogError("Not load the onground map !!!");
                return false;
            }

            //检查每个underground map是否有cave对应
            int cnt = _mapDict.Count - 1; //减去地面
            int caveCnt = 0;
            //获取到地面的cave的list
            if (WarBuildingCtrl.Instance.GetBuildingOnField(WE.RaceType.Neutral, WBD.BuildingMode.CAVE, WE.OnGroundMapIndex, out var caveList) == true)
                caveCnt = caveList.Count;

            if (cnt != caveCnt)
            {
                GameLogger.LogError($"The number of cave is not the same as underground map, map cnt:{cnt}  cave cnt:{caveList.Count} !!!");
                return false;
            }

            //这样去遍历性能差，但是因为是初始化阶段所以就不改了
            for (int i = 0; i < cnt; i++)
            {
                bool hasMatch = false;
                for (int j = 0; j < caveList.Count; j++)
                {
                    CaveTran cave = (CaveTran)caveList.GetByIndex(j);
                    if (cave.gs_mapId != WE.OnGroundMapIndex)
                    {
                        GameLogger.LogError($"Cave not in the surface map, but in the {cave.gs_mapId} map");
                        return false;
                    }

                    if (((CaveTran)caveList.GetByIndex(i)).gs_insideMapId == i)
                    {
                        hasMatch = true;
                        if(_mapDict[i].SetStepoutCave(cave) == false)
                            return false;
                        break;
                    }
                }

                if (hasMatch == false)
                {
                    GameLogger.LogError($"Underground map {i} not find the matches cave");
                }
            }

            //检查portal
            int portalCnt = 0;
            if (WarBuildingCtrl.Instance.GetBuildingOnField(WE.RaceType.Neutral, WBD.BuildingMode.PORTAL, WE.OnGroundMapIndex, out var portalList) == true)
                portalCnt = portalList.Count;
            cnt = portalCnt;
            if (cnt % 3 != 0)
            {
                GameLogger.LogError($"Portal number is not correct {cnt} !!!");
                return false;
            }

            //这样去遍历性能差，但是因为是初始化阶段所以就不改了
            for (int i = 0; i < cnt; i++)
            {
                Portal portal = (Portal)portalList.GetByIndex(i);
                if (portal.gs_mapId != WE.OnGroundMapIndex)
                {
                    GameLogger.LogError($"Portal not in the surface map, but in the {portal.gs_mapId} map");
                    return false;
                }
            }

            return true;
        }

        private bool AnalyseMap(XmlNode parentNode)
        {
            string mapPath = ((XmlElement)parentNode).GetAttribute("value");
            string mapType = ((XmlElement)parentNode).GetAttribute("type");

            if (parentNode.HasChildNodes == true)
            {
                return LoadMap(parentNode, mapPath, mapType);
            }

            GameLogger.LogError($"{mapPath} not set map texture");
            return false;
        }

        private bool LoadMap(XmlNode mapNode, string path, string mapType)
        {
            switch (mapType)
            {
                case "OnGround":
                {
                    GameObject mapPb = Resources.Load<GameObject>("Prefabs/LevelPf/" + path);
                    var map = Instantiate(mapPb, transform).GetComponent<LevelMap>();
                    _mapDict.Add(WE.OnGroundMapIndex, map);
                    //添加流场地图
                    PathFinderMap ffMap = map.GetComponent<PathFinderMap>();
                    if (ffMap == null)
                    {
                        GameLogger.LogError($"Can not find flowfield map in map {WE.OnGroundMapIndex}");
                        return false;
                    }
                    _pathFinderMapDict.Add(WE.OnGroundMapIndex, ffMap);
                    //onground map index 是固定的 WE.OnGroundMapIndex
                    if (map.InitLevelMap(mapNode, WE.OnGroundMapIndex, _nextLevelMapGap, out _nextLevelMapGap) == false)
                    {
                        GameLogger.LogError($"Fail to load onground map {path}");
                        return false;
                    }
                    else
                    {
                        _mapCnt++;
                        return true;
                    }
                }
                case "UnderGround":
                {
                    GameObject mapPb = Resources.Load<GameObject>("Prefabs/LevelPf/" + path);
                    var map = Instantiate(mapPb, transform).GetComponent<LevelMap>();
                    if (map != null)
                    {
                        //添加流场地图
                        PathFinderMap ffMap = map.GetComponent<PathFinderMap>();
                        if (ffMap == null)
                        {
                            GameLogger.LogError($"Can not find flowfield map in map { _mapCnt}");
                            return false;
                        }
                        _pathFinderMapDict.Add(_mapCnt, ffMap);

                        if (!map.InitLevelMap(mapNode, (byte)_mapCnt, _nextLevelMapGap, out _nextLevelMapGap))
                        {
                            GameLogger.LogError($"Fail to load underground map {path}");
                            return false;
                        }
                        _mapCnt++;
                        return true;
                    }
                    GameLogger.LogError($"Fail to load underground map {path}");
                    return false;
                }
                default:
                    break;
            }
            return false;
        }
#endregion
    }

}
