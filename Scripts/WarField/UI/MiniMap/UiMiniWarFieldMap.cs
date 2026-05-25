using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

using UIMiniMapDefines;

namespace WarField
{
    using WE = WarFieldElements;
    using WBD = WarBuildingDefines;
    using UD = UIDefines;
    using SD = SoldierDefines;

    public class UiMiniWarFieldMap : MonoBehaviour, ITask
    {
#region public parameters

#endregion

#region private parameters
        //相同x的3个portal组成一个pair
        private class MiniMapPortalPair
        {
            public float p_xPosInScene; //在世界坐标下的实际x，可能通过浮点运算后在minimap中portal的位置不一定能计算出完全相同的结果
            public UIPortalTransportation[] p_portalTransportations = new UIPortalTransportation[3]; //按照上中下顺序排列,从上到下的排序
        }

        [SerializeField] private GameObject _sdTrailPfb;

        private RectTransform _trails;//sd trail 添加在这个节点下面
        private RectTransform _rectTransform;

        //recode the relationship the objects in minimap with the objects in warfield <mapid, [WE.WarEleType]>
        private AsyncDataPool<WarEleParent>[] _activeList; //[WE.WarEleType.MAXRIVAL]
        private Hero _heroIcon;

        private List<MiniMapPortalPair> _portalPairs;


        // [SerializeField] private RawImage _testImage;
        private DataPool<UIMiniMapSdTrail> _trailPool;
        private List<UIMiniMapSdTrail> _curTrail;
        private LevelMap _realMap; //minimap对应的实际地图
        private Vector2 _miniMapSize;
        private float _portalRange; //portal在小地图中的覆盖范围
        private Vector2 _ratio = new Vector2(-1, -1);

        //cave
        private List<CaveTran> _caveList; //cave在minimap中gameobject的队列，x从小到大排列
        private float _caveRange;

        private bool _rangeShowed; //portal/cave cover range是否显示

        private Vector2 _hideOffset; //不显示时的位置,用于计算element的位置

        private bool _isShowed = false;
        private bool _beInited = false;
#endregion

#region private parameters' get set
        public Vector2 gs_hideOffset
        {
            get { return _hideOffset; }
        }

        public Vector2 gs_ratio
        {
            get { return _ratio; }
        }
#endregion

#region Unity callbacks
        private void Awake()
        {
            _activeList = new AsyncDataPool<WarEleParent>[(int)WE.WarEleType.MAXRIVAL];
            for (int i = 1; i < (int)WE.WarEleType.MAXRIVAL; i++)
                _activeList[i] = new AsyncDataPool<WarEleParent>();

            _portalPairs = new List<MiniMapPortalPair>();

            _heroIcon = null;
            _rectTransform = GetComponent<RectTransform>();
            _trails = transform.Find("SdSpawnTrails").GetComponent<RectTransform>();

            _trailPool = new DataPool<UIMiniMapSdTrail>(false);
            for (int i = 0; i < 10; i++) //pool总共有10个trail,所以显示的路径不能超过10段
            {
                UIMiniMapSdTrail trail = Instantiate(_sdTrailPfb, _trails).GetComponent<UIMiniMapSdTrail>();
                _trailPool.AddItem(trail);
                trail.gameObject.SetActive(false);
            }
            _curTrail = new List<UIMiniMapSdTrail>();
            _caveList = new List<CaveTran>();
            _beInited = false;
        }

        private void OnDestroy()
        {
            for(int i=1;i<(int)WE.WarEleType.MAXRIVAL;i++)
                _activeList[i]?.Dispose();
        }

#endregion

#region public functions
        public bool InitWarFieldMap(LevelMap levelMap, Vector2 hideOffset)
        {
            if (_beInited)
                return false;

            _realMap = levelMap;
            _hideOffset = hideOffset;

            {
                RectTransform rect = gameObject.GetComponent<RectTransform>();
                float ratio = rect.rect.size.y/_realMap.gs_passablePart.size.y;
                _ratio = new Vector2(ratio, ratio);
                float width = _realMap.gs_passablePart.size.x * ratio;
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                _miniMapSize = rect.rect.size;
            }

            //此时levelmap中预定义的building已经已经添加完成
            _activeList[(int)WE.WarEleType.SOLDIER].ForEachAndFlush(static bd => bd.UpdateMinimapObject()); //only flush

            if (_realMap.gs_mapIndex == WE.OnGroundMapIndex)
                _heroIcon = SoldierCtrl.Instance.gs_curHero;
            WarFieldGameManager.Instance.RegisterTask(this, WE.TaskType.NORMAL);
            WarFieldGameManager.Instance.ActiveTask(this, WE.TaskType.NORMAL, 0.1f);
            _beInited = true;
            return true;
        }

        //add building/soldier into minimap
        public void NotifyAddedGameObj(WarEleParent addObj)
        {
            _activeList[(int)addObj.gs_warEleType].AddItemAsync(addObj);
        }

        //remove soldier/building from minimap
        //为了不加锁，这个api必须在主线程调用 ！！！
        public void NotifyRemovedGameObj(WarEleParent rmObj)
        {
            _activeList[(int)rmObj.gs_warEleType].RemoveItemAsync(rmObj);
        }

        public void ShowWarFieldMiniMap()
        {
            int cnt = _portalPairs.Count;
            for (int i = 0; i < cnt; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    _portalPairs[i].p_portalTransportations[j].OnShow();
                }
            }
            _isShowed = true;

            //将mini map内的obj 显示
            _activeList[(int)WE.WarEleType.SOLDIER].ForEachReadOnly(static item => item.UpdateMinimapObject());
            _activeList[(int)WE.WarEleType.BUILDING].ForEachReadOnly(static item => item.UpdateMinimapObject());
        }

        //offset:隐藏时位置
        public void HideWarFieldMiniMap()
        {
            _isShowed = false;
            int cnt = _portalPairs.Count;
            for (int i = 0; i < cnt; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    _portalPairs[i].p_portalTransportations[j].OnHide();
                }
            }

            //将mini map内的obj 隐藏
            _activeList[(int)WE.WarEleType.SOLDIER].ForEachReadOnly(static item => item.HideMinimapObject());
            _activeList[(int)WE.WarEleType.BUILDING].ForEachReadOnly(static item => item.HideMinimapObject());
        }

        //获取portal下一个pair
        public UIPortalTransportation[] GetNextPortalPair(UIPortalTransportation portalTran)
        {
            int cnt = _portalPairs.Count;
            for (int i = 0; i < cnt - 1; i++) //最后一个portal没有下一个传送目标，所以 i < cnt - 1,最后一个MiniMapPortalPair不用匹配了
            {
                for (int j = 0; j < 3; j++)
                {
                    if (_portalPairs[i].p_portalTransportations[j] == portalTran)
                        return _portalPairs[i + 1].p_portalTransportations;
                }
            }

            return null;
        }

        //yPoint是屏幕坐标的y
        public void ShowTrailAt(float yPoint)
        {
            if(_rangeShowed == false)
                ShowCoverRange(); //显示portal/cave cover range

            float yPos = yPoint, xPos = 0;
            HideTrails(false);

            if (_realMap.gs_mapType == WE.MapType.UNDERGROUND)
            {
                //先将本地指标转成世界坐标
                Vector2 from = _rectTransform.TransformPoint(xPos, yPos, 0);
                Vector2 to = _rectTransform.TransformPoint(_miniMapSize.x, yPos, 0); //yEndPos == yStartPos  一条水平直线
                UIMiniMapSdTrail trail = GetTrailFromPool();
                if (ReferenceEquals(trail, null) == true)
                {
                    GameLogger.LogError($"Fail to get trail from pool");
                    return;
                }
                trail.SetTrail(from, to, false);
                trail.SetActive(true);
                trail.gameObject.SetActive(true);
                _curTrail.Add(trail);
            }
            else //地面
            {
                //是否与portal相交
                int cnt = _portalPairs.Count;

                for (int i = 0; i < cnt; i++)
                {
                    MiniMapPortalPair pair = _portalPairs[i];

                    for (int j = 0; j < 3; j++) //3 个portal
                    {
                        UIPortalTransportation portal = pair.p_portalTransportations[j];
                        UIPortalTransportation nextPortal = portal.gs_nextPortal;

                        if (portal.gs_canTransmit == true)
                        {
                            Vector2 potalPos = portal.transform.position;
                            float distance = potalPos.y - yPos;
                            if (Mathf.Abs(distance) < _portalRange) //进入传送范围
                            {
                                //前往传送门的的trail
                                //设置trial起始点
                                Vector2 from = _rectTransform.TransformPoint(xPos, yPos, 0);
                                xPos = potalPos.x;
                                Vector2 to = _rectTransform.TransformPoint(xPos, yPos, 0); //先将本地指标转成世界坐标
                                UIMiniMapSdTrail trail = GetTrailFromPool();
                                if (ReferenceEquals(trail, null) == true)
                                {
                                    GameLogger.LogError($"Fail to get trail from pool");
                                    return;
                                }
                                trail.SetTrail(from, to, false);
                                trail.SetActive(true);
                                trail.gameObject.SetActive(true);
                                _curTrail.Add(trail);

                                //传送门之间的传送trail, 可能有多级传送
                                while (nextPortal != null)
                                {
                                    potalPos = nextPortal.gs_rectTransform.position;
                                    trail = GetTrailFromPool();
                                    from = _rectTransform.TransformPoint(xPos, yPos, 0);
                                    xPos = potalPos.x;
                                    yPos = potalPos.y - distance;
                                    to = _rectTransform.TransformPoint(xPos, yPos, 0);
                                    trail.SetTrail(from, to, true);
                                    trail.SetActive(true);
                                    trail.gameObject.SetActive(true);
                                    _curTrail.Add(trail);

                                    if(nextPortal.gs_canTransmit == true)//确认下一跳portal能继续传送
                                        nextPortal = nextPortal.gs_nextPortal;
                                    else
                                        break;
                                }

                                i = nextPortal.gs_pairIndex; //中间的pair直接不用遍历了
                                break; //trail 只可能同时进入一个pair中的一个portal，不用再遍历其他的了
                            }
                        }
                    }
                }

                //生成最后一段trail
                {
                    UIMiniMapSdTrail trail = GetTrailFromPool();
                    Vector2 from = _rectTransform.TransformPoint(xPos, yPos, 0);
                    Vector2 to = _rectTransform.TransformPoint(_miniMapSize.x, yPos, 0); //先将本地指标转成世界坐标
                    trail.SetTrail(from, to, false);
                    trail.SetActive(true);
                    trail.gameObject.SetActive(true);
                    _curTrail.Add(trail);
                }

            }
        }

        //hideRange: 是否隐藏portal、cave的cover range 显示
        public void HideTrails(bool hideRange)
        {
            int cnt = _curTrail.Count;
            for (int i = 0; i < cnt; i++)
            {
                _curTrail[i].gameObject.SetActive(false);
                ReleaseSdTrail(_curTrail[i]);
            }
            _curTrail.Clear();

            if (hideRange == true)
                HideCoverRange();
        }

        //检测trail是否与cave相交，如果相交通知cave关联的地图添加新的soldier
        // from.y == to.y
        //from to都是世界坐标
        public List<CaveTran> CheckCaves(Vector2 from, Vector2 to)
        {
            List<CaveTran> passedList= null;
            int cnt = _caveList.Count;
            for (int i = 0; i < cnt; i++)
            {
                var caveTran = _caveList[i].gs_miniMapObjTransform;
                Vector2 cavePos = caveTran.position;
                float distance = cavePos.y - from.y;
                if (Mathf.Abs(distance) < _caveRange)
                {
                    if (cavePos.x >= from.x && cavePos.x <= to.x)
                    {
                        if(passedList == null)
                            passedList = new List<CaveTran>();
                        passedList.Add(_caveList[i]);
                    }
                }
            }
            return passedList;
        }

        //根据鼠标抬起时的位置计算行进路线上的portal和cave，然后将位置通知levelmap、barrack
        //当鼠标抬起时才会最终计算结果保存
        //同时通知到levelmap或者barrack
        public void CalculateTrail(SdTrailInfoInSingleMap trailInfo, float yPoint, SD.TroopType troop, int spawnIndex, SoldierConf conf)
        {
            float yPos = yPoint, xPos = 0;

            float ySpacePercent = yPoint / _rectTransform.rect.size.y;
            if (_realMap.gs_mapType == WE.MapType.UNDERGROUND)
            {
                _realMap.UpdateEnterSdPos(troop, spawnIndex, ySpacePercent); //underground map不需要就算portal和cave
            }
            else //地面
            {
                WarBuildingCtrl.Instance.GetFriendlyBarrackByTroop(troop).SetSpawnTargetPos(ySpacePercent, spawnIndex);

                List<CaveTran> passCave = new List<CaveTran>();
                List<UIPortalTransportation> passPortal = new List<UIPortalTransportation>();

                //是否与portal相交
                int cnt = _portalPairs.Count;

                for (int i = 0; i < cnt; i++)
                {
                    MiniMapPortalPair pair = _portalPairs[i];

                    for (int j = 0; j < 3; j++) //3 个portal
                    {
                        UIPortalTransportation portal = pair.p_portalTransportations[j];
                        UIPortalTransportation nextPortal = portal.gs_nextPortal;

                        if (portal.gs_canTransmit == true)
                        {
                            Vector2 potalPos = portal.transform.position;
                            float distance = potalPos.y - yPos;
                            if (Mathf.Abs(distance) < _portalRange) //进入传送范围
                            {
                                //前往传送门的的trail
                                //设置trial起始点
                                Vector2 from = _rectTransform.TransformPoint(xPos, yPos, 0);
                                xPos = potalPos.x;
                                Vector2 to = _rectTransform.TransformPoint(xPos, yPos, 0); //先将本地指标转成世界坐标
                                //检查这一段trial是否与cave范围相交
                                var ret = CheckCaves(from, to);
                                if(ret != null)
                                    passCave.AddRange(ret);
                                passPortal.Add(portal);

                                //传送门之间的传送trail, 可能有多级传送,传送不用检查cave
                                while (nextPortal != null)
                                {
                                    passPortal.Add(nextPortal);
                                    if(nextPortal.gs_canTransmit == true)//确认下一跳portal能继续传送
                                        nextPortal = nextPortal.gs_nextPortal;
                                    else
                                        break;
                                }
                                i = nextPortal.gs_pairIndex; //中间的pair直接不用遍历了
                                break; //trail 只可能同时进入一个pair中的一个portal，不用再遍历其他的了
                            }
                        }
                    }
                }

                //计算最后一段trail
                {
                    Vector2 from = _rectTransform.TransformPoint(xPos, yPos, 0);
                    Vector2 to = _rectTransform.TransformPoint(_miniMapSize.x, yPos, 0); //先将本地指标转成世界坐标
                    //检查这一段trial是否与cave范围相交
                    var ret = CheckCaves(from, to);
                    if(ret != null)
                        passCave.AddRange(ret);
                }

                trailInfo.p_passedPortals = passPortal;
                if (trailInfo.p_passedCaves == null)
                {
                    cnt = passCave.Count;
                    for (int i = 0; i < cnt; i++)
                    {
                        int inMapId = passCave[i].gs_insideMapId;
                        UiMiniMapCtrl.Instance.AddSdToMiniMap(troop, spawnIndex, conf, inMapId);
                    }
                }
                else
                {
                    var addList = passCave.Except(trailInfo.p_passedCaves).ToList();
                    cnt = addList.Count;
                    for (int i = 0; i < cnt; i++)
                    {
                        int inMapId = passCave[i].gs_insideMapId;
                        UiMiniMapCtrl.Instance.AddSdToMiniMap(troop, spawnIndex, conf, inMapId);
                    }

                    var rmList = trailInfo.p_passedCaves.Except(passCave).ToList();
                    cnt = rmList.Count;
                    for (int i = 0; i < cnt; i++)
                    {
                        int inMapId = trailInfo.p_passedCaves[i].gs_insideMapId;
                        UiMiniMapCtrl.Instance.RmSdFromMiniMap(troop, spawnIndex, conf, inMapId);
                    }
                }
                trailInfo.p_passedCaves = passCave;
            }
        }

        //将portal添加到list，同时按照x从小到大排序
        public void AddPortalInMiniMapList(UIPortalTransportation portalTran, Portal portal)
        {
            float x = portal.transform.position.x;
            int cnt = _portalPairs.Count;
            for (var i = 0; i < cnt; i++)
            {
                var tmp = _portalPairs[i];
                if (Mathf.Abs(x - tmp.p_xPosInScene) < 1) //一定的容错
                {
                    tmp.p_portalTransportations[portal.gs_order] = portalTran;
                    return;
                }
            }
            MiniMapPortalPair pair = new MiniMapPortalPair();
            pair.p_xPosInScene = x;
            pair.p_portalTransportations[portal.gs_order] = portalTran;

            _portalPairs.Add(pair);
            _portalPairs.Sort((a, b) => a.p_xPosInScene.CompareTo(b.p_xPosInScene));
            return;
        }

        //将cave添加到list，同时按照x从小到大排序
        public void AddCaveInList(CaveTran cave)
        {
            _caveList.Add(cave);
            _caveList.Sort((a, b) => a.gs_transform.position.x.CompareTo(b.gs_transform.position.x));
        }

        public void RunNormalTask(float deltaTime)
        {
            if (_isShowed == false)
            {
                _activeList[(int)WE.WarEleType.SOLDIER].ForEachAndFlush(null); //only flush
                return;
            }

            //只更新solder的位置（不包括hero）
            _activeList[(int)WE.WarEleType.SOLDIER].ForEachAndFlush(static item => item.UpdateMinimapObject());

            //update hero as a special type
            //TODO: need to check hero status
            if (_heroIcon != null)
                _heroIcon.UpdateMinimapObject();

            int cnt = _portalPairs.Count;
            for (int i = 0; i < cnt; i++)
            {
                for (int j = 0; j < 3; j++)
                    _portalPairs[i].p_portalTransportations[j].UpdatePortalStatus();
            }
        }

        public void RunFixTask(float deltaTime)
        {
            throw new NotImplementedException();
        }

#endregion

#region private functions
        private UIMiniMapSdTrail GetTrailFromPool()
        {
            UIMiniMapSdTrail trail = null;
            if (_trailPool.Count > 0)
            {
                trail = _trailPool.GetByIndex(0);
                _trailPool.RemoveItemAt(0);
            }
            else
                GameLogger.LogError("tail pool is empty, too many trail parts > 10");
            return trail;
        }

        private void ReleaseSdTrail(UIMiniMapSdTrail trail)
        {
            _trailPool.AddItem(trail);
        }

        //显示portal、cave的cover range
        private void ShowCoverRange()
        {
            if (_rangeShowed == false)
            {
                int cnt = _portalPairs.Count;
                for (int i = 0; i < cnt; i++)
                {
                    for (int j = 0; j < 3; j++)
                        _portalPairs[i].p_portalTransportations[j].ShowRangeInMinimap();
                }

                cnt = _caveList.Count;
                for (int i = 0; i < cnt; i++)
                {
                    _caveList[i].ShowRangeInMinimap();
                }

                _rangeShowed = true;
            }
        }

        private void HideCoverRange()
        {
            if (_rangeShowed == true)
            {
                int cnt = _portalPairs.Count;
                for (int i = 0; i < cnt; i++)
                {
                    for (int j = 0; j < 3; j++)
                        _portalPairs[i].p_portalTransportations[j].HideRangeInMinimap();
                }

                cnt = _caveList.Count;
                for (int i = 0; i < cnt; i++)
                {
                    _caveList[i].HideRangeInMinimap();
                }

                _rangeShowed = false;
            }
        }
#endregion
     }
}

