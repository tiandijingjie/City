using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using WE = WarFieldElements;

    public interface IOnMapChange
    {
        public void OnMapChange(WarEleParent obj, int fromMap, int toMap);
        public void ReleaseInterface();
    }

    //WarEleType的父类
    public class WarEleParent : MonoBehaviour, ITask, IGridNode
    {
#region public parameters

#endregion

#region private parameters
        [SerializeField] protected WE.WarEleType _warEleType;
        [SerializeField] protected WE.FactionType _faction;
        [SerializeField] protected bool _needBindMiniMap = true; //是否与minimap中图标绑定

        //task control
        [SerializeField] protected WE.TaskType[] _taskTypes = { WE.TaskType.FIXED }; //默认只有fixedupdate
        [SerializeField] protected float[] _taskIntervals = { -1 }; //两次任务调用之间的时间间隔，-1表示每次都调用

        //所有WarEleType的gameobject必须继承WarEleParent，统一为他们赋予id，通过id能够进行区分
        //在具体实现时，需要声明为 p_wfId { get; private set; }
        //_wfId: {WE.WarEleType}_{WE.FactionType}_XXXX
        protected string _wfId;
        protected byte _mapId; //所处的地图id,  WE.OnGroundMapIndex表示在地面，其他值表示地下
        protected Vector2 _mapPassableBase; //实际地图的包围盒的左下角，用来计算一个位置差，计算minimap中的图标位置
        protected Transform _transform;

        protected List<IOnMapChange> _mapChangeObservers = null;

        protected GameObject _miniMapObj; //the GameObject the in mini map binding to WarEleParent, only soldier and building set this value
        protected RectTransform _miniMapObjTransform; //transform of _miniMapObj
        protected Vector2 _minimapHideOffset; //小地图不显示的时候，需要移动到屏幕外面去
        protected Vector2 _minimapRatio; //用于计算小地图中位置
        protected bool _isMiniMapBind; //是不是完成了小地图绑定

        // info in spatical grid
        protected BodyAuthoring _bodyAuthoring; //对因为body的大小和位置
        protected float _bodyRadius; //身体的碰撞半径
        protected int _gridIndex; //obj对应的entity在spatial grid中存储的id
        protected GridEntityData _entityData;
        protected uint _entitySubType = 0; //是各种type的位集合,每个type占用一个byte

        protected bool _isPaused;
        protected bool _beInited;

#endregion

#region private parameters' get set
        //interface IGridNode
        public int gs_gridIndex
        {
            get { return _gridIndex; }
            set { _gridIndex = value; }
        }

        public string gs_wfId
        {
            get { return _wfId; }
        }

        public WE.WarEleType gs_warEleType
        {
            get { return _warEleType; }
        }

        public virtual WE.FactionType gs_faction
        {
            get { return _faction; }
        }

        public byte gs_mapId
        {
            get { return _mapId; }
        }

        public bool gs_beInited
        {
            get { return _beInited; }
        }

        public Transform gs_transform
        {
            get { return _transform; }
        }

        public GameObject gs_miniMapObj
        {
            get { return _miniMapObj; }
        }

        public RectTransform gs_miniMapObjTransform
        {
            get { return _miniMapObjTransform; }
        }

        public float gs_bodyRadius
        {
            get { return _bodyRadius; }
        }

        public GridEntityData gs_entityData
        {
            get  { return _entityData; }
        }
#endregion

#region Unity callbacks

        protected virtual void Awake()
        {
            _wfId = "";
            _transform = transform;
            _mapId = WE.InvalidMapIndex;
            _bodyAuthoring = GetComponent<BodyAuthoring>();
            _bodyRadius = _bodyAuthoring.gs_radius;
            if(_bodyAuthoring == null)
                GameLogger.LogError($"{gameObject.name} Not set the body");
            _isPaused = false;
            _gridIndex = -1;
            _isMiniMapBind = false;
            _beInited = false;
        }

#endregion

#region public functions

        public bool InitWarEle(byte mapId)
        {
            if (_warEleType == WE.WarEleType.MIN)
            {
                GameLogger.LogError($"{gameObject.name} not set the _warEleType");
            }
            ChangeMapId(mapId);
            CreateWarId();

            if (_taskIntervals.Length != _taskTypes.Length)
            {
                GameLogger.LogError($"task inteval count is not the same as the task type count");
                return false;
            }

            for (int i = 0; i < _taskTypes.Length; i++)
            {
                WarFieldGameManager.Instance.RegisterTask(this, _taskTypes[i]);
                WarFieldGameManager.Instance.ActiveTask(this, _taskTypes[i], _taskIntervals[i]);
            }

            if (_entitySubType == 0)
            {
                GameLogger.LogError($"{gameObject.name} not set the _entitySubType");
                return false;
            }

            _bodyAuthoring.InitType(_warEleType, _entitySubType);
            _entityData = _bodyAuthoring.BakeToEntitie(_mapId, GetSpatialEntitySpecData());
            SpatialGridManager.Instance.AddEntity(_entityData, this);
            if(_bodyAuthoring.gs_writeToFlowField == true)
                WarMapCtrl.Instance.GetPathFinderMapByIndex(_mapId).AddEntity(_entityData);
            _beInited = true;
            return true;
        }

        public virtual void DeInit()
        {
            for (int i = 0; i < _taskTypes.Length; i++)
            {
                WarFieldGameManager.Instance.SuspendTask(this, _taskTypes[i]);
                WarFieldGameManager.Instance.UnregisterTask(this, _taskTypes[i]);
            }

            //_wfId = "";
            if (_mapChangeObservers != null)
            {
                int cnt = _mapChangeObservers.Count;
                for (int i = 0; i < cnt; i++)
                    _mapChangeObservers[i].ReleaseInterface();
                _mapChangeObservers.Clear(); //清空回调
            }
            UnbindMiniMapObj();
            //删除流场地图中的数据
            if(_bodyAuthoring.gs_writeToFlowField == true)
                WarMapCtrl.Instance.GetPathFinderMapByIndex(_mapId).RemoveEntity(_transform.position);

            //删除在spatial grid中的entity
            SpatialGridManager.Instance.RemoveEntity(_warEleType, _gridIndex);
            _gridIndex = -1;

            _mapId = WE.InvalidMapIndex;
            _beInited = false;
        }

        //初始化和进入新地图时需要调用
        public virtual void ChangeMapId(byte mapId)
        {
            if (mapId == WE.InvalidMapIndex)
            {
                GameLogger.LogError("Invalid map id");
                return;
            }

            int prvMapId = _mapId;
            _mapId = mapId;

            if (prvMapId != _mapId)
            {
                if (_mapChangeObservers != null)
                {
                    int cnt = _mapChangeObservers.Count;
                    for (int i = 0; i < cnt; i++)
                        _mapChangeObservers[i].OnMapChange(this, prvMapId, _mapId);
                }
            }
            _mapPassableBase = WarMapCtrl.Instance.GetMapByIndex(_mapId).gs_passablePart.min;
            ChangeMiniMap(prvMapId, _mapId);
        }

        public bool RegisterMapChangeObserver(IOnMapChange observer)
        {
            if(_mapChangeObservers == null)
                _mapChangeObservers = new List<IOnMapChange>();
            if(_mapChangeObservers.Contains(observer) == true)
                return false;
            _mapChangeObservers.Add(observer);
            return true;
        }

        public void UnregisterMapChangeObserver(IOnMapChange observer)
        {
            if(_mapChangeObservers != null)
                _mapChangeObservers.Remove(observer);
        }

        public void Pause()
        {
            if(_isPaused == true)
                return;
            OnPause();
            _isPaused = true;
        }

        public void Resume()
        {
            if(_isPaused == false)
                return;
            OnResume();
            _isPaused = false;
        }

        //更新小地图中图标的位置
        public void UpdateMinimapObject()
        {
            if (_minimapRatio.x < 0) //建筑可能初始化比minimap早，所以没有获取到_minimapRatio
            {
                _minimapRatio = UiMiniMapCtrl.Instance.GetMinimapByMapId(_mapId).gs_warFieldPanel.gs_warFieldMiniMap.gs_ratio;
                _minimapHideOffset = UiMiniMapCtrl.Instance.GetMinimapByMapId(_mapId).gs_warFieldPanel.gs_warFieldMiniMap.gs_hideOffset;
            }

            Vector2 realOffset = (Vector2)_transform.position - _mapPassableBase;
            _miniMapObjTransform.anchoredPosition = realOffset * _minimapRatio;
        }

        //小地图隐藏时，需要同样隐藏小地图中的图标
        public void HideMinimapObject()
        {
            Vector2 realOffset = (Vector2)_transform.position - _mapPassableBase;
            _miniMapObjTransform.anchoredPosition = realOffset * _minimapRatio + _minimapHideOffset;
        }

        //ITask callbacks
        public virtual void RunNormalTask(float deltaTime) { }
        public virtual void RunFixTask(float deltaTime) { }
#endregion

#region private functions

        protected virtual void CreateWarId(){ }
        protected virtual void OnPause() { }
        protected virtual void OnResume() { }

        //修改minimap中的图标
        protected virtual void ChangeMiniMap(int from, int to)
        {
            if (_needBindMiniMap == false)
                return;
            if (from != WE.InvalidMapIndex && from != to)
                UiMiniMapCtrl.Instance.GetMinimapByMapId(_mapId).gs_warFieldPanel.gs_warFieldMiniMap.NotifyRemovedGameObj(this); //从老地图中删除

            if (_isMiniMapBind == false) //初始化地图
            {
                if(UICtrl.Instance.gs_beInited == false) //在地图初始化时创建的建筑无法与小地图绑定，需要start阶段统一重新调用ChangeMapId
                    return;
                var miniMap = UiMiniMapCtrl.Instance.GetMinimapByMapId(_mapId);
                miniMap.gs_warFieldPanel.gs_warFieldMiniMap.NotifyAddedGameObj(this); //加入新地图
                _minimapRatio = UiMiniMapCtrl.Instance.GetMinimapByMapId(_mapId).gs_warFieldPanel.gs_warFieldMiniMap.gs_ratio;
                _minimapHideOffset = UiMiniMapCtrl.Instance.GetMinimapByMapId(_mapId).gs_warFieldPanel.gs_warFieldMiniMap.gs_hideOffset;
                BindToMiniMapObj(); //与minimap中的图标绑定
                HideMinimapObject(); //隐藏
                return;
            }
            else
            {
                var miniMap = UiMiniMapCtrl.Instance.GetMinimapByMapId(_mapId);
                miniMap.gs_warFieldPanel.gs_warFieldMiniMap.NotifyAddedGameObj(this); //加入新地图
                _minimapRatio = UiMiniMapCtrl.Instance.GetMinimapByMapId(_mapId).gs_warFieldPanel.gs_warFieldMiniMap.gs_ratio;
                _minimapHideOffset = UiMiniMapCtrl.Instance.GetMinimapByMapId(_mapId).gs_warFieldPanel.gs_warFieldMiniMap.gs_hideOffset;
            }
        }

        //绑定minimap中的对象
        //soldier和building 初始化的时候调用
        protected virtual void BindToMiniMapObj()
        {
            if (_needBindMiniMap == false)
                return;
            if (_isMiniMapBind == true)
                return;
            var miniMapObj = UiMiniMapCtrl.Instance.GetMiniPrefabObjFromMemPool();
            if (miniMapObj == null)
            {
                GameLogger.LogError($"{gameObject.name} can not band to a new mini map object");
                return;
            }
            if (_miniMapObj != null)
            {
                GameLogger.LogError($"{gameObject.name} can not band to a new mini map object before release the old one");
            }
            _miniMapObj = miniMapObj;
            _miniMapObjTransform = miniMapObj.GetComponent<RectTransform>();
            _miniMapObj.SetActive(true);
            _miniMapObj.name = gameObject.name;
            _isMiniMapBind = true;
            return;
        }

        protected virtual void UnbindMiniMapObj()
        {
            if (_needBindMiniMap == false)
                return;
            if (_isMiniMapBind == false)
                return;
            if (_miniMapObj == null)
            {
                GameLogger.LogError($"{gameObject.name} can not unbind with null mini map object");
                return;
            }
            UiMiniMapCtrl.Instance.ReleasePrefabObjToMemPool(_miniMapObj); //针对portal，cave这种特殊的，因为它们不会被摧毁所以没有处理
            _miniMapObj.SetActive(false);
            _miniMapObjTransform.position = new Vector3(-10000, -10000, 0);
            _miniMapObj = null;
            _miniMapObjTransform = null;
            UiMiniMapCtrl.Instance.GetMinimapByMapId(_mapId).gs_warFieldPanel.gs_warFieldMiniMap.NotifyRemovedGameObj(this);
            _isMiniMapBind = false;
            return;
        }

        //entity发生变化(状态变化,位置变换) 通知spatial grid
        protected virtual void UpdateEntityData()
        {
            SpatialGridManager.Instance.UpdateEntityData(_gridIndex, _entityData);
        }

        //生成enetitydata中的p_spec字段
        protected virtual byte GetSpatialEntitySpecData()
        {
            return 0;
        }
#endregion
    }
}

