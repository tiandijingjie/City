using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using WarField.Anim;

namespace WarField
{
    using FD = FarmerDefines;
    using WE = WarFieldElements;
    using RD = WarResDefine;

    public class Farmer : WarEleParent, IAnimInfo, ICollectResListener
    {
#region public parameters

#endregion

#region private parameters

        private FarmerConf _fConf;

        //animation
        // 在texture2dArray中寻址所需的当前播放的帧动画
        private int _currentDirIndex = 0; //帧动画的方向
        private FD.FarmAnimType _curAnimType; //the animation is playing now
        private AnimRenderProxy _animProxy;

        //searchers
        private SearchArea _enemySearcher; //用于查找周围是不是有敌人
        private SearchShapeDef _enemySearchShape; //查找rival的范围
        private SearchResClosest _resSearcher; //查找最近的资源

        //collect target
        private int _targetResId;
        private Vector2 _targetPos;
        private bool _notTargetFound; //找不到新的资源了

        private float _hideTime;

        private FD.FarmerStatus _curStatue, _prvStatue;
        private bool _hasEnemyInRange;

        private int _carriedCnt;
        private int _carriedValue;
        protected bool _isBusy;

        // move job
        private Vector2 _desiredMoveDir;           // 每帧由 A* 路径计算，由 FarmerMoveJob 读取
        private List<Vector2> _aStarWaypoints;     // 当前 A* 路径的路径点列表
        private int _currentWaypointIndex;         // 当前正在跟随的路径点索引
        private bool _pathRequestPending;          // 异步 A* 请求还未返回
#endregion

#region private parameters' get set

        public FD.FarmerStatus gs_curStatus
        {
            get { return _curStatue; }
        }

        public Vector2 gs_desiredMoveDir
        {
            get { return _desiredMoveDir; }
        }

        public Vector2 gs_targetPos
        {
            get { return _targetPos; }
        }

        public float gs_moveSpeed
        {
            get { return _fConf != null ? _fConf.p_moveSpeed : 0f; }
        }

        public float gs_mass
        {
            get { return _fConf != null ? _fConf.p_mass : 1f; }
        }

#endregion

#region Unity callbacks

        protected override void Awake()
        {
            base.Awake();
            _needBindMiniMap = false;
            _warEleType = WE.WarEleType.FARMER;

            _enemySearcher = new SearchArea(-1, OnEnemyInRange, GetEnemySearchShape, this, -1);
            SearchConditionUtil.AddEnemySoldierAndBuildingConditions(_enemySearcher);
            _enemySearchShape = new SearchShapeDef();
            _enemySearchShape.p_shapeType = SearchDefines.SearchShapeType.CIRCLE;
            _enemySearchShape.p_radius = _fConf.p_enemyDetectRange;
            _enemySearchShape.p_radiusSq = _enemySearchShape.p_radius * _enemySearchShape.p_radius;

            _resSearcher = new SearchResClosest((Vector2)_transform.position, -1, this, (byte)RD.ResTypes.OCULARSTONE);
            _resSearcher.p_isEnabled = false;
        }

#endregion

#region public functions

        public bool InitFarmer(FarmerConf farmerConf, byte mapId)
        {
            _entitySubType = WE.EncodeEntitySubType((byte)WE.RaceType.Human, 0, 0, 0);
            base.InitWarEle(mapId);
            _fConf = farmerConf;

            _entityData.p_position = (Vector2)_transform.position;
            _transform.position = new Vector3(_transform.position.x, _transform.position.y, WarFieldUtil.GetZByY(_transform.position.y, _mapPassableBase.y));
            _isBusy = false;
            _hasEnemyInRange = false;
            _targetResId = -1;
            _notTargetFound = false;
            _desiredMoveDir = Vector2.zero;
            _aStarWaypoints = null;
            _currentWaypointIndex = 0;
            _pathRequestPending = false;

            _enemySearcher.p_mapId = mapId;
            _enemySearcher.p_isEnabled = false;

            _resSearcher.p_mapId = mapId;
            _resSearcher.p_isEnabled = false;

            SearchManager.Instance.RegisterSearch(_enemySearcher);
            SearchManager.Instance.RegisterResSearch(_resSearcher);
            ChangeStatus(FD.FarmerStatus.INIT);
            return true;
        }

        public override void RunFixTask(float deltaTime)
        {
            if (_beInited == false)
                return;

            if(_isPaused == true)
                return;

            if(_isBusy == true)
                return;
            _isBusy = true;

            _desiredMoveDir = Vector2.zero; // 每帧默认静止，TORES 状态才赋值

            if (_curStatue == FD.FarmerStatus.INIT)
            {
                ChangeStatus(FD.FarmerStatus.INHOME);
            }
            else if (_curStatue == FD.FarmerStatus.INHOME)
            {
                if (_prvStatue != _curStatue)
                {
                    _resSearcher.p_isEnabled = true; //开始查找pickable res
                    _enemySearcher.p_isEnabled = false;
                    _animProxy.ChangeAnimVisible(false); //处于建筑内,看不见了
                    _targetResId = -1;

                    //交资源
                    _notTargetFound = false;
                    if (_carriedValue > 0)
                    {
                        WarResCtrl.Instance.AddRes(WarResDefine.ResTypes.OCULARSTONE, _carriedValue);
                        _carriedValue = 0;
                        _carriedCnt = 0;
                    }
                }

                if (_targetResId >= 0)
                {
                    if(_enemySearcher.p_isEnabled == false)
                        _enemySearcher.p_isEnabled = true;
                    else if (_hasEnemyInRange == false) //确保附近没有敌人才能出发
                    {
                        ChangeStatus(FD.FarmerStatus.TORES);
                        _enemySearcher.p_isEnabled = true;
                        _animProxy.ChangeAnimVisible(true); //开始播放动画了
                    }
                }
            }
            else if (_curStatue == FD.FarmerStatus.TORES)
            {
                if(_hasEnemyInRange == true) //dectect enemy in range, start hide
                    ChangeStatus(FD.FarmerStatus.DIGHIDE);

                if(_targetResId < 0)  // target res disappeared
                    ChangeStatus(FD.FarmerStatus.WAITRES);

                if (Vector2.Distance(_transform.position, _targetPos) < 0.1f)
                    ChangeStatus(FD.FarmerStatus.COLLECTRES);

                // 仍在 TORES 状态时，沿 A* 路径更新期望方向供 MoveJob 读取
                if (_curStatue == FD.FarmerStatus.TORES)
                    UpdateAStarDesiredDir();
            }
            else if (_curStatue == FD.FarmerStatus.COLLECTRES)
            {
                if (_animProxy?.gs_hasStateAnim[(int)FD.FarmAnimType.COLLECT] == true)
                {
                    //wait animation finish
                }
                else
                    OnCollectFinish();
            }
            else if (_curStatue == FD.FarmerStatus.WAITRES)
            {
                if (_prvStatue != _curStatue)
                {
                    _resSearcher.p_isEnabled = true;
                    _prvStatue = _curStatue;
                }

                if(_hasEnemyInRange == true) //dectect enemy in range, start hide
                    ChangeStatus(FD.FarmerStatus.DIGHIDE);

                if(_notTargetFound == true)
                    ChangeStatus(FD.FarmerStatus.GOBACK);

                if(_targetResId >= 0)
                    ChangeStatus(FD.FarmerStatus.TORES);
            }
            else if (_curStatue == FD.FarmerStatus.GOBACK)
            {
                if(_hasEnemyInRange == true) //dectect enemy in range, start hide
                    ChangeStatus(FD.FarmerStatus.DIGHIDE);
            }
            else if (_curStatue == FD.FarmerStatus.DIGHIDE)
            {
                if (_animProxy?.gs_hasStateAnim[(int)FD.FarmAnimType.DIGHIDE] == true)
                {
                    //wait animation finish
                }
                else
                    OnDigHideFinish();

            }
            else if (_curStatue == FD.FarmerStatus.HIDE)
            {
                if (_prvStatue != _curStatue)
                {
                    _prvStatue = _curStatue;
                    _hideTime = 0;
                }

                if (_hasEnemyInRange == false)
                {
                    _hideTime += deltaTime;
                    if (_hideTime > _fConf.p_hideMinTime)
                        ChangeStatus(FD.FarmerStatus.HIDEDETECT);
                }
                else
                    _hideTime = 0;
            }
            else if (_curStatue == FD.FarmerStatus.HIDEDETECT)
            {
                if (_prvStatue != _curStatue)
                {
                    _prvStatue = _curStatue;
                    _hideTime = 0;
                }
                if(_hasEnemyInRange == true)
                    ChangeStatus(FD.FarmerStatus.HIDE);
                else
                {
                    _hideTime += deltaTime;
                    if (_hideTime > _fConf.p_hideDetectTime)
                    {
                        if(_notTargetFound == true || _carriedCnt >= _fConf.p_carryCapacity)
                            ChangeStatus(FD.FarmerStatus.GOBACK);
                        else
                        {
                            if (_targetResId >= 0)
                                ChangeStatus(FD.FarmerStatus.TORES);
                            else
                                ChangeStatus(FD.FarmerStatus.WAITRES);
                        }
                    }
                }
            }
            _isBusy = false;
        }

        public override void ChangeMapId(byte mapId)
        {
            base.ChangeMapId(mapId);
            _transform.position = new Vector3(_transform.position.x, _transform.position.y, WarFieldUtil.GetZByY(_transform.position.y, _mapPassableBase.y));
        }

        //IAnimInfo
        public uint IAnimInfo_GetEleAnimId()
        {
            return (uint)AnimDefines.AnimEntityType.FARMER | (uint)WE.RaceType.Human << 2 ;
        }

        public Dictionary<string, uint> IAnimInfo_GetStateId()
        {
            Dictionary<string, uint> ret = new Dictionary<string, uint>();
            ret.Add("Idle", (uint)FD.FarmAnimType.IDLE);
            ret.Add("EmptyMove", (uint)FD.FarmAnimType.ENMTYMOVE);
            ret.Add("PackedMove", (uint)FD.FarmAnimType.PACKEDMOVE);
            ret.Add("Collect", (uint)FD.FarmAnimType.COLLECT);
            ret.Add("DigHide", (uint)FD.FarmAnimType.DIGHIDE);
            ret.Add("Hide", (uint)FD.FarmAnimType.HIDE);
            ret.Add("HideDetect", (uint)FD.FarmAnimType.HIDEDETECT);
            return ret;
        }

        // animation event callback
        public void IAnimInfo_OnAnimEvent(int stateId)
        {
            switch (stateId)
            {
                case (int)FD.FarmAnimType.COLLECT:
                    OnCollectFinish();
                    break;
                case (int)FD.FarmAnimType.DIGHIDE:
                    OnDigHideFinish();
                    break;
                default:
                    break;
            }
        }

        //ICollectResListener
        public bool ICollectResListener_OnResourceFound(int poolIndex, Vector2 pos)
        {
            if (_targetResId != -1)
                return false;
            _targetResId = poolIndex;
            _targetPos = pos;
            _resSearcher.p_isEnabled = false; //stop search new res

            // 在 INHOME 或 WAITRES 阶段提前发起异步 A* 寻路，路径结果在进入 TORES 前可能已就绪
            if (_curStatue == FD.FarmerStatus.INHOME || _curStatue == FD.FarmerStatus.WAITRES)
                RequestAStarToTarget();

            return true;
        }

        public void ICollectResListener_OnResourceNotFound()
        {
            if (_curStatue != FD.FarmerStatus.INHOME)
            {
                _notTargetFound = true;
                _resSearcher.p_isEnabled = false; //stop search new res,  until change to status INHOME
            }
        }

        //target res disappeared, maybe timeout
        public void ICollectResListener_OnResourceDisappeared(int poolIndex)
        {
            _targetResId = -1;
            _aStarWaypoints = null;
            _currentWaypointIndex = 0;
            _pathRequestPending = false;
        }

        // 每帧由 FarmerCtrl 在 MoveJob 完成后调用，将 Transform 同步回 SpatialGrid
        public void SyncSpatialEntity()
        {
            if (_gridIndex < 0)
                return;
            _entityData.p_position = (Vector2)_transform.position;
            _entityData.p_mapId = _mapId;
            _entityData.p_spec = GetSpatialEntitySpecData();
            UpdateEntityData();
        }
#endregion

#region private functions

        private void OnCollectFinish()
        {
            int value = WarResCtrl.Instance.PickUpRes(_targetResId);
            if (value > 0)
            {
                _carriedCnt++;
                _carriedValue += value;
            }

            _targetResId = -1;

            if(_carriedCnt >= _fConf.p_carryCapacity)
                ChangeStatus(FD.FarmerStatus.GOBACK);
            else
                ChangeStatus(FD.FarmerStatus.WAITRES);
        }

        private void OnDigHideFinish()
        {
            ChangeStatus(FD.FarmerStatus.HIDE);
        }

        //enemy searcher
        private SearchShapeDef GetEnemySearchShape()
        {
            _enemySearchShape.p_centerOrStartPos = (Vector2)_transform.position;
            return _enemySearchShape;
        }

        //范围内发现了敌人
        private void OnEnemyInRange(List<IGridNode> targets)
        {
            _hasEnemyInRange = true;
        }

        private void ChangeStatus(FD.FarmerStatus value)
        {
            _prvStatue = _curStatue;
            _curStatue = value;

            // 进入 INHOME 或 GOBACK 时清除 A* 路径状态
            if (value == FD.FarmerStatus.INHOME || value == FD.FarmerStatus.GOBACK)
            {
                _aStarWaypoints = null;
                _currentWaypointIndex = 0;
                _pathRequestPending = false;
                _desiredMoveDir = Vector2.zero;
            }
        }

        // 沿 A* 路径点更新 _desiredMoveDir
        private void UpdateAStarDesiredDir()
        {
            Vector2 currentPos = _transform.position;

            if (_aStarWaypoints == null || _aStarWaypoints.Count == 0)
            {
                // 没有路径时先尝试请求
                if (!_pathRequestPending && _targetResId >= 0)
                    RequestAStarToTarget();

                if (_pathRequestPending)
                {
                    // A* 请求挂起中：保持静止，避免先冲向障碍物再被迫折返造成顿挫
                    _desiredMoveDir = Vector2.zero;
                    return;
                }

                // A* 已确定失败（回调返回空路径）时才直线兜底
                Vector2 toTarget = _targetPos - currentPos;
                _desiredMoveDir = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : Vector2.zero;
                return;
            }

            // 推进已到达的路径点
            while (_currentWaypointIndex < _aStarWaypoints.Count &&
                   (_aStarWaypoints[_currentWaypointIndex] - currentPos).sqrMagnitude < 0.1f)
            {
                _currentWaypointIndex++;
            }

            if (_currentWaypointIndex < _aStarWaypoints.Count)
            {
                Vector2 offset = _aStarWaypoints[_currentWaypointIndex] - currentPos;
                _desiredMoveDir = offset.sqrMagnitude > 0.001f ? offset.normalized : Vector2.zero;
            }
            else
            {
                // 所有路径点已走完，直接朝终点
                Vector2 offset = _targetPos - currentPos;
                _desiredMoveDir = offset.sqrMagnitude > 0.001f ? offset.normalized : Vector2.zero;
            }
        }

        // 提交异步 A* 寻路请求
        private void RequestAStarToTarget()
        {
            if (_targetResId < 0)
                return;
            var pathMap = WarMapCtrl.Instance.GetPathFinderMapByIndex(_mapId);
            if (pathMap == null)
                return;
            _pathRequestPending = true;
            pathMap.RequestPathAStar(_transform.position, _targetPos, OnAStarPathReceived);
        }

        // 异步 A* 寻路结果回调（主线程调用）
        private void OnAStarPathReceived(List<Vector2> path)
        {
            _pathRequestPending = false;
            if (_targetResId < 0) // 目标在路径计算期间消失了，丢弃结果
                return;
            if (path != null && path.Count > 0)
            {
                _aStarWaypoints = path;
                _currentWaypointIndex = 0;
            }
            else
            {
                _aStarWaypoints = null; // 路径计算失败，回退到直线引导
            }
        }
#endregion
    }
}
