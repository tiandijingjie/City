using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace WarField
{
    using WE = WarFieldElements;

    public struct SearchCmd
    {
        public FixedList64Bytes<int> p_excludeGridIndices;  //对于area的查找目前只支持exclude一个
        public SearchShapeDef p_shape;
        public FixedList128Bytes<SearchCondition> p_conditions; // 最多支持放 10个不同条件
    }

    // 用于安全传递 Job 数据和回调引用的上下文类
    public class SearchJobContext
    {
        public NativeList<SearchCmd> p_closestCmds;
        public NativeList<int> p_closestResults;
        public NativeList<SearchCondition> p_closestMatchConds;
        public NativeList<float> p_closestDistances;
        public List<SearchClosest> p_closestSearchersRef = new List<SearchClosest>();

        public NativeList<SearchCmd> p_areaCmds;
        // Key: Area Job 的 cmd 下标；Value: x=网格实体下标, y=该命中对应的 p_targetEleType（静/动两套 index 空间不同，必须随条件带上类型）
        public NativeParallelMultiHashMap<int, int2> p_areaResults;
        public List<SearchArea> p_areaSearchersRef = new List<SearchArea>();

        public SearchJobContext()
        {
            // 在构造函数中一次性分配持久化内存
            p_closestCmds = new NativeList<SearchCmd>(64, Allocator.Persistent);
            p_closestResults = new NativeList<int>(64, Allocator.Persistent);
            p_closestMatchConds = new NativeList<SearchCondition>(64, Allocator.Persistent);
            p_closestDistances = new NativeList<float>(64, Allocator.Persistent);
            p_areaCmds = new NativeList<SearchCmd>(64, Allocator.Persistent);
            p_areaResults = new NativeParallelMultiHashMap<int, int2>(256, Allocator.Persistent);
        }

        public void DisposeNativeArrays()
        {
            if (p_closestCmds.IsCreated)
                p_closestCmds.Dispose();
            if (p_closestResults.IsCreated)
                p_closestResults.Dispose();
            if (p_closestMatchConds.IsCreated)
                p_closestMatchConds.Dispose();
            if (p_closestDistances.IsCreated)
                p_closestDistances.Dispose();
            if (p_areaCmds.IsCreated)
                p_areaCmds.Dispose();
            if (p_areaResults.IsCreated)
                p_areaResults.Dispose();
        }

        public void ClearRefsAndData()
        {
            p_closestSearchersRef.Clear();
            p_areaSearchersRef.Clear();
            // 仅仅清空计数器，不释放内存！
            p_closestCmds.Clear();
            p_closestResults.Clear();
            p_closestMatchConds.Clear();
            p_closestDistances.Clear();

            p_areaCmds.Clear();
            p_areaResults.Clear();
        }
    }

    //所有需要查询Spatial entity的操作通通在这里完成
    public class SearchManager : MonoBehaviour, ITask
    {
#region public parameters
        public static SearchManager Instance;

#endregion

#region private parameters

        [SerializeField] private float _searchTickRate = 0.1f; // 两次查询之间的时间间隔

        private Dictionary<int, List<SearchClosest>> _activeClosestSearchers; //mapid -> searchList
        private Dictionary<int, List<SearchArea>> _activeAreaSearchers; //mapid -> searchList
        private List<SearchResClosest> _pendingResSearchers;  //本帧新收到的资源查找请求
        private List<SearchResClosest> _queuedResSearchers;   //已提交异步重建，等待下帧结果返回

        private List<SearchJobContext> _activeJobContexts = new List<SearchJobContext>();  //每个map的search是一个SearchJobContext, _activeJobContexts中存放所有map的search
        private Queue<SearchJobContext> _contextPool = new Queue<SearchJobContext>();

        private readonly List<SearchClosest> _mapClosests = new List<SearchClosest>(1024);
        private readonly List<SearchArea>  _mapAreas    = new List<SearchArea>(64);
        private readonly List<IGridNode> _syncAreas = new List<IGridNode>(64);
        private readonly List<IGridNode> _dispatchList = new List<IGridNode>(32);

        private JobHandle _combinedJobHandle;
        private JobHandle _resSearchJobHandle;
        private bool _canScheduleJob;

        private bool _beInited;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks
        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _activeClosestSearchers = new Dictionary<int, List<SearchClosest>>();
            _activeAreaSearchers = new Dictionary<int, List<SearchArea>>();
            _pendingResSearchers = new List<SearchResClosest>(64);
            _queuedResSearchers  = new List<SearchResClosest>(64);
            _beInited = false;
        }

        private void OnDestroy()
        {
            _combinedJobHandle.Complete();
            _resSearchJobHandle.Complete();
            foreach (var ctx in _activeJobContexts)
                ctx.DisposeNativeArrays();
            foreach (var ctx in _contextPool)
                ctx.DisposeNativeArrays();
            _activeJobContexts.Clear();
            _contextPool.Clear();
        }

#endregion

#region public functions

        public bool InitSearchManager()
        {
            if (_beInited == true)
                return false;

            _canScheduleJob = false;
            WarFieldGameManager.Instance.RegisterTask(this, WarFieldElements.TaskType.NORMAL);
            WarFieldGameManager.Instance.ActiveTask(this, WarFieldElements.TaskType.NORMAL, _searchTickRate);
            _beInited = true;
            return true;
        }

        // 注册查找器。如果 duration == 0，会立刻在主线程同步结算并触发回调。
        public void RegisterSearch(SearchBase searcher)
        {
            if (searcher == null || searcher.p_conditions.Count == 0 || searcher.p_getShapeCall == null)
                return;

            if (searcher.p_duration == 0) //立刻同步执行
            {
                DoSyncSearch(searcher);
            }
            else
            {
                // 加入异步轮询队列
                if (searcher is SearchClosest closest)
                {
                    if(_activeClosestSearchers.ContainsKey(closest.p_mapId) == false)
                        _activeClosestSearchers.Add(closest.p_mapId, new List<SearchClosest>());
                    _activeClosestSearchers[closest.p_mapId].Add(closest);
                }
                else if (searcher is SearchArea area)
                {
                    if(_activeAreaSearchers.ContainsKey(area.p_mapId) == false)
                        _activeAreaSearchers.Add(area.p_mapId, new List<SearchArea>());
                    _activeAreaSearchers[area.p_mapId].Add(area);
                }
            }
        }

        public void UnregisterSearch(SearchBase searcher)
        {
            if (searcher is SearchClosest closest)
            {
                if(_activeClosestSearchers.ContainsKey(closest.p_mapId) == false)
                    _activeClosestSearchers.Add(closest.p_mapId, new List<SearchClosest>());
                _activeClosestSearchers[closest.p_mapId].Remove(closest);
            }
            else if (searcher is SearchArea area)
            {
                if(_activeAreaSearchers.ContainsKey(area.p_mapId) == false)
                    _activeAreaSearchers.Add(area.p_mapId, new List<SearchArea>());
                _activeAreaSearchers[area.p_mapId].Remove(area);
            }
        }

        //每_searchTickRate才调用一次
        public void RunNormalTask(float deltaTime)
        {
            _canScheduleJob = true;
        }

        public void RunFixTask(float deltaTime)
        {
            throw new System.NotImplementedException();
        }

        // 提交一次可拾取资源的最近点查找（无范围限制，在 FinishSearchJobs 中统一处理）
        public void RegisterResSearch(SearchResClosest searcher)
        {
            if (searcher == null || searcher.p_listener == null)
                return;
            _pendingResSearchers.Add(searcher);
        }

        // 完成并分发上一帧调度的查找 Job。写入 NativeArray 前必须先调用。
        public void FinishSearchJobs()
        {
            _combinedJobHandle.Complete();
            _resSearchJobHandle.Complete();
            DispatchJobResults();
            ProcessResSearches();
        }

        // 在实体数据写入完成后再调度新的查找 Job，避免与 ClosestSearchJob 读写冲突。
        public void StartSearchJobs(JobHandle dependence)
        {
            if (_canScheduleJob == true) //最短_searchTickRate执行一次
            {
                PrepareAndScheduleJobs(dependence);
                _canScheduleJob = false;
            }

            // 资源查找：每帧检查，有请求时按需调度 res grid 重建（与 search tick 无关）
            if (_pendingResSearchers.Count > 0)
            {
                // 仅当数据变脏时才真正调度 BuildResGridJob，否则直接返回依赖句柄（跳过重建）
                _resSearchJobHandle = SpatialGridManager.Instance.ScheduleResGridRebuild(WE.OnGroundMapIndex, dependence);

                // 将本帧请求移入等待队列，下帧 FinishSearchJobs 完成后统一处理
                (_pendingResSearchers, _queuedResSearchers) = (_queuedResSearchers, _pendingResSearchers);
                _pendingResSearchers.Clear();
            }
        }

        // 主线程任意时刻写入网格实体数据前的安全屏障（如状态切换、复活等路径）。
        public void CompletePendingSearchJobs()
        {
            _combinedJobHandle.Complete();
            _resSearchJobHandle.Complete();
        }

        public void CompleteResSearchJobs()
        {
            _resSearchJobHandle.Complete();
        }

#endregion

#region private functions
        // 处理上一帧提交的资源查找请求
        // BuildResGridJob 已作为 _combinedJobHandle 的依赖完成，p_resGrid 可安全读取
        // 使用 ResQueryHelper 扩展环搜索，充分利用 p_resGrid 的空间索引
        private void ProcessResSearches()
        {
            if (_queuedResSearchers.Count == 0)
                return;
            if (ResourceGrid.Instance == null || !ResourceGrid.Instance.gs_resPool.IsCreated)
            {
                for (int i = 0; i < _queuedResSearchers.Count; i++)
                    _queuedResSearchers[i].p_listener?.ICollectResListener_OnResourceNotFound();
                _queuedResSearchers.Clear();
                return;
            }

            // _resSearchJobHandle.Complete() 已在 FinishSearchJobs 中完成，
            // BuildResGridJob 已结束，p_resGrid 此时可安全读取
            ResQueryHelper queryHelper = SpatialGridManager.Instance.GetResQueryHelper(WE.OnGroundMapIndex);

            for (int i = 0; i < _queuedResSearchers.Count; i++)
            {
                SearchResClosest searcher = _queuedResSearchers[i];
                if (searcher.p_isEnabled == false || searcher.p_listener == null)
                    continue;

                // 扩展环搜索，利用 p_resGrid 空间索引，支持 p_resType 过滤
                int bestIndex = queryHelper.FindClosest(searcher.p_searchPos, searcher.p_resType);

                if (bestIndex != -1)
                {
                    ResEntityData best = queryHelper.p_resPool[bestIndex];
                    ICollectResListener listener = searcher.p_listener;
                    if (listener.ICollectResListener_OnResourceFound(bestIndex, new UnityEngine.Vector2(best.p_position.x, best.p_position.y)) == true) //返回true 才能锁定资源
                        WarResCtrl.Instance.LockPickableRes(bestIndex, listener);
                }
                else
                {
                    searcher.p_listener.ICollectResListener_OnResourceNotFound();
                }
            }

            _queuedResSearchers.Clear();
        }

        // 同步查找(主线程直接查)
        private void DoSyncSearch(SearchBase searcher)
        {
            // BuildSpatialHashJob 在上一帧 LateUpdate 异步写入；动画/战斗回调可能在下一帧 Update 触发同步查询。
            SpatialGridManager.Instance?.CompletePendingRebuildJob();  //不需要等待流场任务
            CompletePendingSearchJobs();

            SearchShapeDef shape = searcher.p_getShapeCall.Invoke();
            int excludeIdx = searcher.p_ownerNode != null ? searcher.p_ownerNode.gs_gridIndex : -1;

            if (searcher is SearchClosest closestSearcher)
            {
                int bestIndex = -1;
                float minDistanceSq = float.MaxValue;
                SearchCondition bestMatchCondition = default;

                FixedList64Bytes<int> excludeList = new FixedList64Bytes<int>();
                if (searcher.p_getExcludeCall != null) // 如果有排除列表回调，直接调用获取（会包含自身和黑名单）
                    searcher.p_getExcludeCall.Invoke(ref excludeList);
                else if (searcher.p_ownerNode != null)  // 否则退化为只排除自己
                    excludeList.Add(searcher.p_ownerNode.gs_gridIndex);

                foreach (var cond in searcher.p_conditions)
                {
                    var queryHelper = SpatialGridManager.Instance.GetQueryHelper(searcher.p_mapId, cond.p_targetEleType);
                    int foundIndex = DoClosestShapeQuery(ref queryHelper, shape, cond, ref excludeList);
                    if (foundIndex != -1)
                    {
                        GridEntityData targetData = queryHelper.p_entities[foundIndex];
                        float distSq = math.distancesq(shape.p_centerOrStartPos, targetData.p_position);
                        if (distSq < minDistanceSq)
                        {
                            minDistanceSq = distSq;
                            bestIndex = foundIndex;
                            bestMatchCondition = cond;
                        }
                    }
                }

                if (bestIndex != -1)
                {
                    IGridNode node = SpatialGridManager.Instance.GetGridNode(bestMatchCondition.p_targetEleType, bestIndex);
                    closestSearcher.TriggerCallback(node, math.sqrt(minDistanceSq));
                }
            }
            else if (searcher is SearchArea areaSearcher)
            {
                _syncAreas.Clear();
                FixedList512Bytes<int> foundIndices = new FixedList512Bytes<int>();

                foreach (var cond in searcher.p_conditions)
                {
                    var queryHelper = SpatialGridManager.Instance.GetQueryHelper(searcher.p_mapId, cond.p_targetEleType);
                    DoAreaShapeQuery(ref queryHelper, shape, cond, excludeIdx, ref foundIndices);

                    for (int i = 0; i < foundIndices.Length; i++)
                    {
                        IGridNode node = SpatialGridManager.Instance.GetGridNode(cond.p_targetEleType, foundIndices[i]);
                        if (node != null)
                            _syncAreas.Add(node);
                    }
                    foundIndices.Clear();
                }

                areaSearcher.TriggerCallback(_syncAreas);
            }
        }

        // 异步打包与 Job 分发
        private void PrepareAndScheduleJobs(JobHandle dependence)
        {
            NativeList<JobHandle> handles = new NativeList<JobHandle>(Allocator.Temp);

            var maps = WarMapCtrl.Instance.gs_mapDict;
            foreach (var tmp in maps)  //按照map添加查询任务
            {
                if (tmp.Value.gs_isOpened == false)
                    continue;

                int mapId = tmp.Key;

                _mapClosests.Clear();
                _mapAreas.Clear();

                if (_activeClosestSearchers.TryGetValue(mapId, out var closestList))
                {
                    for (int i = closestList.Count - 1; i >= 0; i--)
                    {
                        if (!UpdateSearcherDuration(closestList[i], _searchTickRate))
                            closestList.RemoveAt(i);
                        else if (closestList[i].p_isEnabled == true && closestList[i].p_conditions.Count > 0) //查询必须要有查询条件
                            _mapClosests.Add(closestList[i]);
                    }
                }

                if (_activeAreaSearchers.TryGetValue(mapId, out var areaList))
                {
                    for (int i = areaList.Count - 1; i >= 0; i--)
                    {
                        if (!UpdateSearcherDuration(areaList[i], _searchTickRate))
                            areaList.RemoveAt(i);
                        else if (areaList[i].p_isEnabled == true && areaList[i].p_conditions.Count > 0) //查询必须要有查询条件
                            _mapAreas.Add(areaList[i]);
                    }
                }

                if (_mapClosests.Count == 0 && _mapAreas.Count == 0)
                    continue;

                // 从对象池取出一个上下文来保存这个地图的任务
                SearchJobContext ctx = _contextPool.Count > 0 ? _contextPool.Dequeue() : new SearchJobContext();
                var staticQueryHelper = SpatialGridManager.Instance.GetQueryHelper(mapId, (int)WE.WarEleType.BUILDING);
                var dynamicQueryHelper = SpatialGridManager.Instance.GetQueryHelper(mapId, (int)WE.WarEleType.SOLDIER);

                // 查找最近的entity Job
                if (_mapClosests.Count > 0)
                {
                    // 不要 new NativeArray，直接把 List 的容量撑大以供 Job 写入
                    ctx.p_closestResults.ResizeUninitialized(_mapClosests.Count);
                    ctx.p_closestMatchConds.ResizeUninitialized(_mapClosests.Count);
                    ctx.p_closestDistances.ResizeUninitialized(_mapClosests.Count);

                    for (int i = 0; i < _mapClosests.Count; i++)
                    {
                        ctx.p_closestCmds.Add(PackToCmd(_mapClosests[i]));
                        ctx.p_closestSearchersRef.Add(_mapClosests[i]);
                    }

                    var closestJob = new ClosestSearchJob
                    {
                        p_cmds = ctx.p_closestCmds.AsArray(), // 转为 Array 传给 Job
                        p_staticQueryHelper = staticQueryHelper,
                        p_dynamicQueryHelper = dynamicQueryHelper,
                        p_resultIndices = ctx.p_closestResults.AsArray(),
                        p_resultConditions = ctx.p_closestMatchConds.AsArray(),
                        p_resultDistances = ctx.p_closestDistances.AsArray()
                    };
                    handles.Add(closestJob.Schedule(_mapClosests.Count, 64, dependence));
                }

                // 范围查找 Job
                if (_mapAreas.Count > 0)
                {
                    for (int i = 0; i < _mapAreas.Count; i++)
                    {
                        ctx.p_areaCmds.Add(PackToCmd(_mapAreas[i]));
                        ctx.p_areaSearchersRef.Add(_mapAreas[i]);
                    }

                    var areaJob = new AreaSearchJob
                    {
                        p_cmds = ctx.p_areaCmds.AsArray(),
                        p_staticQueryHelper = staticQueryHelper,
                        p_dynamicQueryHelper = dynamicQueryHelper,
                        p_results = ctx.p_areaResults.AsParallelWriter()
                    };
                    handles.Add(areaJob.Schedule(_mapAreas.Count, 16, dependence));
                }

                _activeJobContexts.Add(ctx);
            }

            if (handles.Length > 0)
                _combinedJobHandle = JobHandle.CombineDependencies(handles.AsArray());
            handles.Dispose();
        }

        //将jobs查询的结果分发出去
        private void DispatchJobResults()
        {
            // 遍历所有地图的 JobContext
            foreach (var ctx in _activeJobContexts)
            {
                // 分发 Closest
                if (ctx.p_closestCmds.IsCreated)
                {
                    for (int i = 0; i < ctx.p_closestCmds.Length; i++)
                    {
                        int targetIndex = ctx.p_closestResults[i];
                        if (targetIndex != -1)
                        {
                            SearchCondition matchCond = ctx.p_closestMatchConds[i];
                            float distance = ctx.p_closestDistances[i];
                            IGridNode node = SpatialGridManager.Instance.GetGridNode(matchCond.p_targetEleType, targetIndex);

                            // 因为组装时位置是一一对应的，直接拿对应的 searcher 触发回调
                            if (node != null)
                                ctx.p_closestSearchersRef[i].TriggerCallback(node, distance);
                        }
                    }
                }

                // 分发 Area
                if (ctx.p_areaCmds.IsCreated)
                {
                    for (int i = 0; i < ctx.p_areaCmds.Length; i++)
                    {
                        _dispatchList.Clear();

                        if (ctx.p_areaResults.TryGetFirstValue(i, out int2 hit, out var iterator))
                        {
                            do
                            {
                                IGridNode node = SpatialGridManager.Instance.GetGridNode(hit.y, hit.x);
                                if (node != null) //结果没有去重
                                    _dispatchList.Add(node);
                            } while (ctx.p_areaResults.TryGetNextValue(out hit, ref iterator));
                        }

                        if (_dispatchList.Count > 0)
                        {
                            ctx.p_areaSearchersRef[i].TriggerCallback(_dispatchList);
                        }
                    }
                }

                ctx.ClearRefsAndData();
                _contextPool.Enqueue(ctx);
            }

            _activeJobContexts.Clear();
        }

        // return false 表示时间到了
        private bool UpdateSearcherDuration(SearchBase searcher, float delta)
        {
            if (searcher.p_duration == -1)
                return true; // 无限时长
            searcher.p_duration -= delta;
            return searcher.p_duration > 0;
        }

        private SearchCmd PackToCmd(SearchBase searcher)
        {
            SearchCmd cmd = new SearchCmd
            {
                p_excludeGridIndices = new FixedList64Bytes<int>(),
                p_shape = searcher.p_getShapeCall.Invoke(),
                p_conditions = new FixedList128Bytes<SearchCondition>()
            };

            if (searcher.p_getExcludeCall != null) // 如果有排除列表回调，直接调用获取（会包含自身和黑名单）
                searcher.p_getExcludeCall.Invoke(ref cmd.p_excludeGridIndices);
            else if (searcher.p_ownerNode != null)  // 否则退化为只排除自己
                cmd.p_excludeGridIndices.Add(searcher.p_ownerNode.gs_gridIndex);

            int cnt = searcher.p_conditions.Count;
            for (int i = 0; i < cnt; i++)
                cmd.p_conditions.Add(searcher.p_conditions[i]);
            return cmd;
        }

        private static int DoClosestShapeQuery(ref SpatialQueryHelper helper, SearchShapeDef shape, SearchCondition cond, ref FixedList64Bytes<int> excludeList)
        {
            int foundIndex = -1;
            if (shape.p_shapeType == SearchDefines.SearchShapeType.CIRCLE)  //目前支持在圆形区域内查找最近的
                foundIndex = helper.FindClosestEntity(shape.p_centerOrStartPos, shape.p_radius, shape.p_radiusSq, cond.p_targetEleType, cond.p_targetSubType, ref excludeList,
                                cond.p_excludeFlags, cond.p_includeFlags);

            return foundIndex;
        }

        private static void DoAreaShapeQuery(ref SpatialQueryHelper helper, SearchShapeDef shape, SearchCondition cond, int excludeIdx, ref FixedList512Bytes<int> foundIndices)
        {
            if (shape.p_shapeType == SearchDefines.SearchShapeType.CIRCLE)
                helper.FindEntitiesInRange(shape.p_centerOrStartPos, shape.p_radius, shape.p_radiusSq, cond.p_targetEleType, cond.p_targetSubType, excludeIdx,
                    ref foundIndices, cond.p_excludeFlags, cond.p_includeFlags);
            else if (shape.p_shapeType == SearchDefines.SearchShapeType.SEGMENT)
                helper.FindEntitiesInSegment(shape.p_centerOrStartPos, shape.p_endPos, shape.p_widthRadius, cond.p_targetEleType, cond.p_targetSubType, excludeIdx,
                    ref foundIndices, cond.p_excludeFlags, cond.p_includeFlags);
            else if (shape.p_shapeType == SearchDefines.SearchShapeType.SECTOR)
                helper.FindEntitiesInSector(shape.p_centerOrStartPos, shape.p_sectorRadius, shape.p_forwardDir, shape.p_cosHalfAngle, cond.p_targetEleType, cond.p_targetSubType,
                    excludeIdx, ref foundIndices, cond.p_excludeFlags, cond.p_includeFlags);
        }

        [BurstCompile(CompileSynchronously = true)]
        public struct ClosestSearchJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<SearchCmd> p_cmds;
            public SpatialQueryHelper p_staticQueryHelper;
            public SpatialQueryHelper p_dynamicQueryHelper;

            [WriteOnly] public NativeArray<int> p_resultIndices;
            [WriteOnly] public NativeArray<SearchCondition> p_resultConditions;
            [WriteOnly] public NativeArray<float> p_resultDistances;

            public void Execute(int index)
            {
                SearchCmd cmd = p_cmds[index];
                int bestIndex = -1;
                float minDistanceSq = float.MaxValue; // 如果是长方形/扇形也可以用它比对最优解
                SearchCondition bestMatchCond = default;

                for (int c = 0; c < cmd.p_conditions.Length; c++)
                {
                    SearchCondition cond = cmd.p_conditions[c];
                    SpatialQueryHelper qh = SpatialDefines.CheckEletypeInGrid(cond.p_targetEleType) == 1 ? p_staticQueryHelper : p_dynamicQueryHelper;
                    int foundIndex = DoClosestShapeQuery(ref qh, cmd.p_shape, cond, ref cmd.p_excludeGridIndices);

                    if (foundIndex != -1)
                    {
                        float distSq = math.distancesq(cmd.p_shape.p_centerOrStartPos, qh.p_entities[foundIndex].p_position);
                        if (distSq < minDistanceSq)
                        {
                            minDistanceSq = distSq;
                            bestIndex = foundIndex;
                            bestMatchCond = cond;
                        }
                    }
                }

                p_resultIndices[index] = bestIndex;
                p_resultConditions[index] = bestMatchCond;
                p_resultDistances[index] = bestIndex != -1 ? math.sqrt(minDistanceSq) : -1f;
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        public struct AreaSearchJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<SearchCmd> p_cmds;
            public SpatialQueryHelper p_staticQueryHelper;
            public SpatialQueryHelper p_dynamicQueryHelper;

            // Key: Job的index (对应第几个Cmd), Value: x=实体下标 y=该条命中条件的 p_targetEleType
            public NativeParallelMultiHashMap<int, int2>.ParallelWriter p_results;

            public void Execute(int index)
            {
                SearchCmd cmd = p_cmds[index];
                FixedList512Bytes<int> foundIndices = new FixedList512Bytes<int>();

                for (int c = 0; c < cmd.p_conditions.Length; c++)
                {
                    SearchCondition cond = cmd.p_conditions[c];
                    SpatialQueryHelper qh = SpatialDefines.CheckEletypeInGrid(cond.p_targetEleType) == 1 ? p_staticQueryHelper : p_dynamicQueryHelper;
                    int excludeIdx = cmd.p_excludeGridIndices.Length > 0 ? cmd.p_excludeGridIndices[0] : -1;//只支持最多exclude一个
                    DoAreaShapeQuery(ref qh, cmd.p_shape, cond, excludeIdx, ref foundIndices);

                    for (int i = 0; i < foundIndices.Length; i++)
                    {
                        p_results.Add(index, new int2(foundIndices[i], cond.p_targetEleType));
                    }
                    foundIndices.Clear();
                }
            }
        }
#endregion
    }
}

