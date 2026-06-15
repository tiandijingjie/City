using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace WarField
{
    using WE = WarFieldElements;

    public class SpatialGridManager : MonoBehaviour
    {
#region public parameters

        public static SpatialGridManager Instance = null;

#endregion

#region private parameters
        //用来重建resource grid (IJob, 因为资源数量少且必须整体扫描)
        [BurstCompile(CompileSynchronously = true)]
        private struct BuildResGridJob : IJob
        {
            [ReadOnly] public NativeArray<ResEntityData> p_resPool;
            public NativeParallelMultiHashMap<int, int> p_resGridMap;

            public float2 p_mapOrigin;
            public float p_cellSize;
            public int p_cols;
            public int p_rows;

            public void Execute()
            {
                p_resGridMap.Clear();
                for (int i = 0; i < p_resPool.Length; i++)
                {
                    ResEntityData data = p_resPool[i];
                    if (!data.p_isActive)
                        continue;
                    int col = (int)math.floor((data.p_position.x - p_mapOrigin.x) / p_cellSize);
                    int row = (int)math.floor((data.p_position.y - p_mapOrigin.y) / p_cellSize);
                    col = math.clamp(col, 0, p_cols - 1);
                    row = math.clamp(row, 0, p_rows - 1);
                    p_resGridMap.Add(row * p_cols + col, i);
                }
            }
        }

        //用来重建grid
        [BurstCompile(CompileSynchronously = true)]
        private struct BuildSpatialHashJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<GridEntityData> p_entities; //全局的所有的static或者dynamic entity,
            public NativeParallelMultiHashMap<int, int>.ParallelWriter p_gridHashMap; //map每个格子和entity的对应

            public int p_mapId;
            public float2 p_mapOrigin;
            public float p_cellSize;
            public int p_cols;
            public int p_rows;

            public void Execute(int index)
            {
                GridEntityData entity = p_entities[index];

                if (entity.p_isDead || entity.p_mapId != p_mapId)
                    return;

                float2 pos = entity.p_position;
                int col = (int)math.floor((pos.x - p_mapOrigin.x) / p_cellSize);
                int row = (int)math.floor((pos.y - p_mapOrigin.y) / p_cellSize);

                if (row >= 0 && row < p_rows && col >= 0 && col < p_cols)
                {
                    int cellIndex = row * p_cols + col;
                    p_gridHashMap.Add(cellIndex, index);
                }
            }
        }

        //每个map的grid
        private class GridMapContext
        {
            public int p_mapId;
            public float2 p_mapOrigin;
            public float p_cellSize;
            public int p_cols;
            public int p_rows;

            //记录grid中每个cell下存放的entity, <cell Index, entity Index>
            public NativeParallelMultiHashMap<int, int> p_dynamicGrid; //动态grid, 每一帧都会重建
            public NativeParallelMultiHashMap<int, int> p_staticGrid; //静态grid, p_isStaticDirty==true时候会重建

            // 可拾取资源grid (仅 OnGroundMap 使用, 由 InitResGrid 初始化)
            public NativeParallelMultiHashMap<int, int> p_resGrid;
            public bool p_isResDirty;
            public float p_resCellSize;
            public int p_resCols;
            public int p_resRows;

            public int p_curStaticEntityCount, p_curDynamicEntityCount; //当前地图中的entity的数量,用来判断是不是要对p_spatialGrid扩容
            public bool p_isStaticDirty = true; // 初始为脏，保证第一次会构建静态网格

            public GridMapContext(int id, Bounds bounds, float cell, int maxDynamicCapacity, int maxStaticCapacity)
            {
                p_mapId = id;
                p_mapOrigin = new float2(bounds.min.x, bounds.min.y);
                p_cellSize = cell;
                p_cols = Mathf.CeilToInt(bounds.size.x / p_cellSize);
                p_rows = Mathf.CeilToInt(bounds.size.y / p_cellSize);
                p_dynamicGrid = new NativeParallelMultiHashMap<int, int>(maxDynamicCapacity, Allocator.Persistent);
                p_staticGrid = new NativeParallelMultiHashMap<int, int>(maxStaticCapacity, Allocator.Persistent);
            }

            public void Dispose()
            {
                if (p_dynamicGrid.IsCreated)
                    p_dynamicGrid.Dispose();
                if (p_staticGrid.IsCreated)
                    p_staticGrid.Dispose();
                if (p_resGrid.IsCreated)
                    p_resGrid.Dispose();
            }
        }

        //这是为了添加带IGridNode的entity的
        private struct EntityAddWithGridNode
        {
            public GridEntityData p_entity;
            public IGridNode p_gridNode;
        }

        [SerializeField] private int _maxGlobalStaticEntities = 100000;  //全局最大static entity的数量
        [SerializeField] private int _maxGlobalDynamicEntities = 100000; //全局最大dynamic entity的数量
        [SerializeField] private bool _debugGrid = false;
        [SerializeField] private bool _debugEntity = false;

        private NativeArray<GridEntityData> _allStaticEntities; //记录全局所有的static entity
        private int _activeStaticEntityCount = 0;
        private int _threshHoldStaticEntityCnt; //接近最大存储上限,扩容

        private NativeArray<GridEntityData> _allDynamicEntities; //记录全局所有的dynamic entity
        private int _activeDynamicEntityCount = 0;
        private int _threshHoldDynamicEntityCnt; //接近最大存储上限,扩容

        private Dictionary<int, GridMapContext> _mapContexts; //mapId -> GridMapContext
        private Queue<EntityAddWithGridNode> _addEntityQueue;  //add entity wtith grid node queue async

        private List<IGridNode> _dynamicNodeRefs ; //排序与_allDynamicEntities一一对应
        private List<IGridNode> _staticNodeRefs;  //排序与_allStaticEntities一一对应

        private JobHandle _rebuildJob = default;
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

            _mapContexts = new Dictionary<int, GridMapContext>();
            _addEntityQueue = new  Queue<EntityAddWithGridNode>();

            //static
            _allStaticEntities = new NativeArray<GridEntityData>(_maxGlobalStaticEntities, Allocator.Persistent);
            _threshHoldStaticEntityCnt = (int)(_maxGlobalStaticEntities * 0.95f);
            _staticNodeRefs = new List<IGridNode>();

            //dynamic
            _allDynamicEntities = new NativeArray<GridEntityData>(_maxGlobalDynamicEntities, Allocator.Persistent);
            _threshHoldDynamicEntityCnt = (int)(_maxGlobalDynamicEntities * 0.95f);
            _dynamicNodeRefs = new List<IGridNode>();

            _beInited = false;
        }

        private void OnDestroy()
        {
            _rebuildJob.Complete();
            if (_allStaticEntities.IsCreated)
                _allStaticEntities.Dispose();
            if (_allDynamicEntities.IsCreated)
                _allDynamicEntities.Dispose();

            foreach (var ctx in _mapContexts.Values)
                ctx.Dispose();
        }

#if UNITY_EDITOR
        //绘制grid网格
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !_debugGrid || !_beInited || _mapContexts == null)
                return;

            // 绘制每一个地图的网格
            foreach (var ctx in _mapContexts.Values)
            {
                float2 origin = ctx.p_mapOrigin;
                float width = ctx.p_cols * ctx.p_cellSize;
                float height = ctx.p_rows * ctx.p_cellSize;

                // 设置网格线的颜色（半透明绿）
                Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.3f);

                // 画垂直线 (列)
                for (int col = 0; col <= ctx.p_cols; col++)
                {
                    float x = origin.x + col * ctx.p_cellSize;
                    Gizmos.DrawLine(new Vector3(x, origin.y, 0), new Vector3(x, origin.y + height, 0));
                }

                // 画水平线 (行)
                for (int row = 0; row <= ctx.p_rows; row++)
                {
                    float y = origin.y + row * ctx.p_cellSize;
                    Gizmos.DrawLine(new Vector3(origin.x, y, 0), new Vector3(origin.x + width, y, 0));
                }
            }

            // 绘制实体
            if (_debugEntity == true)
            {
                if (_allStaticEntities.IsCreated)
                {
                    for (int i = 0; i < _activeStaticEntityCount; i++)
                    {
                        var entity = _allStaticEntities[i];
                        if (entity.p_isDead)
                            continue;
                        // 静态物体画成灰色的实心球
                        Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);
                        Gizmos.DrawSphere(new Vector3(entity.p_position.x, entity.p_position.y, 0), entity.p_radius);
                    }
                }
                if (_allDynamicEntities.IsCreated)
                {
                    for (int i = 0; i < _activeDynamicEntityCount; i++)
                    {
                        var entity = _allDynamicEntities[i];
                        if (entity.p_isDead)
                            continue;
                        // 动态物体画成蓝色的线框球
                        Gizmos.color = Color.cyan;
                        Gizmos.DrawWireSphere(new Vector3(entity.p_position.x, entity.p_position.y, 0), entity.p_radius);
                    }
                }
            }
        }
#endif

#endregion

#region public functions

        public bool InitSpatialGridManager()
        {
            if(_beInited == true)
                return false;

            _beInited = true;
            return true;
        }

        //添加一个grid
        //每个map就有一个grid
        public bool AddGrid(int mapId, Bounds bounds, float cellSize)
        {
            if(_beInited == false)
                return false;

            if (_mapContexts.ContainsKey(mapId))
                return false;

            int maxStaticCount = _maxGlobalStaticEntities; //地面地图存放更多entity
            int maxDynamicCount = _maxGlobalDynamicEntities;
            if (mapId != WE.OnGroundMapIndex)
            {
                maxStaticCount = maxStaticCount >> 1;
                maxDynamicCount = maxDynamicCount >> 1;
            }

            var ctx = new GridMapContext(mapId, bounds, cellSize, maxDynamicCount, maxStaticCount);
            _mapContexts.Add(mapId, ctx);
            return true;
        }

        //eleType: WE.WarEleType
        public NativeArray<GridEntityData>.ReadOnly GetAllEntitiesReadOnly(int eleType)
        {
            int gridType = SpatialDefines.CheckEletypeInGrid(eleType);
            if (gridType == 1)
                return _allStaticEntities.AsReadOnly();
            else if (gridType == 2)
                return _allDynamicEntities.AsReadOnly();
            throw new System.Exception($"Can not determine which grid to get by eleType {eleType}!!!");
        }

        //获取entity查询接口
        public SpatialQueryHelper GetQueryHelper(int mapId, int eleType)
        {
            if (_mapContexts.TryGetValue(mapId, out var ctx))
            {
                NativeArray<GridEntityData> arr;
                NativeParallelMultiHashMap<int, int> grid;
                int gridType = SpatialDefines.CheckEletypeInGrid(eleType);
                if (gridType == 1)
                {
                    arr = _allStaticEntities;
                    grid = ctx.p_staticGrid;
                }
                else if (gridType == 2)
                {
                    arr = _allDynamicEntities;
                    grid = ctx.p_dynamicGrid;
                }
                else
                    throw new System.Exception($"Can not determine which grid to get by eleType {eleType}!!!");

                return new SpatialQueryHelper
                {
                    p_entities = arr,
                    p_grid = grid,
                    p_mapOrigin = ctx.p_mapOrigin,
                    p_cellSize = ctx.p_cellSize,
                    p_cols = ctx.p_cols,
                    p_rows = ctx.p_rows
                };
            }
            throw new System.Exception($"Map {mapId} doesn't exist!");
        }

        //更新一个entity的数据
        public void UpdateEntityData(int index, GridEntityData entity)
        {
            int gridType = SpatialDefines.CheckEletypeInGrid(entity.p_eleType);
            if (gridType == 1)
            {
                if (index >= 0 && index < _activeStaticEntityCount)
                {
                    _allStaticEntities[index] = entity;
                }
            }
            else if (gridType == 2)
            {
                if (index >= 0 && index < _activeDynamicEntityCount)
                {
                    _allDynamicEntities[index] = entity;
                }
            }
            else
                throw new System.Exception($"Can not determine which grid to update by eleType {entity.p_eleType}!!!");
        }

        //node可以为null, 只有需要被删除的entity才会有node
        public void AddEntity(GridEntityData entity, IGridNode node)
        {
            EntityAddWithGridNode ed = new EntityAddWithGridNode()
            {
                p_entity = entity,
                p_gridNode = node
            };
            _addEntityQueue.Enqueue(ed);
        }

        public void RemoveEntity(WE.WarEleType eleType, int index)
        {
            _rebuildJob.Complete();
            SearchManager.Instance?.CompletePendingSearchJobs();

            int gridType = SpatialDefines.CheckEletypeInGrid((int)eleType);
            if (gridType == 1) // 静态网格 (Static)
            {
                if (index < _activeStaticEntityCount)
                {
                    GridEntityData data = _allStaticEntities[index];
                    data.p_isDead = true;
                    _allStaticEntities[index] = data;
                }
            }
            else if (gridType == 2) // 动态网格 (Dynamic)
            {
                if (index < _activeDynamicEntityCount)
                {
                    GridEntityData data = _allDynamicEntities[index];
                    data.p_isDead = true;
                    _allDynamicEntities[index] = data;
                }
            }
        }

        //必须在LastUpdate中调用
        public void FlushGridCommands()
        {
            _rebuildJob.Complete();

            // 必须倒序删除，保证前面的元素不被影响
            for (int i = _activeStaticEntityCount - 1; i >= 0; i--)
            {
                if (_allStaticEntities[i].p_isDead)
                {
                    int deadMapId = _allStaticEntities[i].p_mapId;
                    if (_mapContexts.TryGetValue(deadMapId, out var mapCtx))
                    {
                        mapCtx.p_isStaticDirty = true; //静态网络需要更新
                        mapCtx.p_curStaticEntityCount--;
                    }
                    int lastIndex = _activeStaticEntityCount - 1;
                    _allStaticEntities[i] = _allStaticEntities[lastIndex]; // 最后一个元素覆盖当前空缺
                    if (Utils.ExecuteSwapAndPop(_staticNodeRefs, i) == true)  //发生了交换
                    {
                        if(_staticNodeRefs[i] != null)
                            _staticNodeRefs[i].gs_gridIndex = i;
                        //被交换的entity的index改变了,但是对应map context中的static grid并没有更新,需要被强制更新
                        int movedMapId = _allStaticEntities[i].p_mapId;
                        if (_allStaticEntities[i].p_mapId != deadMapId)
                        {
                            if (_mapContexts.TryGetValue(movedMapId, out var movedMapCtx))
                                movedMapCtx.p_isStaticDirty = true;
                        }
                    }
                    _activeStaticEntityCount--;
                }
            }

            for (int i = _activeDynamicEntityCount - 1; i >= 0; i--)
            {
                if (_allDynamicEntities[i].p_isDead)
                {
                    int deadMapId = _allDynamicEntities[i].p_mapId;
                    if (_mapContexts.TryGetValue(deadMapId, out var mapCtx))
                        mapCtx.p_curDynamicEntityCount--;

                    int lastIndex = _activeDynamicEntityCount - 1;
                    _allDynamicEntities[i] = _allDynamicEntities[lastIndex]; // 最后一个元素覆盖当前空缺
                    if (Utils.ExecuteSwapAndPop(_dynamicNodeRefs, i) == true)  //发生了交换
                        _dynamicNodeRefs[i].gs_gridIndex = i;
                    //dynamic grid是每一帧都更新的,所以不用判断是否要要强制更新
                    _activeDynamicEntityCount--;
                }
            }

            //将新的entity添加进队列
            while (_addEntityQueue.TryDequeue(out EntityAddWithGridNode ed))
            {
                GridEntityData entity = ed.p_entity;
                IGridNode node = ed.p_gridNode;

                int gridType = SpatialDefines.CheckEletypeInGrid(entity.p_eleType);
                if (gridType == 1)
                {
                    int newIndex = _activeStaticEntityCount;
                    _allStaticEntities[newIndex] = entity;
                    _activeStaticEntityCount++;
                    _staticNodeRefs.Add(node);

                    if(node != null)  //只有static的才可能是null
                        node.gs_gridIndex = newIndex;

                    //数量超过上限,需要扩容
                    if (_activeStaticEntityCount >= _threshHoldStaticEntityCnt)
                        ExpandGlobalEntityArray(1);

                    if (_mapContexts.TryGetValue(entity.p_mapId, out var mapCtx))
                    {
                        mapCtx.p_isStaticDirty = true; //静态网络需要更新
                        mapCtx.p_curStaticEntityCount++;
                    }
                }
                else if (gridType == 2)
                {
                    int newIndex = _activeDynamicEntityCount;
                    _allDynamicEntities[newIndex] = entity;
                    _activeDynamicEntityCount++;
                    _dynamicNodeRefs.Add(node);
                    node.gs_gridIndex = newIndex;

                    //数量超过上限,需要扩容
                    if (_activeDynamicEntityCount >= _threshHoldDynamicEntityCnt)
                        ExpandGlobalEntityArray(2);

                    if (_mapContexts.TryGetValue(entity.p_mapId, out var mapCtx))
                        mapCtx.p_curDynamicEntityCount++;
                }
                else
                    throw new System.Exception($"Can not determine which grid to flush by eleType {entity.p_eleType}!!!");
            }

            if(_activeStaticEntityCount != _staticNodeRefs.Count)
                GameLogger.LogError($"Length of static entity {_activeStaticEntityCount} is not the same as IGridNode count {_staticNodeRefs.Count}");

            if(_activeDynamicEntityCount != _dynamicNodeRefs.Count)
                GameLogger.LogError($"Length of dynamic entity {_activeDynamicEntityCount} is not the same as IGridNode count {_dynamicNodeRefs.Count}");
        }

        //必须在LastUpdate中调用,只能在在FlushGridCommands之后调用
        //重建静态网格
        public JobHandle RebuildGrids(JobHandle dependency = default)
        {
            if (_mapContexts.Count == 0)
                return dependency;
            if(_activeStaticEntityCount == 0 && _activeDynamicEntityCount == 0)
                return dependency;

            // 强制主线程等待传入的依赖完毕，因为下面要执行 Clear()
            dependency.Complete();

            NativeList<JobHandle> jobHandles = new NativeList<JobHandle>(Allocator.Temp);
            foreach (var ctx in _mapContexts.Values)
            {
                // 动态网格：每帧清空并重建
                if (ctx.p_curDynamicEntityCount >= ctx.p_dynamicGrid.Capacity)
                {
                    int newCap = ctx.p_dynamicGrid.Capacity * 2;
                    ctx.p_dynamicGrid.Capacity = newCap;
                    GameLogger.LogWarning($"Map {ctx.p_mapId} dynamic grid capacity enlarge to {newCap}");
                }
                ctx.p_dynamicGrid.Clear();
                var dynamicJob = new BuildSpatialHashJob
                {
                    p_entities = _allDynamicEntities,
                    p_gridHashMap = ctx.p_dynamicGrid.AsParallelWriter(),
                    p_mapId = ctx.p_mapId,
                    p_mapOrigin = ctx.p_mapOrigin,
                    p_cellSize = ctx.p_cellSize,
                    p_cols = ctx.p_cols,
                    p_rows = ctx.p_rows
                };
                jobHandles.Add(dynamicJob.Schedule(_activeDynamicEntityCount, 64));

                // 静态网格：只有脏了才重建
                if (ctx.p_isStaticDirty)
                {
                    if (ctx.p_curStaticEntityCount >= ctx.p_staticGrid.Capacity)
                    {
                        int newCap = ctx.p_staticGrid.Capacity * 2;
                        ctx.p_staticGrid.Capacity = newCap;
                        GameLogger.LogWarning($"Map {ctx.p_mapId} static grid capacity enlarge to {newCap}");
                    }
                    ctx.p_staticGrid.Clear();
                    var staticJob = new BuildSpatialHashJob
                    {
                        p_entities = _allStaticEntities,
                        p_gridHashMap = ctx.p_staticGrid.AsParallelWriter(),
                        p_mapId = ctx.p_mapId,
                        p_mapOrigin = ctx.p_mapOrigin,
                        p_cellSize = ctx.p_cellSize,
                        p_cols = ctx.p_cols,
                        p_rows = ctx.p_rows
                    };
                    jobHandles.Add(staticJob.Schedule(_activeStaticEntityCount, 64));
                    ctx.p_isStaticDirty = false; // 重建后洗白
                }

            }

            // 将所有地图的 Job 打包成一个总句柄返回
            _rebuildJob = JobHandle.CombineDependencies(jobHandles.AsArray());
            jobHandles.Dispose();
            return _rebuildJob;
        }

        // 主线程同步查询网格前必须调用，等待上一帧 LateUpdate 调度的 BuildSpatialHashJob 写完 p_*Grid。
        public void CompletePendingRebuildJob()
        {
            _rebuildJob.Complete();
        }

        // 为指定地图创建资源空间网格 (由 ResourceGrid.InitResGrid 调用, 仅 OnGroundMap)
        public void InitResGrid(int mapId, Bounds bounds, float resCellSize)
        {
            if (!_mapContexts.TryGetValue(mapId, out var ctx))
            {
                GameLogger.LogError($"[SpatialGridManager] Map {mapId} not found when calling InitResGrid!");
                return;
            }
            ctx.p_resCellSize = resCellSize;
            ctx.p_resCols = Mathf.CeilToInt(bounds.size.x / resCellSize);
            ctx.p_resRows = Mathf.CeilToInt(bounds.size.y / resCellSize);
            ctx.p_resGrid = new NativeParallelMultiHashMap<int, int>(4096, Allocator.Persistent);
            ctx.p_isResDirty = true;
        }

        // 标记指定地图的资源网格为脏（资源增删/锁定时调用，供 ScheduleResGridRebuild 判断是否需要重建）
        public void MarkResGridDirty(int mapId)
        {
            if (_mapContexts.TryGetValue(mapId, out var ctx) && ctx.p_resGrid.IsCreated)
                ctx.p_isResDirty = true;
        }

        // 构建资源查询辅助结构（在 BuildResGridJob 完成后由主线程调用）
        public ResQueryHelper GetResQueryHelper(int mapId)
        {
            if (!_mapContexts.TryGetValue(mapId, out var ctx) || !ctx.p_resGrid.IsCreated)
                throw new System.Exception($"[SpatialGridManager] Map {mapId} has no res grid!");
            if (ResourceGrid.Instance == null || !ResourceGrid.Instance.gs_resPool.IsCreated)
                throw new System.Exception("[SpatialGridManager] ResourceGrid not initialized!");

            return new ResQueryHelper
            {
                p_resPool  = ResourceGrid.Instance.gs_resPool,
                p_resGrid  = ctx.p_resGrid,
                p_mapOrigin = ctx.p_mapOrigin,
                p_cellSize = ctx.p_resCellSize,
                p_cols     = ctx.p_resCols,
                p_rows     = ctx.p_resRows
            };
        }

        // 按需调度资源网格重建 Job（仅在有 SearchResClosest 请求时由 SearchManager 调用）
        // 若数据未变（p_isResDirty==false），跳过 Job 并直接返回传入的依赖句柄
        public JobHandle ScheduleResGridRebuild(int mapId, JobHandle dependency = default)
        {
            if (!_mapContexts.TryGetValue(mapId, out var ctx) || !ctx.p_resGrid.IsCreated)
                return dependency;
            if (!ctx.p_isResDirty)
                return dependency; // 数据未变，无需重建

            if (ResourceGrid.Instance == null)
                return dependency;
            var resPool = ResourceGrid.Instance.gs_resPool;
            if (!resPool.IsCreated)
                return dependency;

            if (resPool.Length > ctx.p_resGrid.Capacity)
            {
                ctx.p_resGrid.Capacity = resPool.Length * 2;
                GameLogger.LogWarning($"Map {mapId} res grid capacity enlarged to {ctx.p_resGrid.Capacity}");
            }

            var job = new BuildResGridJob
            {
                p_resPool = resPool,
                p_resGridMap = ctx.p_resGrid,
                p_mapOrigin = ctx.p_mapOrigin,
                p_cellSize = ctx.p_resCellSize,
                p_cols = ctx.p_resCols,
                p_rows = ctx.p_resRows
            };
            ctx.p_isResDirty = false;
            return job.Schedule(dependency);
        }

        public IGridNode GetGridNode(int eleType, int index)
        {
            int gridType = SpatialDefines.CheckEletypeInGrid(eleType);
            if (gridType == 1)
            {
                if (index >= 0 && index < _staticNodeRefs.Count)
                    return _staticNodeRefs[index];
            }
            else if (gridType == 2)
            {
                if (index >= 0 && index < _dynamicNodeRefs.Count)
                    return _dynamicNodeRefs[index];
            }
            return null;
        }
#endregion

#region private functions
        //对_allEntities进行扩容
        //效率极低
        private void ExpandGlobalEntityArray(int gridType)
        {
            if (gridType == 1)
            {
                int newCapacity = _maxGlobalStaticEntities * 2;
                GameLogger.LogWarning($"Start enlarge the static entity array, capacity {_maxGlobalStaticEntities} -> {newCapacity} !!! ");
                NativeArray<GridEntityData> newArray = new NativeArray<GridEntityData>(newCapacity, Allocator.Persistent);

                // 只拷贝 _activeEntityCount 长度的数据
                if (_activeStaticEntityCount > 0)
                    NativeArray<GridEntityData>.Copy(_allStaticEntities, newArray, _activeStaticEntityCount);

                // 释放老的内存
                if (_allStaticEntities.IsCreated)
                    _allStaticEntities.Dispose();

                // 将主指针指向新内存，并更新容量记录
                _allStaticEntities = newArray;
                _maxGlobalStaticEntities = newCapacity;
                _threshHoldStaticEntityCnt = (int)(_maxGlobalStaticEntities * 0.95f);

                Debug.Log("Enlarge the static entity array finish !!!");
            }
            else if (gridType == 2)
            {
                int newCapacity = _maxGlobalDynamicEntities * 2;
                GameLogger.LogWarning($"Start enlarge the dynamic entity array, capacity {_maxGlobalDynamicEntities} -> {newCapacity} !!! ");
                NativeArray<GridEntityData> newArray = new NativeArray<GridEntityData>(newCapacity, Allocator.Persistent);

                // 只拷贝 _activeEntityCount 长度的数据
                if (_activeDynamicEntityCount > 0)
                    NativeArray<GridEntityData>.Copy(_allDynamicEntities, newArray, _activeDynamicEntityCount);

                // 释放老的内存
                if (_allDynamicEntities.IsCreated)
                    _allDynamicEntities.Dispose();

                // 将主指针指向新内存，并更新容量记录
                _allDynamicEntities = newArray;
                _maxGlobalDynamicEntities = newCapacity;
                _threshHoldDynamicEntityCnt = (int)(_maxGlobalDynamicEntities * 0.95f);

                Debug.Log("Enlarge the dynamic entity array finish !!!");
            }
            else
                throw new System.Exception($"Can not determine which grid to enlarge {gridType}!!!");
        }
#endregion
    }
}
