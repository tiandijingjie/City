using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace WarField
{
    using GD = GlobalDefines;
    using WE = WarFieldElements;
    using WBD = WarBuildingDefines;
    using SD = SoldierDefines;

    public class LevelMap : MonoBehaviour
    {
#region public parameters

#endregion

#region private parameters
        //记录spawn spot的进入位置
        private class CaveEnterPos
        {
            // true：表示需要在小地图上显示，false：表示当前已经不进入了，但是之前设置过位置。
            //小地图中出兵路线不进入cave，但是有可能有些中间的士兵还在向cave运动，这些士兵仍然需要进入时获取位置
            public bool p_enabled; //这个变量并不影响minimap的显示，只是做一个记录
            public Vector2 p_enterPos;
        }

        [SerializeField] private Material _groundMat;
        [SerializeField] private Texture2D _roadTex, _roadDecayTex;

        private Vector2 _leftDownPos; //地图左下角世界坐标
        private Vector2 _leftMidPos; //左边中心点世界坐标
        private GameObject _road,  _decoration;
        private Bounds _passablePart; //整个地图可通行部分的包围盒
        private WE.MapType _mapType;
        private byte _mapId; //the index of the map
        private string _mapName;
        private Dictionary<int, CaveEnterPos>[] _enterSdPos; //underground map进入的己方士兵的起始世界坐标位置,[troop][spawnindex]

        private EdgeCollider2D _left, _right; //左右边界
        private bool _isOpened; //地图是否开启， onground默认开启， underground需要通过开启cave来开启，一旦开启之后就地图无法关闭,但是完全占领之后cave可以关闭不让士兵进来
        private CaveTran _connectCave = null; //underground map所关联的cave， surface map is null

        private bool _isOccupy = false; //地图是否被占领，如果onground map， occupy代表游戏结束
#endregion

#region private parameters' get set

        public Bounds gs_passablePart
        {
            get { return _passablePart; }
        }

        public WE.MapType gs_mapType
        {
            get { return _mapType; }
        }

        public byte gs_mapIndex
        {
            get { return _mapId; }
        }

        public bool gs_isOpened
        {
            get { return _isOpened; }
        }

        public bool gs_isOccupy
        {
            get { return _isOccupy; }
        }

        public string gs_mapName
        {
            get { return _mapName; }
        }

        public Vector2 gs_leftMidPos
        {
            get { return _leftMidPos; }
        }
#endregion

#region Unity callbacks

        private void Awake()
        {
            _road = transform.Find("Road").gameObject;
            _decoration = transform.Find("Decoration").gameObject;
            gameObject.layer = LayerMask.NameToLayer("MapEdge"); //设置碰撞layer
            if (_road == null || _decoration == null)
            {
                GameLogger.LogError($"No road {_road} or decoration {_decoration}");
            }

            Addressables.LoadAssetAsync<Material>("RenderGroundMat").Completed += handle =>
            {
                _groundMat = handle.Result;
            };
            //_groundMat = Resources.Load<Material>("Materials/Map/RenderGroundMat");
            _enterSdPos = new Dictionary<int, CaveEnterPos>[(int)SD.TroopType.MAX];
            for (int i = 1; i < _enterSdPos.Length; ++i)
                _enterSdPos[i] = new Dictionary<int, CaveEnterPos>();

            Rigidbody2D rb = gameObject.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
            }

            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.simulated = true;
            rb.useAutoMass = false;
            rb.gravityScale = 0f;
        }

        //应该只处理underground map的碰撞
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (_mapId == WE.OnGroundMapIndex)
            {
                GameLogger.LogError("OnGroundMapIndex Collision should not be triggered");
                return;
            }

            EdgeCollider2D edge = collision.otherCollider as EdgeCollider2D;

            if (edge == null)
                return;

            if (edge == _left)
            {
                string tag = collision.gameObject.tag;
                WE.WarEleType colType = WarFieldUtil.GetWarEleType(tag);
                if (colType == WE.WarEleType.SOLDIER) //左边界只处理敌人
                {
                    WE.FactionType colFaction = WarFieldUtil.GetFactionByTag(tag);
                    if (colFaction == WE.FactionType.ENEMY)
                        collision.gameObject.GetComponent<Soldier>().StepOutofCave(_connectCave, _mapId, WE.OnGroundMapIndex, CaculateYPercent(
                            collision.transform.position.y));
                }
            }
            else if (edge == _right)
            {
                string tag = collision.gameObject.tag;
                WE.WarEleType colType = WarFieldUtil.GetWarEleType(tag);
                if (colType == WE.WarEleType.SOLDIER)
                {
                    WE.FactionType colFaction = WarFieldUtil.GetFactionByTag(tag);
                    if (colFaction == WE.FactionType.FRIENDLY) //右边界只处理友军
                        collision.gameObject.GetComponent<Soldier>().StepOutofCave(_connectCave, _mapId, WE.OnGroundMapIndex, CaculateYPercent(
                            collision.transform.position.y));
                }
            }
        }

#endregion

#region public functions
        //leftMidGap: 地图左边中心点距离上一个地图左边中心点的距离
        //nextLeftButtomPos： 返回这个地图左下角的位置
        public bool InitLevelMap(XmlNode mapNode, byte index, Vector2 leftMidGap, out Vector2 nextLeftButtomPos)
        {
            _mapId = index;
            gameObject.name += _mapId.ToString();
            nextLeftButtomPos = Vector2.zero;
            string mapType = ((XmlElement)mapNode).GetAttribute("type");
            if (mapType == "OnGround")
            {
                _mapType = WE.MapType.ONGROUND;
                _isOpened = true;
                _mapName = "Surface";
            }
            else if (mapType == "UnderGround")
            {
                _mapType = WE.MapType.UNDERGROUND;
                _isOpened = true; //false;  //for debug
                _mapName = "Cave " + (_mapId + 1);
            }
            else
                return false;

            CalRoadHight();//Obtain original bounds
            if (_mapType == WE.MapType.ONGROUND)
                transform.position += new Vector3(-_passablePart.min.x, -_passablePart.min.y - _passablePart.size.y / 2, 0);
            else
                transform.position += new Vector3(-_passablePart.min.x, -_passablePart.min.y - _passablePart.size.y + leftMidGap.y, 0);
            CalRoadHight();//update _passablePart position

            //get render texture
            {
                XmlNode texNode = mapNode.SelectSingleNode("texture");
                if (texNode == null)
                {
                    GameLogger.LogError($" {_mapId} Can not get texture node for map node");
                    return false;
                }

                string roadTexName = null, roadDecayTexName = null;
                foreach (XmlNode textureNode in texNode.ChildNodes)
                {
                    if (textureNode.NodeType == XmlNodeType.Comment)
                        continue;

                    XmlElement texEle = (XmlElement)textureNode;
                    switch(texEle.Name)
                    {
                        case "road":
                            roadTexName = texEle.GetAttribute("normal");
                            roadDecayTexName = texEle.GetAttribute("decay");
                            break;
                        default:
                            break;
                    }
                }

                Texture2D roadTex = Resources.Load<Texture2D>("Textures/MapTex/Level/GroundTexture/" + roadTexName);
                Texture2D roadDecayTex = Resources.Load<Texture2D>("Textures/MapTex/Level/GroundTexture/" + roadDecayTexName);

                if (ReferenceEquals(roadTex, null) == true || ReferenceEquals(roadDecayTex, null) == true)
                {
                    GameLogger.LogError($"Load map textures failed {_mapId}");
                    return false;
                }

                RenderRoad(roadTex, roadDecayTex);
            }
            SetDecorationRenderOrder();

            nextLeftButtomPos = new Vector2(_passablePart.min.x, _passablePart.min.y);
            _leftDownPos = nextLeftButtomPos;
            _leftMidPos = new Vector2(_leftDownPos.x, _leftDownPos.y + _passablePart.size.y / 2);

            // 创建左右两条边,仅仅只为underground map创建
            //两边位置往外面扩大了1
            if (_mapType == WE.MapType.UNDERGROUND)
            {
                _left = CreateEdge(new Vector2(_passablePart.min.x - 1, _passablePart.min.y),
                    new Vector2(_passablePart.min.x - 1, _passablePart.max.y));
                _right = CreateEdge(new Vector2(_passablePart.max.x + 1, _passablePart.min.y),
                    new Vector2(_passablePart.max.x + 1, _passablePart.max.y));
            }

            return AnalyzeBuildings(mapNode);
        }

        //设置地下map所关联的出口洞穴
        public bool SetStepoutCave(CaveTran cave)
        {
            if (ReferenceEquals(_connectCave, null) == true)
            {
                _connectCave = cave;
                return true;
            }
            else
            {
                GameLogger.LogError($"Underground Map {_mapId} has already assigned step out cave, can not assign again");
                return false;
            }
        }

        //计算可以通行部分地图的高度
        public void CalRoadHight()
        {
            SpriteRenderer[] sprites = _road.GetComponentsInChildren<SpriteRenderer>();
            if (sprites.Length == 0)
            {
                GameLogger.LogError($"没有找到子物体的 SpriteRenderer! {_mapId}");
                return;
            }

            _passablePart = sprites[0].bounds;
            for (int i = 1; i < sprites.Length; i++)
            {
                _passablePart.Encapsulate(sprites[i].bounds);
            }
            return;
        }

        //增加一种进入地图的己方兵，保存进入之后的位置，ui minimap调用
        //spawnIndex： 对应于barrack中spawn arr index
        //只针对undergroud map才需要
        public void AddEnterSd(SD.TroopType troopType, int spawnIndex)
        {
            var dic = _enterSdPos[(int)troopType];
            if (dic.ContainsKey(spawnIndex) == true) //以前进入过
            {
                dic[spawnIndex].p_enabled = true;
            }
            else
            {
                CaveEnterPos ep = new CaveEnterPos();
                ep.p_enabled = true;
                ep.p_enterPos = new Vector2(_leftDownPos.x, _leftDownPos.y + _passablePart.size.y / 2); //默认走中间
                _enterSdPos[(int)troopType].Add(spawnIndex, ep);
            }
        }

        //只针对undergroud map才需要
        public void RmEnterSd(SD.TroopType troopType, int spawnIndex)
        {
            var dic = _enterSdPos[(int)troopType];
            CaveEnterPos ep = dic[spawnIndex];
            if (ep != null)
            {
                ep.p_enabled = false; //保留之前的位置，但是不在minimap中显示，以免有些暂时没有进入的士兵进入时获取位置失败
            }
        }

        //yPercent: 位置距离左下角Y轴上比例
        //只针对undergroud map才需要
        public void UpdateEnterSdPos(SD.TroopType troopType, int spawnIndex, float yPercent)
        {
            var dic = _enterSdPos[(int)troopType];
            if (dic.ContainsKey(spawnIndex) == false)
            {
                GameLogger.LogError($"map {_mapId} not has soldier {troopType} {spawnIndex}");
                return;
            }
            dic[spawnIndex].p_enterPos = new Vector2(_leftDownPos.x, _passablePart.size.y * yPercent + _leftDownPos.y);
        }

        //只针对undergroud map才需要
        public Vector2 GetEnterSdPos(SD.TroopType troopType, int spawnIndex)
        {
            try
            {
                return _enterSdPos[(int)troopType][spawnIndex].p_enterPos;
            }
            catch (Exception e)
            {
                return new Vector2(_leftDownPos.x, _leftDownPos.y + _passablePart.size.y / 2); //以免有误入的士兵
            }

        }

#endregion

#region private functions
        private bool RenderRoad(Texture2D roadTex, Texture2D decayTex)
        {
            Transform tf = _road.transform;

            for (int i = 0; i < tf.childCount; i++)
            {
                SpriteRenderer tmp = tf.GetChild(i).GetComponent<SpriteRenderer>();
                if (ReferenceEquals(tmp, null) == false)
                {
                    tmp.sortingLayerName = "War_BackGround";
                    tmp.sortingOrder = 0;
                    tmp.material = _groundMat;
                    tmp.material.SetTexture("_NormalTex", roadTex);
                    tmp.material.SetTexture("_DecayTex", decayTex);
                }
            }

            return true;
        }

        private void SetDecorationRenderOrder()
        {
            List<Transform> renderList = Utils.GetChildrenGameObjects(_decoration.transform);

            foreach (Transform obj in renderList)
            {
                SpriteRenderer render = obj.GetComponent<SpriteRenderer>();
                if (render != null)
                {
                    render.sortingLayerName = "Element";
                    float y = obj.position.y - (render.sprite.rect.size / (2 * GD.PixelPerUnit)).y;
                    obj.position = new Vector3(obj.position.x, obj.position.y, WarFieldUtil.GetZByY(obj.position.y, _passablePart.min.y));
                }
            }
        }

        private bool AnalyzeBuildings(XmlNode mapNode)
        {
            bool loadFriendlyBuildings = true, loadEnemyBuildings = true, loadNeutralBuildings = true;

            foreach (XmlNode childNode in mapNode.ChildNodes)
            {
                if (childNode.NodeType == XmlNodeType.Comment)
                    continue;

                switch (childNode.Name)
                {
                    case "friendlyBuildings":
                        loadFriendlyBuildings = AnalyseFriendlyBuilding(childNode);
                        break;

                    case "enemyBuildings":
                        loadEnemyBuildings = AnalyseEnemyBuilding(childNode);
                        break;

                    case "NeutralBuildings":
                        loadNeutralBuildings = AnalyseNeutralBuilding(childNode);
                        break;

                    default:
                        break;
                }
            }

            if (_mapType == WE.MapType.ONGROUND)
            {
                if (loadFriendlyBuildings && loadEnemyBuildings && loadNeutralBuildings)
                    return true;
            }
            else if (_mapType == WE.MapType.UNDERGROUND)
            {
                if (loadEnemyBuildings && loadNeutralBuildings)
                    return true;
            }

            GameLogger.LogError(
                $"Failed to load level conf  {_mapId} friendly buildings: {loadFriendlyBuildings} enemy buildings: {loadEnemyBuildings} neutral buildings: {loadNeutralBuildings}");

            return false;
        }

        private bool AnalyseFriendlyBuilding(XmlNode parentNode)
        {
            WE.RaceType race = WE.RaceType.Human;

            if (parentNode.HasChildNodes == true)
            {
                foreach (XmlNode bdNode in parentNode.ChildNodes)
                {
                    if(bdNode.NodeType == XmlNodeType.Comment)
                        continue;

                    XmlElement bdEle = (XmlElement)bdNode;
                    if (bdEle.Name != "friendlyBuilding")
                    {
                        GameLogger.LogError($"Unknown xml tag {bdEle.Name}");
                        continue;
                    }
                    float xPos = float.Parse(bdEle.GetAttribute("xPos")) + _passablePart.min.x;
                    float yPos = float.Parse(bdEle.GetAttribute("yPos")) + _passablePart.min.y;
                    WBD.BuildingMode mode = Enum.Parse<WBD.BuildingMode>(bdEle.GetAttribute("type"));
                    switch (mode)
                    {
                        case WBD.BuildingMode.FORTRESS:
                        {
                            HumanDefines.FortressType subType = Enum.Parse<HumanDefines.FortressType>(bdEle.GetAttribute("subType"));
                            BuildingConf bdConf = WarBuildingCtrl.Instance.GetBdConf(WE.RaceType.Human, mode, (int)subType);
                            if (WarBuildingCtrl.Instance.AddBuildingDuringMapCreate(bdConf, new Vector2(xPos, yPos), (object)null, _mapId) == null)
                            {
                                GameLogger.LogError($" {_mapId} Fail to add friendly building {mode} {subType}");
                                return false;
                            }
                        }
                            break;
                        case WBD.BuildingMode.BARRACK:
                        {
                            HumanDefines.BarrackType subType = Enum.Parse<HumanDefines.BarrackType>(bdEle.GetAttribute("subType"));
                            BuildingConf bdConf = WarBuildingCtrl.Instance.GetBdConf(WE.RaceType.Human, mode, (int)subType);
                            if(WarBuildingCtrl.Instance.AddBuildingDuringMapCreate(bdConf, new Vector2(xPos, yPos), (object)null, _mapId) ==null)
                            {
                                GameLogger.LogError($" {_mapId} Fail to add friendly building {mode} {subType}");
                                return false;
                            }
                        }
                            break;
                        case WBD.BuildingMode.DEFENCE:
                        {
                            HumanDefines.DefenceType subType = Enum.Parse<HumanDefines.DefenceType>(bdEle.GetAttribute("subType"));
                            BuildingConf bdConf = WarBuildingCtrl.Instance.GetBdConf(WE.RaceType.Human, mode, (int)subType);
                            if (WarBuildingCtrl.Instance.AddBuildingDuringMapCreate(bdConf, new Vector2(xPos, yPos), (object)null, _mapId) == null)
                            {
                                GameLogger.LogError($" {_mapId} Fail to add friendly defence {mode} {subType}");
                                return false;
                            }
                        }
                            break;
                        default:
                            GameLogger.LogError($" {_mapId} Unknown building mode {mode}");
                            return false;
                    }
                }
                return true;
            }
            GameLogger.LogError($"Fail to load friendly buildings in level map conf  {_mapId}");
            return false;
        }

        private bool AnalyseNeutralBuilding(XmlNode parentNode)
        {
            WE.RaceType race = WE.RaceType.Human;

            if (parentNode.HasChildNodes == true)
            {
                foreach (XmlNode bdNode in parentNode.ChildNodes)
                {
                    if(bdNode.NodeType == XmlNodeType.Comment)
                        continue;

                    XmlElement bdEle = (XmlElement)bdNode;
                    switch (bdEle.Name)
                    {
                        case "portal": //传送点
                        {
                            float xPos = float.Parse(bdEle.GetAttribute("xPos")) + _passablePart.min.x;
                            float[] yPos = bdEle.GetAttribute("yPos").Split(',').Select(x => float.Parse(x.Trim())).ToArray();
                            Array.Sort(yPos);
                            Array.Reverse(yPos); //将y从大到小排序,从上到下的排序
                            BuildingConf bdConf = WarBuildingCtrl.Instance.GetBdConf(WE.RaceType.Neutral, WBD.BuildingMode.PORTAL, (int)
                                NeutralDefines.PortalType.PORTAL);
                            for (int i = 0; i < yPos.Length; i++)
                            {
                                yPos[i] += _passablePart.min.y;
                                Portal pt = (Portal)WarBuildingCtrl.Instance.AddBuildingDuringMapCreate(bdConf, new Vector2(xPos, yPos[i]),
                                    (object)null, _mapId);
                                if (ReferenceEquals(pt, null) == true)
                                {
                                    GameLogger.LogError
                                        ($" {_mapId} Fail to add neutral building {WBD.BuildingMode.PORTAL} {NeutralDefines.PortalType.PORTAL}");
                                    return false;
                                }

                                pt.gs_order = i; //保存y信息，minimap需要用
                            }
                        }
                            break;
                        case "goldMine":
                        {
                            float xPos = float.Parse(bdEle.GetAttribute("xPos")) + _passablePart.min.x;;
                            float yPos = float.Parse(bdEle.GetAttribute("yPos")) + _passablePart.min.y;;
                            NeutralDefines.GoldMineType subType = (NeutralDefines.GoldMineType)int.Parse(bdEle.GetAttribute("level"));
                            BuildingConf bdConf = WarBuildingCtrl.Instance.GetBdConf(WE.RaceType.Neutral, WBD.BuildingMode.GOLDMINE, (int)subType);
                            if (WarBuildingCtrl.Instance.AddBuildingDuringMapCreate(bdConf, new Vector2(xPos, yPos), (object)null, _mapId) == null)
                            {
                                GameLogger.LogError($" {_mapId} Fail to add neutral building {WBD.BuildingMode.GOLDMINE} {subType}");
                                return false;
                            }
                        }
                            break;
                        case "gemMine":
                        {
                            float xPos = float.Parse(bdEle.GetAttribute("xPos")) + _passablePart.min.x;
                            float yPos = float.Parse(bdEle.GetAttribute("yPos")) + _passablePart.min.y;
                            NeutralDefines.GemMineType subType = (NeutralDefines.GemMineType)int.Parse(bdEle.GetAttribute("level"));
                            BuildingConf bdConf = WarBuildingCtrl.Instance.GetBdConf(WE.RaceType.Neutral, WBD.BuildingMode.GEMMINE, (int)subType);
                            if (WarBuildingCtrl.Instance.AddBuildingDuringMapCreate(bdConf, new Vector2(xPos, yPos), (object)null, _mapId) == null)
                            {
                                GameLogger.LogError($" {_mapId} Fail to add neutral building {WBD.BuildingMode.GEMMINE} {subType}");
                                return false;
                            }
                        }
                            break;
                        case "cave":
                        {
                            float xPos = float.Parse(bdEle.GetAttribute("xPos")) + _passablePart.min.x;
                            float yPos = float.Parse(bdEle.GetAttribute("yPos")) + _passablePart.min.y;
                            BuildingConf bdConf = WarBuildingCtrl.Instance.GetBdConf(WE.RaceType.Neutral, WBD.BuildingMode.CAVE,
                                (int)NeutralDefines.CaveType.CAVE);
                            CaveTran bd = (CaveTran)WarBuildingCtrl.Instance.AddBuildingDuringMapCreate(bdConf, new Vector2(xPos, yPos), (object)null,
                                    _mapId);
                            if (ReferenceEquals(bd, null) == true)
                            {
                                GameLogger.LogError($" {_mapId} Fail to add neutral building {NeutralDefines.CaveType.CAVE}");
                                return false;
                            }

                            byte ungroundMapId = byte.Parse(bdEle.GetAttribute("ungroundMap"));
                            bd.AddMapToCave(ungroundMapId); //关联上不同地图
                        }
                            break;
                        default:
                            GameLogger.LogError($" {_mapId} Unknown xml tag {bdEle.Name}");
                            return false;
                    }
                }
                return true;
            }
            GameLogger.LogError($" {_mapId} Fail to load neutral buildings in level map conf");
            return false;
        }

        private bool AnalyseEnemyBuilding(XmlNode parentNode)
        {
            WE.RaceType race;

            foreach (XmlNode raceNode in parentNode.ChildNodes)
            {
                if(raceNode.NodeType == XmlNodeType.Comment)
                    continue;

                race = Enum.Parse<WE.RaceType>(((XmlElement)raceNode).GetAttribute("type"), true); //忽略大小写
                if (LoadEnemyBuildingByRace(race, raceNode) == false)
                {
                    GameLogger.LogError($" {_mapId} Fail to load {race} building");
                    return false;
                }
            }
            return true;
        }

        private bool LoadEnemyBuildingByRace(WE.RaceType race, XmlNode parentNode)
        {
            foreach (XmlNode bdNode in parentNode.ChildNodes)
            {
                if(bdNode.NodeType == XmlNodeType.Comment)
                    continue;
                XmlElement bdEle = (XmlElement)bdNode;
                float xPos = float.Parse(bdEle.GetAttribute("xPos")) + _passablePart.min.x;
                float yPos = float.Parse(bdEle.GetAttribute("yPos")) + _passablePart.min.y;

                WBD.BarrackTriggerStage defaultStatge =
                    Enum.TryParse<WBD.BarrackTriggerStage>((bdEle.GetAttribute("defaultStage")), out WBD.BarrackTriggerStage val)
                        ? val
                        : WBD.BarrackTriggerStage.MIN;
                WarBuilding bd = null;
                if(defaultStatge != WBD.BarrackTriggerStage.MIN)
                    bd = WarBuildingCtrl.Instance.AddBuildingDuringMapCreate(bdNode.SelectSingleNode("bdConf"), new Vector2(xPos, yPos),
                    defaultStatge, _mapId);
                else
                    bd = WarBuildingCtrl.Instance.AddBuildingDuringMapCreate(bdNode.SelectSingleNode("bdConf"), new Vector2(xPos, yPos), (object)null,
                        _mapId);
                if (bd == null)
                {
                    GameLogger.LogError($" {_mapId} Fail to add enemy builind at ({xPos}, {yPos})");
                    return false;
                }

                if (bd.gs_mode == WBD.BuildingMode.BARRACK)
                {
                    if (AnalyseEnemyInBuilding(race, (EnemyBarrack)bd, bdNode) == false)
                    {
                        GameLogger.LogError($" {_mapId} Fail to add soldiers to building {bd.gs_mode} {xPos} {yPos}");
                        return false;
                    }
                }
            }
            return true;
        }

        private bool AnalyseEnemyInBuilding(WE.RaceType race, EnemyBarrack bd, XmlNode parentNode)
        {
            foreach (XmlNode stageNode in parentNode.ChildNodes)
            {
                if(stageNode.NodeType == XmlNodeType.Comment)
                    continue;
                if(stageNode.Name != "stage")
                    continue;

                XmlElement stageEle = (XmlElement)stageNode;
                WBD.BarrackTriggerStage stage = Enum.Parse<WBD.BarrackTriggerStage>(stageEle.GetAttribute("type"));
                float distance = float.Parse(stageEle.GetAttribute("distance"));
                List<object> enemyInfoList = new List<object>();

                foreach (XmlNode enemyNode in stageNode.ChildNodes)
                {
                    if (enemyNode.NodeType == XmlNodeType.Comment)
                        continue;

                    XmlElement enemyEle = (XmlElement)enemyNode;
                    SD.TroopType troop = Enum.Parse<SD.TroopType>(enemyEle.GetAttribute("troop"), true);
                    int sdType = int.Parse(enemyEle.GetAttribute("soldier"));
                    int spawnCntPerTime = int.Parse(enemyEle.GetAttribute("spawnCntPerTime"));
                    int spawnMaxTime = int.TryParse(enemyEle.GetAttribute("spawnMaxTime"), out int tmp) ? tmp : -1;
                    var info = (troop, sdType, spawnCntPerTime, spawnMaxTime);
                    enemyInfoList.Add(info);
                }

                if(enemyInfoList.Count == 0)
                    continue;

                if (bd.AddSpawnStage(stage, distance, enemyInfoList) == false)
                {
                    GameLogger.LogError($" {_mapId} Fail to add stage {race} {stage} {distance} to enemy building");
                    return false;
                }
            }
            return true;
        }

        private EdgeCollider2D CreateEdge(Vector2 start, Vector2 end)
        {
            EdgeCollider2D edge = gameObject.AddComponent<EdgeCollider2D>();

            // 把世界坐标转成 local 坐标
            Vector2 localStart = transform.InverseTransformPoint(start);
            Vector2 localEnd   = transform.InverseTransformPoint(end);

            edge.points = new Vector2[] { localStart, localEnd };
            return edge;
        }

        //计算y与地图高的比值
        //y是一个世界坐标的y
        private float CaculateYPercent(float y)
        {
            return (y - _passablePart.min.y) / _passablePart.size.y;
        }
#endregion
    }
}

