using System;
using System.Collections.Generic;
using Unity.Burst;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;

namespace WarField
{
    using WE = WarFieldElements;

    //处理一个map各种寻路
    public class PathFinderMap : MonoBehaviour
    {
#region public parameters

#endregion

#region private parameters

        // A* 寻路专用的节点类 (对象池优化，避免频繁 GC)
        private class AStarNode : IComparable<AStarNode>
        {
            public int2 Position;
            public int G, H;
            public int F => G + H;
            public AStarNode Parent;

            public void Reset(int2 pos, int g, int h, AStarNode parent)
            {
                Position = pos;
                G = g;
                H = h;
                Parent = parent;
            }

            public int CompareTo(AStarNode other)
            {
                int compare = F.CompareTo(other.F);
                if (compare == 0)
                    return H.CompareTo(other.H);
                return compare;
            }
        }

        [SerializeField] private float _cellSize = 1.5f;

        [SerializeField] private float _friendlyDesX = 150f; //相对于地图左下角的x轴距离 , friendly 向enemy推进,什么位置开始收敛流场
        [SerializeField] private float _enemyDesX = 50f; //相对于地图左下角的x轴距离, enemy 向friendly推进,什么位置开始收敛流场

        [Header("Debug Visualization")] [SerializeField]
        private bool _showGrid = false;

        [SerializeField] private bool _showBoundaries = false;
        [SerializeField] private bool _debugFriendly = false;
        [SerializeField] private bool _debugEnemy = false;

        //两端的终点目标,可以为null
        [SerializeField] private Transform _castleFriendly; //己方的最终的城堡
        [SerializeField] private Transform _castleEnemy; //敌方的最终城堡

        //obstacle侵入到cell多少才能算这个cell不能通行
        [SerializeField] private float _obstacleTolerance = 0.1f;

        // cost场
        private NativeArray<byte> _costMap;

        private int _currentMaxFlowFields = 16; // 初始池子容量
        private NativeArray<float2> _flowFieldPool; // 所有流场的一维合并数组
        private Queue<int> _availableFlowIndices; // 可用的局部流场ID池
        private Dictionary<int, int> _activeLocalFlows = new Dictionary<int, int>(); //[flowfieldId, _flowFieldPool中这个流场开始的index]

        private NativeList<GridEntityData> _flowFieldBlockers;
        private int _blockerCount; //blocker总的数量

        // 地图尺寸缓存
        private int _mapId;
        private float2 _origin;
        private int _cols;
        private int _rows;
        private bool _beInited;
        private Bounds _mapBounds;
        private int _totalCells; //地图中总共的cell数量

        // 动态更新流场地图
        private bool _isFlowFieldDirty = false;
        private List<float2> _buildingsToRemoveThisFrame = new List<float2>(); //只记录位置,删除的时候去计算与位置最近的建筑
        private List<GridEntityData> _buildingsToAddThisFrame = new List<GridEntityData>();
        private Dictionary<int, int> _flowFieldRefCounts = new Dictionary<int, int>(); //每个动态流场中使用的士兵数量

        // Job 句柄，用于追踪后台计算是否完成
        private JobHandle _flowFieldJobHandle;

        //A*
        private static readonly int2[] _neighbors =
        {
            new int2(0, 1), new int2(0, -1), new int2(1, 0), new int2(-1, 0),
            new int2(1, 1), new int2(1, -1), new int2(-1, 1), new int2(-1, -1)
        };

        private static readonly int[] _moveCosts = { 10, 10, 10, 10, 14, 14, 14, 14 };
        private static readonly int2[] _bsfDirs = {
            new int2(0,1), new int2(0,-1), new int2(1,0), new int2(-1,0),
            new int2(1,1), new int2(1,-1), new int2(-1,1), new int2(-1,-1)
        };

        // obstacle代价场 只计算一次
        private NativeArray<byte> _staticCostMap;

        // 多源 HomeFlowField：供 farmer GOBACK 使用
        private NativeList<int> _homeTargetIndices;
        private NativeArray<int> _emptyTargetIndices; // 供不需要多源的 CalcFlowFieldJob 占位使用

        // 异步 A* 寻路队列系统
        private struct PathReq
        {
            public float2 start;
            public float2 end;
            public Action<List<Vector2>> callback;
        }
        private Queue<PathReq> _pendingPathReqs = new Queue<PathReq>();
        private List<PathReq> _activePathReqs = new List<PathReq>();
        private JobHandle _aStarJobHandle;
        private bool _isAStarJobRunning = false;
        private NativeArray<float2> _aStarStarts;
        private NativeArray<float2> _aStarEnds;
        private NativeList<float2> _aStarAllPaths;
        private NativeArray<int2> _aStarPathOffsets;
#endregion

#region private parameters' get set

        public float gs_cellSize
        {
            get { return _cellSize; }
        }

        public float gs_friendlyDesX
        {
            get { return _friendlyDesX; }
            set { _friendlyDesX = value; }
        }

        public float gs_enemyDesX
        {
            get { return _enemyDesX; }
            set { _enemyDesX = value; }
        }

        public Bounds gs_bounds
        {
            get { return _mapBounds; }
        }

        public int gs_cols
        {
            get { return _cols; }
        }

        public int gs_rows
        {
            get { return _rows; }
        }

        public float2 gs_origin
        {
            get { return _origin; }
        }

        public NativeArray<byte> gs_costMap
        {
            get { return _costMap; }
        }

        public NativeArray<float2> gs_flowFieldPool
        {
            get { return _flowFieldPool; }
        }

        public JobHandle gs_flowFieldJobHandle
        {
            get { return _flowFieldJobHandle; }
        }

#endregion

#region Unity callbacks

        private void Awake()
        {
            _blockerCount = 0;
            _beInited = false;
            _availableFlowIndices = new Queue<int>();
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !_beInited)
                return;

            // 绘制底层网格
            if (_showGrid)
            {
                Gizmos.color = new Color(1f, 1f, 1f, 0.15f); // 半透明的白色线条
                float mapWidth = _cols * _cellSize;
                float mapHeight = _rows * _cellSize;

                // 画竖线 (列)
                for (int c = 0; c <= _cols; c++)
                {
                    float x = _origin.x + c * _cellSize;
                    Gizmos.DrawLine(new Vector3(x, _origin.y, 0), new Vector3(x, _origin.y + mapHeight, 0));
                }

                // 画横线 (行)
                for (int r = 0; r <= _rows; r++)
                {
                    float y = _origin.y + r * _cellSize;
                    Gizmos.DrawLine(new Vector3(_origin.x, y, 0), new Vector3(_origin.x + mapWidth, y, 0));
                }
            }

            //画两端的边缘流场的边界
            if (_showBoundaries)
            {
                float mapHeight = _rows * _cellSize;
                float absoluteFriendlyX = _origin.x + _friendlyDesX;
                float absoluteEnemyX = _origin.x + _enemyDesX;

                Gizmos.color = new Color(0, 0.5f, 1f, 0.8f);
                Gizmos.DrawLine(new Vector3(absoluteFriendlyX, _origin.y, 0), new Vector3(absoluteFriendlyX, _origin.y + mapHeight, 0));

                Gizmos.color = new Color(1f, 0, 0, 0.8f);
                Gizmos.DrawLine(new Vector3(absoluteEnemyX, _origin.y, 0), new Vector3(absoluteEnemyX, _origin.y + mapHeight, 0));
            }

            // 确保 Job 计算完毕后再画图，防止读取冲突
            _flowFieldJobHandle.Complete();

            if (_debugFriendly && _flowFieldPool.IsCreated)
            {
                Gizmos.color = new Color(0, 0.8f, 1f, 0.4f);
                DrawArrows(new NativeSlice<float2>(_flowFieldPool, WE.FriendlyFlowFieldDefaultId * _cols * _rows, _cols * _rows));
            }

            if (_debugEnemy && _flowFieldPool.IsCreated)
            {
                Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
                DrawArrows(new NativeSlice<float2>(_flowFieldPool, WE.EnemyFlowFieldDefaultId * _cols * _rows, _cols * _rows));
            }
        }

        private void OnDestroy()
        {
            // 必须等待 Job 完成才能销毁内存，否则会崩溃
            _flowFieldJobHandle.Complete();
            _aStarJobHandle.Complete();

            if (_flowFieldPool.IsCreated)
                _flowFieldPool.Dispose();
            if (_costMap.IsCreated)
                _costMap.Dispose();
            if (_staticCostMap.IsCreated)
                _staticCostMap.Dispose();
            if (_flowFieldBlockers.IsCreated)
                _flowFieldBlockers.Dispose();
            if (_homeTargetIndices.IsCreated)
                _homeTargetIndices.Dispose();

            if (_isAStarJobRunning)
            {
                if (_aStarStarts.IsCreated)
                    _aStarStarts.Dispose();
                if (_aStarEnds.IsCreated)
                    _aStarEnds.Dispose();
                if (_aStarAllPaths.IsCreated)
                    _aStarAllPaths.Dispose();
                if (_aStarPathOffsets.IsCreated)
                    _aStarPathOffsets.Dispose();
            }

            _beInited = false;
        }

#endregion

#region public functions

        public void InitPathFinderMap(int mapId, Bounds passableBounds)
        {
            _mapId = mapId;
            _mapBounds = passableBounds;
            _origin = new float2(passableBounds.min.x, passableBounds.min.y);
            _cols = Mathf.CeilToInt(passableBounds.size.x / _cellSize);
            _rows = Mathf.CeilToInt(passableBounds.size.y / _cellSize);

            _totalCells = _cols * _rows;
            if (_flowFieldPool.IsCreated)
                _flowFieldPool.Dispose();
            if (_costMap.IsCreated)
                _costMap.Dispose();
            if (_staticCostMap.IsCreated)
                _staticCostMap.Dispose();

            if (_flowFieldBlockers.IsCreated)
                _flowFieldBlockers.Dispose();

            _currentMaxFlowFields = 16;
            _flowFieldPool = new NativeArray<float2>(_totalCells * _currentMaxFlowFields, Allocator.Persistent);
            _costMap = new NativeArray<byte>(_totalCells, Allocator.Persistent);
            _staticCostMap = new NativeArray<byte>(_totalCells, Allocator.Persistent);
            _flowFieldBlockers = new NativeList<GridEntityData>(2048, Allocator.Persistent);

            _availableFlowIndices.Clear();
            for (int i = WE.LocalFlowFieldStartId; i < _currentMaxFlowFields; i++)
                _availableFlowIndices.Enqueue(i);

            if (_homeTargetIndices.IsCreated) _homeTargetIndices.Dispose();
            if (_emptyTargetIndices.IsCreated) _emptyTargetIndices.Dispose();
            _homeTargetIndices = new NativeList<int>(16, Allocator.Persistent);
            if (_emptyTargetIndices.IsCreated) _emptyTargetIndices.Dispose();
            _emptyTargetIndices = new NativeArray<int>(0, Allocator.Persistent);

            BakeStaticObstacles();
            // 在主线程直接跑一次专属的静态代价场多维泛洪烘焙 Job
            var staticBakeJob = new BakeStaticCostMapJob
            {
                p_staticCostMap = _staticCostMap,
                p_blockers = _flowFieldBlockers.AsArray(),
                p_mapId = _mapId,
                p_origin = _origin,
                p_cellSize = _cellSize,
                p_cols = _cols,
                p_rows = _rows,
                p_obstacleTolerance = _obstacleTolerance
            };
            staticBakeJob.Run(); // 利用 Burst 在加载界面瞬间完成高精度静态固化填充

            // 静态障碍物和内部实心空间已经永久固化在 _staticCostMap 里了，
            // 它们再不需要再参与运行时的重新迭代了！直接将 Blocker 列表清空。
            // 后面这个列表将纯粹、干净地只服务于战场上动态添加/拆除的防御塔建筑！
            _flowFieldBlockers.Clear();
            _blockerCount = 0;

            _beInited = true;

            ScheduleFlowFieldJobs();
            _flowFieldJobHandle.Complete(); //在init阶段确保job执行完成
        }

        //需要在lastupdate里面调用
        public JobHandle FlushFlowFieldMap()
        {
            if (!_beInited) return default;

            UpdateAsyncAStar();

            if (!_isFlowFieldDirty)
                return _flowFieldJobHandle;

            // 添加建筑
            if (_buildingsToAddThisFrame.Count > 0)
            {
                for (int i = 0; i < _buildingsToAddThisFrame.Count; i++)
                    _flowFieldBlockers.Add(_buildingsToAddThisFrame[i]);
                _buildingsToAddThisFrame.Clear();
            }

            // 移除建筑
            if (_buildingsToRemoveThisFrame.Count > 0)
            {
                foreach (var pos in _buildingsToRemoveThisFrame)
                {
                    for (int i = _flowFieldBlockers.Length - 1; i >= 0; i--)
                    {
                        if (math.distancesq(_flowFieldBlockers[i].p_position, pos) < 0.01f)
                        {
                            _flowFieldBlockers.RemoveAtSwapBack(i);
                            break;
                        }
                    }
                }

                _buildingsToRemoveThisFrame.Clear();
            }

            _blockerCount = _flowFieldBlockers.Length;

            ScheduleFlowFieldJobs();
            _isFlowFieldDirty = false;
            return _flowFieldJobHandle;
        }

        public void AddEntity(GridEntityData newBuilding)
        {
            if (_beInited == false)
                return;
            _buildingsToAddThisFrame.Add(newBuilding);
            _isFlowFieldDirty = true;
        }

        public void RemoveEntity(Vector2 buildingPos)
        {
            if (_beInited == false)
                return;
            _buildingsToRemoveThisFrame.Add((float2)buildingPos);
            _isFlowFieldDirty = true;
        }

        //把世界坐标吸附到最近的可走 cell 中心；找不到时返回 false
        public bool TryFindNearestWalkableWorldPos(Vector2 worldPos, out Vector2 walkableWorldPos)
        {
            walkableWorldPos = worldPos;
            if (!_beInited || !_costMap.IsCreated)
                return false;

            int2 coord = WorldToCellCoord(worldPos);
            int idx = FindNearestWalkableCell(coord.x, coord.y);
            if (idx == -1)
                return false;

            int2 snapped = CellIndexToCoord(idx);
            walkableWorldPos = new Vector2(
                _origin.x + snapped.x * _cellSize + _cellSize * 0.5f,
                _origin.y + snapped.y * _cellSize + _cellSize * 0.5f);
            return true;
        }

        //获取一个局部流场
        //成功时 snappedTargetWorldPos 输出 BFS 吸附后的可走目标坐标，调用方应该用它替代原始点击坐标
        public int RequestLocalFlowField(Vector2 targetWorldPos, int unitCount, out Vector2 snappedTargetWorldPos)
        {
            snappedTargetWorldPos = targetWorldPos;
            if (!_beInited || unitCount <= 1)
                return -1;

            if (_availableFlowIndices.Count == 0)
            {
                ExpandFlowFieldPool(); // 池子空了自动扩容！
            }

            int flowIndex = _availableFlowIndices.Dequeue();

            //计算目标pos的cell的行列
            int2 coord = WorldToCellCoord(targetWorldPos);
            // 有可能点击到无效的cell上  找到最近的有效cell
            int validTargetIndex = FindNearestWalkableCell(coord.x, coord.y);
            if (validTargetIndex == -1)
            {
                // 极端异常情况：如果在半径内找不到任何落脚点，直接回收这个流场，让士兵走默认逻辑
                _availableFlowIndices.Enqueue(flowIndex);
                return -1;
            }

            // 把吸附结果回传给调用方，让所有共享此流场的士兵以此为 _cmdTargetPos，
            // 否则 SoldierMoveJob 在 1.5*cellSize 终点圈内会脱离流场直插原始点击点（可能在障碍物内）。
            int2 snappedCoord = CellIndexToCoord(validTargetIndex);
            snappedTargetWorldPos = new Vector2(
                _origin.x + snappedCoord.x * _cellSize + _cellSize * 0.5f,
                _origin.y + snappedCoord.y * _cellSize + _cellSize * 0.5f);

            _activeLocalFlows.Add(flowIndex, validTargetIndex);
            _flowFieldRefCounts[flowIndex] = unitCount;
            var localJob = new CalcFlowFieldJob
            {
                p_costMap = _costMap,
                p_flowFieldSlice = new NativeSlice<float2>(_flowFieldPool, flowIndex * _totalCells, _totalCells),
                p_cols = _cols,
                p_rows = _rows,
                p_totalCells = _totalCells,
                p_startC = 0,
                p_endC = _cols - 1,
                p_pushDir = 0, // 聚拢模式
                p_targetIndex = validTargetIndex,
                p_targetIndices = _emptyTargetIndices,
                p_defaultDir = float2.zero
            };

            // 须等本帧 SoldierMoveJob 读完 pool 后再写；并与其它流场写任务串行
            JobHandle deps = _flowFieldJobHandle;
            if (SoldierCtrl.Instance != null)
                deps = JobHandle.CombineDependencies(deps, SoldierCtrl.Instance.gs_moveJobHandle);
            _flowFieldJobHandle = localJob.Schedule(deps);
            return flowIndex;
        }

        //士兵到达目标位置或者中途被再一次被操作 将自己从局部流场中释放
        public void ReleaseUnitFromFlowField(int flowIndex)
        {
            if (flowIndex >= WE.LocalFlowFieldStartId && _flowFieldRefCounts.ContainsKey(flowIndex))
            {
                _flowFieldRefCounts[flowIndex]--;

                // 当最后一名士兵退出该流场时，彻底释放它
                if (_flowFieldRefCounts[flowIndex] <= 0)
                {
                    _flowFieldRefCounts.Remove(flowIndex);
                    ReleaseLocalFlowField(flowIndex);  //流场没有unit使用了就释放流场
                }
            }
        }

        // A*寻路   在主线程运行
        public List<Vector2> FindPathAStar(Vector2 startWorld, Vector2 endWorld)
        {
            List<Vector2> path = new List<Vector2>();
            if (!_beInited || !_costMap.IsCreated)
                return path;

            _flowFieldJobHandle.Complete();

            int2 startGrid = WorldToCellCoord(startWorld);
            int2 endGrid = WorldToCellCoord(endWorld);
            int rawEndIndex = endGrid.y * _cols + endGrid.x;
            bool isRawEndWalkable = rawEndIndex >= 0 && rawEndIndex < _totalCells && _costMap[rawEndIndex] != 255;

            if (startGrid.x == endGrid.x && startGrid.y == endGrid.y)
            {
                path.Add(endWorld);
                return path;
            }

            int validTargetIndex = FindNearestWalkableCell(endGrid.x, endGrid.y);
            if (validTargetIndex == -1)
                return path; //玩家点击的位置以及附近都不可达

            endGrid = CellIndexToCoord(validTargetIndex);
            List<AStarNode> openList = new List<AStarNode>();
            Dictionary<int, AStarNode> openSet = new Dictionary<int, AStarNode>();
            HashSet<int> closedSet = new HashSet<int>();

            AStarNode startNode = new AStarNode();
            startNode.Reset(startGrid, 0, GetHeuristic(startGrid, endGrid), null);
            openList.Add(startNode);
            openSet[startGrid.y * _cols + startGrid.x] = startNode;

            AStarNode endNode = null;
            int maxIterations = 1500;

            while (openList.Count > 0 && maxIterations-- > 0)
            {
                int bestIndex = 0;
                for (int i = 1; i < openList.Count; i++) // 遍历找出 F 值/H 值最小的节点索引
                {
                    if (openList[i].CompareTo(openList[bestIndex]) < 0)
                        bestIndex = i;
                }

                AStarNode current = openList[bestIndex];
                openList.RemoveAt(bestIndex);

                int currKey = CellCoordToIndex(current.Position);
                openSet.Remove(currKey);
                closedSet.Add(currKey);

                if (current.Position.x == endGrid.x && current.Position.y == endGrid.y)
                {
                    endNode = current;
                    break;
                }

                for (int i = 0; i < 8; i++)
                {
                    int2 neighborPos = current.Position + _neighbors[i];
                    if (neighborPos.x < 0 || neighborPos.x >= _cols || neighborPos.y < 0 || neighborPos.y >= _rows)
                        continue;

                    int nKey = CellCoordToIndex(neighborPos);
                    if (closedSet.Contains(nKey) || _costMap[nKey] == 255)
                        continue;

                    if (i >= 4) // 穿墙保护
                    {
                        if (_costMap[currKey] == 255 && _costMap[nKey] == 255)
                            continue;
                    }

                    int tentativeG = current.G + _moveCosts[i];

                    if (openSet.TryGetValue(nKey, out AStarNode existingNode))
                    {
                        if (tentativeG < existingNode.G)
                        {
                            existingNode.G = tentativeG;
                            existingNode.Parent = current;
                        }
                    }
                    else
                    {
                        AStarNode neighborNode = new AStarNode();
                        neighborNode.Reset(neighborPos, tentativeG, GetHeuristic(neighborPos, endGrid), current);
                        openList.Add(neighborNode);
                        openSet[nKey] = neighborNode;
                    }
                }
            }

            if (endNode != null)
            {
                AStarNode curr = endNode;
                while (curr != null)
                {
                    Vector2 worldPos = new Vector2(
                        _origin.x + curr.Position.x * _cellSize + _cellSize * 0.5f,
                        _origin.y + curr.Position.y * _cellSize + _cellSize * 0.5f
                    );
                    path.Add(worldPos);
                    curr = curr.Parent;
                }

                path.Reverse();
                // 插入路径平滑逻辑，消除锯齿折线
                path = SmoothPath(path);

                if (path.Count > 0)
                    path.RemoveAt(0);
                // 只有原始点击点本身可达时，才把最后一个路径点改成精确点击坐标。
                // 否则保持 A* 找到的最近可走格中心，避免终点落在障碍里导致贴墙抖动。
                if (path.Count > 0 && isRawEndWalkable)
                    path[path.Count - 1] = endWorld;
            }

            return path;
        }

        // 提交异步 A* 寻路请求（非阻塞，通过 callback 返回结果）
        public void RequestPathAStar(Vector2 startWorld, Vector2 endWorld, Action<List<Vector2>> callback)
        {
            if (!_beInited || !_costMap.IsCreated)
            {
                callback?.Invoke(new List<Vector2>());
                return;
            }

            int2 endGrid = WorldToCellCoord(endWorld);
            int validTargetIndex = FindNearestWalkableCell(endGrid.x, endGrid.y);
            if (validTargetIndex == -1)
            {
                callback?.Invoke(new List<Vector2>());
                return;
            }

            int2 snappedEndGrid = CellIndexToCoord(validTargetIndex);
            Vector2 safeEndWorld = new Vector2(
                _origin.x + snappedEndGrid.x * _cellSize + _cellSize * 0.5f,
                _origin.y + snappedEndGrid.y * _cellSize + _cellSize * 0.5f
            );

            _pendingPathReqs.Enqueue(new PathReq
            {
                start = new float2(startWorld.x, startWorld.y),
                end = new float2(safeEndWorld.x, safeEndWorld.y),
                callback = callback
            });
        }

        // 注册城镇中心到 HomeFlowField 多源集合
        public void AddTownCenter(Vector2 worldPos)
        {
            if (!_beInited || !_costMap.IsCreated) return;

            int2 coord = WorldToCellCoord(worldPos);
            int validIdx = FindNearestWalkableCell(coord.x, coord.y);
            if (validIdx != -1 && !_homeTargetIndices.Contains(validIdx))
            {
                _homeTargetIndices.Add(validIdx);
                _isFlowFieldDirty = true;
            }
        }

        // 移除城镇中心（容差 ±2 cell）
        public void RemoveTownCenter(Vector2 worldPos)
        {
            if (!_beInited || !_costMap.IsCreated || _homeTargetIndices.Length == 0) return;

            int2 coord = WorldToCellCoord(worldPos);
            for (int i = 0; i < _homeTargetIndices.Length; i++)
            {
                int2 targetCoord = CellIndexToCoord(_homeTargetIndices[i]);
                if (math.abs(targetCoord.x - coord.x) <= 2 && math.abs(targetCoord.y - coord.y) <= 2)
                {
                    _homeTargetIndices.RemoveAtSwapBack(i);
                    _isFlowFieldDirty = true;
                    return;
                }
            }
        }
#endregion

#region private functions

        // 异步 A* Job 的调度与结果回收（每帧在 FlushFlowFieldMap 开头调用）
        private void UpdateAsyncAStar()
        {
            if (_isAStarJobRunning)
            {
                if (_aStarJobHandle.IsCompleted)
                {
                    _aStarJobHandle.Complete();

                    for (int i = 0; i < _activePathReqs.Count; i++)
                    {
                        List<Vector2> path = new List<Vector2>();
                        int2 offset = _aStarPathOffsets[i];
                        for (int p = 0; p < offset.y; p++)
                        {
                            float2 point = _aStarAllPaths[offset.x + p];
                            path.Add(new Vector2(point.x, point.y));
                        }

                        // 如果路径非空，将最后一个点还原为精确点击坐标
                        if (path.Count > 0 && offset.y > 0)
                        {
                            float2 exactEnd = _activePathReqs[i].end;
                            path[path.Count - 1] = new Vector2(exactEnd.x, exactEnd.y);
                        }

                        _activePathReqs[i].callback?.Invoke(path);
                    }

                    _activePathReqs.Clear();
                    _isAStarJobRunning = false;

                    _aStarStarts.Dispose();
                    _aStarEnds.Dispose();
                    _aStarAllPaths.Dispose();
                    _aStarPathOffsets.Dispose();
                }
            }

            // 打包新请求（每帧最多 32 个，防止峰值突刺）
            if (!_isAStarJobRunning && _pendingPathReqs.Count > 0)
            {
                int batchSize = Mathf.Min(_pendingPathReqs.Count, 32);
                _aStarStarts = new NativeArray<float2>(batchSize, Allocator.Persistent);
                _aStarEnds = new NativeArray<float2>(batchSize, Allocator.Persistent);
                _aStarAllPaths = new NativeList<float2>(batchSize * 100, Allocator.Persistent);
                _aStarPathOffsets = new NativeArray<int2>(batchSize, Allocator.Persistent);

                for (int i = 0; i < batchSize; i++)
                {
                    PathReq req = _pendingPathReqs.Dequeue();
                    _activePathReqs.Add(req);
                    _aStarStarts[i] = req.start;
                    _aStarEnds[i] = req.end;
                }

                var job = new AStarBatchJob
                {
                    costMap = _costMap,
                    startPoints = _aStarStarts,
                    endPoints = _aStarEnds,
                    cols = _cols,
                    rows = _rows,
                    totalCells = _totalCells,
                    origin = _origin,
                    cellSize = _cellSize,
                    allPaths = _aStarAllPaths,
                    pathOffsets = _aStarPathOffsets
                };

                // A* 仅读取 CostMap，必须等待上一帧流场写入结束
                _aStarJobHandle = job.Schedule(_flowFieldJobHandle);
                _isAStarJobRunning = true;
            }
        }

        //释放一个局部流场
        private void ReleaseLocalFlowField(int flowIndex)
        {
            if (flowIndex >= WE.LocalFlowFieldStartId && flowIndex < _currentMaxFlowFields)
            {
                _activeLocalFlows.Remove(flowIndex);
                _availableFlowIndices.Enqueue(flowIndex);
            }
        }

        private void ExpandFlowFieldPool()
        {
            // 必须等待所有后台寻路任务完成才能迁移内存！
            if (SoldierCtrl.Instance != null)
                SoldierCtrl.Instance.WaitForMoveJobFinish();
            _flowFieldJobHandle.Complete();

            int oldMax = _currentMaxFlowFields;
            int newMax = oldMax * 2;

            GameLogger.LogWarning($"Expanding FlowField Pool from {oldMax} to {newMax}");

            NativeArray<float2> newPool = new NativeArray<float2>(newMax * _totalCells, Allocator.Persistent);

            if (_flowFieldPool.IsCreated)
            {
                NativeArray<float2>.Copy(_flowFieldPool, newPool, oldMax * _totalCells);
                _flowFieldPool.Dispose();
            }

            _flowFieldPool = newPool;
            _currentMaxFlowFields = newMax;

            for (int i = oldMax; i < newMax; i++)
            {
                _availableFlowIndices.Enqueue(i);
            }
        }

        //扫描地图中所有的障碍物添加到spatial grid中去, 包括建筑
        //spatial grid和流场中的网络两个之间没有什么关系,流场网络是寻路用的, 而spatial grid是用来查询敌人或者是计算障碍物的排斥作用的
        private void BakeStaticObstacles()
        {
            int totalEntitiesBaked = 0;

            //单个障碍物
            var singleObstacles = FindObjectsOfType<StaticBodyAuthoring>();
            foreach (var obs in singleObstacles)
            {
                if (obs.gs_eleType == WarFieldElements.WarEleType.BUILDING) //建筑添加到spatial grid是在WarEleParent的InitWarEle中调用的
                    continue;

                var entity = obs.BakeToEntitie((byte)_mapId, 0); //只有obstacle
                SpatialGridManager.Instance.AddEntity(entity, null);

                totalEntitiesBaked++;
                if (obs.gs_writeToFlowField == true)
                    _flowFieldBlockers.Add(entity);
            }

            //一片区域
            var polyObstacles = FindObjectsOfType<StaticObstacleAreaAuthoring>();
            foreach (var poly in polyObstacles)
            {
                List<GridEntityData> entities = poly.BakeToEntities((byte)_mapId);
                foreach (var entity in entities)
                {
                    SpatialGridManager.Instance.AddEntity(entity, null);
                    totalEntitiesBaked++;
                }

                poly.gs_polygon.enabled = false; //加到grid中之后禁用collider
                //poly.enabled = false;
                int cnt = entities.Count;
                for (int j = 0; j < cnt; j++)
                    _flowFieldBlockers.Add(entities[j]);
            }

            _blockerCount = _flowFieldBlockers.Length;

            GameLogger.LogDebug($"map {_mapId}, add {totalEntitiesBaked} Obstables into grid");
        }

        //生成流场地图
        private void ScheduleFlowFieldJobs()
        {
            // 强制等待上一帧可能没跑完的流场重建
            _flowFieldJobHandle.Complete();

            //用极速内存拷贝，直接把已经闭合并填充好内部空间的静态铁板数据拉过来
            _costMap.CopyFrom(_staticCostMap);

            // 重建代价场的 Job只需要以极低开销把这几座动态建造的防御塔刷成 255 即可！
            var buildCostJob = new BuildDynamicCostMapJob
            {
                p_costMap = _costMap,
                p_dynamicBlockers = _flowFieldBlockers.AsArray(), // 将 NativeList 作为 Array 传进去
                p_mapId = _mapId,
                p_origin = _origin,
                p_cellSize = _cellSize,
                p_cols = _cols,
                p_rows = _rows,
                p_obstacleTolerance = _obstacleTolerance
            };
            JobHandle costMapHandle = buildCostJob.Schedule();

            JobHandle flowWriteChain = costMapHandle;

            int cellCastleFriendly = _castleFriendly != null ? WorldToCellIndex(_castleFriendly.position) : -1;
            int cellCastleEnemy = _castleEnemy != null ? WorldToCellIndex(_castleEnemy.position) : -1;

            int rawSplitColA = Mathf.FloorToInt(_friendlyDesX / _cellSize);
            int rawSplitColB = Mathf.FloorToInt(_enemyDesX / _cellSize);

            int splitA = Mathf.Clamp(rawSplitColA, 0, _cols - 1);
            int splitB = Mathf.Clamp(rawSplitColB, 0, _cols - 1);

            // 向右的边缘流场
            // 指向 _flowFieldPool 索引 0 的区段
            var jobA_Edge = new CalcFlowFieldJob
            {
                p_costMap = _costMap,
                p_flowFieldSlice = new NativeSlice<float2>(_flowFieldPool, WE.FriendlyFlowFieldDefaultId * _totalCells, _totalCells),
                p_cols = _cols,
                p_rows = _rows,
                p_totalCells = _totalCells,
                p_startC = 0,
                p_endC = (rawSplitColA >= _cols - 1) ? _cols - 1 : splitA,
                p_pushDir = 1,
                p_targetIndex = -1,
                p_targetIndices = _emptyTargetIndices,
                p_defaultDir = new float2(1, 0)
            };
            // 向左的边缘流场
            // 指向 _flowFieldPool 索引 1 的区段
            var jobB_Edge = new CalcFlowFieldJob
            {
                p_costMap = _costMap,
                p_flowFieldSlice = new NativeSlice<float2>(_flowFieldPool, WE.EnemyFlowFieldDefaultId * _totalCells, _totalCells),
                p_cols = _cols,
                p_rows = _rows,
                p_totalCells = _totalCells,
                p_startC = (rawSplitColB <= 0) ? 0 : splitB, p_endC = _cols - 1,
                p_pushDir = -1,
                p_targetIndex = -1,
                p_targetIndices = _emptyTargetIndices,
                p_defaultDir = new float2(-1, 0)
            };

            //边缘流场
            flowWriteChain = jobA_Edge.Schedule(flowWriteChain);
            flowWriteChain = jobB_Edge.Schedule(flowWriteChain);

            //聚拢流场
            // 向右的聚拢流场
            if (rawSplitColA < _cols - 1)
            {
                var jobA_Conv = new CalcFlowFieldJob
                {
                    p_costMap = _costMap,
                    p_flowFieldSlice = new NativeSlice<float2>(_flowFieldPool, WE.FriendlyFlowFieldDefaultId * _totalCells, _totalCells),
                    p_cols = _cols,
                    p_rows = _rows,
                    p_startC = splitA + 1,
                    p_totalCells = _totalCells,
                    p_endC = _cols - 1,
                    p_pushDir = 0,
                    p_targetIndex = cellCastleEnemy,
                    p_targetIndices = _emptyTargetIndices,
                    p_defaultDir = new float2(-1, 0)
                };
                flowWriteChain = jobA_Conv.Schedule(flowWriteChain);
            }

            // 向左的聚拢流场
            if (rawSplitColB > 0)
            {
                var jobB_Conv = new CalcFlowFieldJob
                {
                    p_costMap = _costMap,
                    p_flowFieldSlice = new NativeSlice<float2>(_flowFieldPool, WE.EnemyFlowFieldDefaultId * _totalCells, _totalCells),
                    p_cols = _cols,
                    p_rows = _rows,
                    p_totalCells = _totalCells,
                    p_startC = 0,
                    p_endC = splitB - 1,
                    p_pushDir = 0,
                    p_targetIndex = cellCastleFriendly,
                    p_targetIndices = _emptyTargetIndices,
                    p_defaultDir = new float2(1, 0)
                };
                flowWriteChain = jobB_Conv.Schedule(flowWriteChain);
            }

            // 计算所有正在活跃的局部流场
            foreach (var kvp in _activeLocalFlows)
            {
                int activeFlowIndex = kvp.Key;
                int cellIndex = kvp.Value;

                var localJob = new CalcFlowFieldJob
                {
                    p_costMap = _costMap,
                    p_flowFieldSlice = new NativeSlice<float2>(_flowFieldPool, activeFlowIndex * _totalCells, _totalCells),
                    p_cols = _cols, p_rows = _rows,
                    p_startC = 0, p_endC = _cols - 1,
                    p_totalCells = _totalCells,
                    p_pushDir = 0, // 聚拢模式
                    p_targetIndex = cellIndex,
                    p_targetIndices = _emptyTargetIndices,
                    p_defaultDir = float2.zero
                };

                // 与其他流场计算串行，避免并发写同一个底层 NativeArray 触发 safety 异常
                flowWriteChain = localJob.Schedule(flowWriteChain);
            }

            // HomeFlowField：多源流场，供 farmer GOBACK 使用（指向所有已注册的城镇中心）
            if (_homeTargetIndices.IsCreated && _homeTargetIndices.Length > 0)
            {
                var homeJob = new CalcFlowFieldJob
                {
                    p_costMap = _costMap,
                    p_flowFieldSlice = new NativeSlice<float2>(_flowFieldPool, WE.HomeFlowFieldId * _totalCells, _totalCells),
                    p_cols = _cols, p_rows = _rows, p_totalCells = _totalCells,
                    p_startC = 0, p_endC = _cols - 1,
                    p_pushDir = 0,
                    p_targetIndex = -1,
                    p_targetIndices = _homeTargetIndices.AsArray(),
                    p_defaultDir = float2.zero
                };
                flowWriteChain = homeJob.Schedule(flowWriteChain);
            }

            // 士兵的移动 Job 依赖整个流场写链结束
            _flowFieldJobHandle = flowWriteChain;
        }

        private void DrawArrows(NativeSlice<float2> fieldSlice)
        {
            for (int i = 0; i < _cols; i++)
            {
                for (int j = 0; j < _rows; j++)
                {
                    float2 d = fieldSlice[j * _cols + i];
                    if (math.lengthsq(d) < 0.01f) continue;

                    Vector3 center = new Vector3(_origin.x + i * _cellSize + _cellSize * 0.5f, _origin.y + j * _cellSize + _cellSize * 0.5f, 0);
                    Vector3 endPos = center + new Vector3(d.x, d.y, 0) * _cellSize * 0.45f;
                    Gizmos.DrawLine(center, endPos);

                    Vector3 right = Quaternion.Euler(0, 0, 160) * (endPos - center);
                    Vector3 left = Quaternion.Euler(0, 0, -160) * (endPos - center);
                    Gizmos.DrawLine(endPos, endPos + right * 0.3f);
                    Gizmos.DrawLine(endPos, endPos + left * 0.3f);
                }
            }
        }

        // DDA (Digital Differential Analyzer) 算法：在网格中精确画一条线，检测是否碰到障碍物
        //用来平滑a*生成的路径
        private bool CheckLineOfSight(Vector2 p1, Vector2 p2)
        {
            // 将世界坐标转换为相对于 _origin 的局部坐标
            Vector2 localP1 = p1 - (Vector2)_origin;
            Vector2 localP2 = p2 - (Vector2)_origin;

            // 计算起点和终点的网格坐标
            int x0 = Mathf.FloorToInt(localP1.x / _cellSize);
            int y0 = Mathf.FloorToInt(localP1.y / _cellSize);
            int x1 = Mathf.FloorToInt(localP2.x / _cellSize);
            int y1 = Mathf.FloorToInt(localP2.y / _cellSize);

            // 如果起点和终点在同一个格子里，肯定不会被挡住
            if (x0 == x1 && y0 == y1) return true;

            float dx = localP2.x - localP1.x;
            float dy = localP2.y - localP1.y;

            int stepX = (dx > 0) ? 1 : ((dx < 0) ? -1 : 0);
            int stepY = (dy > 0) ? 1 : ((dy < 0) ? -1 : 0);

            // tMaxX/Y: 射线从当前位置走到下一个网格边界所需的参数 t
            // tDeltaX/Y: 每跨越一个完整的网格，参数 t 需要增加的值
            float tMaxX = (stepX != 0) ? (((x0 + (stepX > 0 ? 1 : 0)) * _cellSize) - localP1.x) / dx : float.MaxValue;
            float tMaxY = (stepY != 0) ? (((y0 + (stepY > 0 ? 1 : 0)) * _cellSize) - localP1.y) / dy : float.MaxValue;

            float tDeltaX = (stepX != 0) ? (_cellSize / Mathf.Abs(dx)) : float.MaxValue;
            float tDeltaY = (stepY != 0) ? (_cellSize / Mathf.Abs(dy)) : float.MaxValue;

            int currentX = x0;
            int currentY = y0;

            // 防死循环保险
            int maxDist = Mathf.Abs(x1 - x0) + Mathf.Abs(y1 - y0) + 1;

            for (int i = 0; i <= maxDist; i++)
            {
                // 检查当前格子是否是障碍物 (255)
                if (currentX >= 0 && currentX < _cols && currentY >= 0 && currentY < _rows)
                {
                    if (_costMap[currentY * _cols + currentX] == 255)
                    {
                        return false; // 视线被墙挡住
                    }
                }

                // 已经到达终点网格，视线畅通
                if (currentX == x1 && currentY == y1) break;

                // 决定射线下一步是跨越 X 边界还是 Y 边界
                if (tMaxX < tMaxY)
                {
                    tMaxX += tDeltaX;
                    currentX += stepX;
                }
                else
                {
                    tMaxY += tDeltaY;
                    currentY += stepY;
                }
            }

            return true; // 视线畅通，可以直接直线走过去
        }

        // A*路径平滑（拉线算法）
        private List<Vector2> SmoothPath(List<Vector2> rawPath)
        {
            // 如果路径点少于3个，无需平滑
            if (rawPath == null || rawPath.Count <= 2) return rawPath;

            List<Vector2> smoothedPath = new List<Vector2>();
            smoothedPath.Add(rawPath[0]); // 起点永远保留

            int currentIndex = 0;
            while (currentIndex < rawPath.Count - 1)
            {
                int furthestVisibleIndex = currentIndex + 1;

                // 从当前点出发，尽可能往后找能“直线看见”的最远点
                for (int i = currentIndex + 2; i < rawPath.Count; i++)
                {
                    if (CheckLineOfSight(rawPath[currentIndex], rawPath[i]))
                    {
                        furthestVisibleIndex = i; // 视线通畅，记录这个更远的点
                    }
                    else
                    {
                        break; // 一旦被挡住，说明拐角到了，停止探测
                    }
                }

                // 把能看到的最远点加入新路径，并从那里继续拉线
                smoothedPath.Add(rawPath[furthestVisibleIndex]);
                currentIndex = furthestVisibleIndex;
            }

            return smoothedPath;
        }

        // 判断玩家点击的位置是否可达, 如果不可达就按照广度优先的算法(BSF))查找最近的可达cell
        private int FindNearestWalkableCell(int startC, int startR, int maxRadius = 20)
        {
            // 如果起点本身就合法，直接返回
            int startIndex = startR * _cols + startC;
            if (startIndex >= 0 && startIndex < _totalCells && _costMap[startIndex] != 255)
                return startIndex;

            Queue<int2> queue = new Queue<int2>();
            HashSet<int> visited = new HashSet<int>();

            queue.Enqueue(new int2(startC, startR));
            visited.Add(startIndex);

            while (queue.Count > 0)
            {
                int2 curr = queue.Dequeue();

                // 超出最大搜索半径，提前放弃，防止性能浪费
                if (math.abs(curr.x - startC) > maxRadius || math.abs(curr.y - startR) > maxRadius)
                    continue;

                for (int i = 0; i < 8; i++)
                {
                    int2 n = curr + _bsfDirs[i];
                    if (n.x >= 0 && n.x < _cols && n.y >= 0 && n.y < _rows)
                    {
                        int nIdx = CellCoordToIndex(n);
                        if (!visited.Contains(nIdx))
                        {
                            if (_costMap[nIdx] != 255)
                                return nIdx; // 找到了最近的有效点

                            visited.Add(nIdx);
                            queue.Enqueue(n);
                        }
                    }
                }
            }
            return -1; // 极大概率不会发生，除非整个地图都被填满了
        }

        private int GetHeuristic(int2 a, int2 b)
        {
            int dx = math.abs(a.x - b.x);
            int dy = math.abs(a.y - b.y);
            return 10 * (dx + dy) + (14 - 2 * 10) * math.min(dx, dy);
        }

        private int WorldToCellIndex(Vector3 p)
        {
            int c = Mathf.Clamp(Mathf.FloorToInt((p.x - _origin.x) / _cellSize), 0, _cols - 1);
            int r = Mathf.Clamp(Mathf.FloorToInt((p.y - _origin.y) / _cellSize), 0, _rows - 1);
            return r * _cols + c;
        }

        private int2 WorldToCellCoord(Vector3 p)
        {
            int c = Mathf.Clamp(Mathf.FloorToInt((p.x - _origin.x) / _cellSize), 0, _cols - 1);
            int r = Mathf.Clamp(Mathf.FloorToInt((p.y - _origin.y) / _cellSize), 0, _rows - 1);
            return new int2(c, r);
        }

        private int2 CellIndexToCoord(int index)
        {
            return new int2(index % _cols, index / _cols);
        }

        private int CellCoordToIndex(int2 coord)
        {
            return coord.y * _cols + coord.x;
        }

#endregion
    }

    [BurstCompile(CompileSynchronously = true)]
    public struct CalcFlowFieldJob : IJob
    {
        [ReadOnly] public NativeArray<byte> p_costMap;

        // 使用 NativeSlice：只允许操作被切片划定的一段内存，因此不用声明 DisableParallelForRestriction
        public NativeSlice<float2> p_flowFieldSlice;

        public int p_cols;
        public int p_rows;
        public int p_startC;
        public int p_endC;
        public int p_totalCells;

        public int p_pushDir;     // 1 向右, -1 向左
        public int p_targetIndex; // -1 代表推线，否则是目标建筑格子的一维索引
        [ReadOnly] public NativeArray<int> p_targetIndices; // 多源目标索引（用于 HomeFlowField）
        public float2 p_defaultDir;

        public void Execute()
        {
            if (p_startC > p_endC)
                return;

            NativeArray<ushort> intField = new NativeArray<ushort>(p_totalCells, Allocator.Temp);
            NativeQueue<int> queue = new NativeQueue<int>(Allocator.Temp);

            // 初始化距离为最大值
            for (int i = 0; i < p_totalCells; i++)
                intField[i] = ushort.MaxValue;

            // 找到终点并入队
            if (p_targetIndices.IsCreated && p_targetIndices.Length > 0)
            {
                // 多源模式：所有城镇中心格子作为起始波前
                for (int i = 0; i < p_targetIndices.Length; i++)
                {
                    int tIdx = p_targetIndices[i];
                    if (tIdx >= 0 && tIdx < p_totalCells && p_costMap[tIdx] != 255)
                    {
                        intField[tIdx] = 0;
                        queue.Enqueue(tIdx);
                    }
                }
                p_defaultDir = float2.zero; // 多源辐射全图，无需朝向兜底
            }
            else if (p_targetIndex == -1)// 没有终点建筑,终点就是最后一列
            {
                int targetCol = (p_pushDir == 1) ? p_endC : p_startC;
                for (int r = 0; r < p_rows; r++)
                {
                    int index = r * p_cols + targetCol;
                    if (p_costMap[index] != 255)
                    {
                        intField[index] = 0;
                        queue.Enqueue(index);
                    }
                }
            }
            else
            {
                // 聚拢流场：终点是一个特定的位置
                int tc = p_targetIndex % p_cols;
                int tr = p_targetIndex / p_cols;
                if (tc >= p_startC && tc <= p_endC && p_costMap[p_targetIndex] != 255)
                {
                    intField[p_targetIndex] = 0;
                    queue.Enqueue(p_targetIndex);
                }
                else
                {
                    // 若目标在墙里或越界，计算备用朝向
                    p_defaultDir = math.normalize(new float2(tc - (p_startC + p_endC) / 2f, 0));
                }
            }

            // Dijkstra 波前蔓延
            NativeArray<int> offsetC = new NativeArray<int>(8, Allocator.Temp) { [0] = 0, [1] = 0, [2] = 1, [3] = -1, [4] = 1, [5] = 1, [6] = -1, [7] = -1 };
            NativeArray<int> offsetR = new NativeArray<int>(8, Allocator.Temp) { [0] = 1, [1] = -1, [2] = 0, [3] = 0, [4] = 1, [5] = -1, [6] = 1, [7] = -1 };
            NativeArray<ushort> moveCosts = new NativeArray<ushort>(8, Allocator.Temp) { [0] = 10, [1] = 10, [2] = 10, [3] = 10, [4] = 14, [5] = 14, [6] = 14, [7] = 14 };

            while (queue.TryDequeue(out int currIndex))
            {
                int currC = currIndex % p_cols;
                int currR = currIndex / p_cols;
                ushort currDist = intField[currIndex];

                for (int i = 0; i < 8; i++)
                {
                    int nc = currC + offsetC[i];
                    int nr = currR + offsetR[i];

                    if (nc >= p_startC && nc <= p_endC && nr >= 0 && nr < p_rows)
                    {
                        int nIndex = nr * p_cols + nc;
                        if (p_costMap[nIndex] == 255)
                            continue;

                        if (i >= 4)  //防止对角线穿墙
                        {
                            int block1Index = currR * p_cols + nc; // 水平相邻格子
                            int block2Index = nr * p_cols + currC; // 垂直相邻格子

                            // 如果两个直角方向都被墙堵死，说明这是一个对角线死角，不允许穿透
                            if (p_costMap[block1Index] == 255 && p_costMap[block2Index] == 255)
                            {
                                continue;
                            }
                        }

                        ushort newDist = (ushort)(currDist + p_costMap[nIndex] * moveCosts[i]);
                        if (newDist < intField[nIndex])
                        {
                            intField[nIndex] = newDist;
                            queue.Enqueue(nIndex);
                        }
                    }
                }
            }

            // 计算平滑梯度向量并直接写入输出数组
            for (int c = p_startC; c <= p_endC; c++)
            {
                for (int r = 0; r < p_rows; r++)
                {
                    int index = r * p_cols + c;
                    if (p_costMap[index] == 255)
                    {
                        // 写入 NativeSlice 时，索引就是 0 ~ totalCells-1
                        p_flowFieldSlice[index] = float2.zero; // 墙里没方向
                        continue;
                    }

                    ushort myCost = intField[index];

                    // 如果这个格子根本无法到达目标点（比如被死胡同/全封闭的墙包围的内部空间）！
                    // 或者处于流场未触及的极远端区域
                    // 局部流场不能给零向量，要通过径向大方向把远处的单位“牵引”过来
                    if (myCost == ushort.MaxValue)
                    {
                        if (p_targetIndex != -1)
                        {
                            int tc = p_targetIndex % p_cols;
                            int tr = p_targetIndex / p_cols;
                            p_flowFieldSlice[index] = math.normalizesafe(new float2(tc - c, tr - r));
                        }
                        else
                        {
                            p_flowFieldSlice[index] = float2.zero; // 全局推线无法到达的死角保持不动
                        }
                        continue;
                    }

                    // 初始化四个方向的cost为当前中心点的cost
                    ushort costLeft  = myCost;
                    ushort costRight = myCost;
                    ushort costDown  = myCost;
                    ushort costUp    = myCost;

                    // 获取左侧cost
                    if (c - 1 >= p_startC)
                    {
                        int idx = r * p_cols + (c - 1);
                        if (p_costMap[idx] != 255 && intField[idx] != ushort.MaxValue)
                            costLeft = intField[idx];
                    }
                    // 获取右侧cost
                    if (c + 1 <= p_endC)
                    {
                        int idx = r * p_cols + (c + 1);
                        if (p_costMap[idx] != 255 && intField[idx] != ushort.MaxValue)
                            costRight = intField[idx];
                    }
                    // 获取下方cost
                    if (r - 1 >= 0)
                    {
                        int idx = (r - 1) * p_cols + c;
                        if (p_costMap[idx] != 255 && intField[idx] != ushort.MaxValue)
                            costDown = intField[idx];
                    }
                    // 获取上方cost
                    if (r + 1 < p_rows)
                    {
                        int idx = (r + 1) * p_cols + c;
                        if (p_costMap[idx] != 255 && intField[idx] != ushort.MaxValue)
                            costUp = intField[idx];
                    }

                    // 计算平滑梯度向量
                    float dx = costLeft - costRight;
                    float dy = costDown - costUp;

                    if (dx == 0 && dy == 0)
                    {
                        // 修复问题三 (B部分)：在开阔平坦平原或死角，梯度消失时
                        // 局部流场利用目标方向作为保底，拒绝发呆
                        if (p_targetIndex != -1)
                        {
                            int tc = p_targetIndex % p_cols;
                            int tr = p_targetIndex / p_cols;
                            p_flowFieldSlice[index] = math.normalizesafe(new float2(tc - c, tr - r));
                        }
                        else
                        {
                            p_flowFieldSlice[index] = p_defaultDir; // 全局推线保留原样
                        }
                    }
                    else
                        p_flowFieldSlice[index] = math.normalize(new float2(dx, dy));
                }
            }

            // 清理临时内存
            queue.Dispose();
            intField.Dispose();
            offsetC.Dispose();
            offsetR.Dispose();
            moveCosts.Dispose();
        }
    }

    // Init 时运行一次。负责铺设静态边界 + 逆向边界泛洪
    [BurstCompile(CompileSynchronously = true)]
    public struct BakeStaticCostMapJob : IJob
    {
        public NativeArray<byte> p_staticCostMap;
        [ReadOnly] public NativeArray<GridEntityData> p_blockers;

        public int p_mapId;
        public float2 p_origin;
        public float p_cellSize;
        public int p_cols;
        public int p_rows;
        public float p_obstacleTolerance;

        public void Execute()
        {
            // 0 统一代表尚未被泛洪滋润到的未知格子
            for (int i = 0; i < p_staticCostMap.Length; i++)
                p_staticCostMap[i] = 0;

            // 步骤一：将多边形组件生成的边缘外壳圈、以及普通石头刷成 255 阻挡墙
            for (int i = 0; i < p_blockers.Length; i++)
            {
                GridEntityData entity = p_blockers[i];
                if (entity.p_mapId != p_mapId) continue;

                float r = entity.p_radius;
                int minC = math.max(0, (int)math.floor((entity.p_position.x - r - p_origin.x) / p_cellSize));
                int maxC = math.min(p_cols - 1, (int)math.floor((entity.p_position.x + r - p_origin.x) / p_cellSize));
                int minR = math.max(0, (int)math.floor((entity.p_position.y - r - p_origin.y) / p_cellSize));
                int maxR = math.min(p_rows - 1, (int)math.floor((entity.p_position.y + r - p_origin.y) / p_cellSize));

                float sqrRadius = r * r;

                for (int c = minC; c <= maxC; c++)
                {
                    for (int rCell = minR; rCell <= maxR; rCell++)
                    {
                        float cellMinX = p_origin.x + c * p_cellSize + p_obstacleTolerance;
                        float cellMaxX = p_origin.x + (c + 1) * p_cellSize - p_obstacleTolerance;
                        float cellMinY = p_origin.y + rCell * p_cellSize + p_obstacleTolerance;
                        float cellMaxY = p_origin.y + (rCell + 1) * p_cellSize - p_obstacleTolerance;

                        float closestX = math.clamp(entity.p_position.x, cellMinX, cellMaxX);
                        float closestY = math.clamp(entity.p_position.y, cellMinY, cellMaxY);

                        float dx = entity.p_position.x - closestX;
                        float dy = entity.p_position.y - closestY;

                        if (dx * dx + dy * dy <= sqrRadius)
                        {
                            p_staticCostMap[rCell * p_cols + c] = 255;
                        }
                    }
                }
            }

            // 步骤二：逆向泛洪算法。将地图最外侧的 4 条大边缘上所有可通行的空地格塞入队列作为水源
            NativeQueue<int2> fillQueue = new NativeQueue<int2>(Allocator.Temp);

            for (int c = 0; c < p_cols; c++)
            {
                int idxBottom = 0 * p_cols + c;
                if (p_staticCostMap[idxBottom] == 0) { p_staticCostMap[idxBottom] = 1; fillQueue.Enqueue(new int2(c, 0)); }

                int idxTop = (p_rows - 1) * p_cols + c;
                if (p_staticCostMap[idxTop] == 0) { p_staticCostMap[idxTop] = 1; fillQueue.Enqueue(new int2(c, p_rows - 1)); }
            }
            for (int r = 0; r < p_rows; r++)
            {
                int idxLeft = r * p_cols + 0;
                if (p_staticCostMap[idxLeft] == 0) { p_staticCostMap[idxLeft] = 1; fillQueue.Enqueue(new int2(0, r)); }

                int idxRight = r * p_cols + (p_cols - 1);
                if (p_staticCostMap[idxRight] == 0) { p_staticCostMap[idxRight] = 1; fillQueue.Enqueue(new int2(p_cols - 1, r)); }
            }

            // 【核心重构修复位置】：通过展开 4 邻居逻辑，彻底斩断 managed 数组的分配
            while (fillQueue.TryDequeue(out int2 curr))
            {
                // 1. 右侧扩展 (curr.x + 1, curr.y)
                if (curr.x + 1 < p_cols)
                {
                    int nIdx = curr.y * p_cols + (curr.x + 1);
                    if (p_staticCostMap[nIdx] == 0)
                    {
                        p_staticCostMap[nIdx] = 1;
                        fillQueue.Enqueue(new int2(curr.x + 1, curr.y));
                    }
                }

                // 2. 左侧扩展 (curr.x - 1, curr.y)
                if (curr.x - 1 >= 0)
                {
                    int nIdx = curr.y * p_cols + (curr.x - 1);
                    if (p_staticCostMap[nIdx] == 0)
                    {
                        p_staticCostMap[nIdx] = 1;
                        fillQueue.Enqueue(new int2(curr.x - 1, curr.y));
                    }
                }

                // 3. 上方扩展 (curr.x, curr.y + 1)
                if (curr.y + 1 < p_rows)
                {
                    int nIdx = (curr.y + 1) * p_cols + curr.x;
                    if (p_staticCostMap[nIdx] == 0)
                    {
                        p_staticCostMap[nIdx] = 1;
                        fillQueue.Enqueue(new int2(curr.x, curr.y + 1));
                    }
                }

                // 4. 下方扩展 (curr.x, curr.y - 1)
                if (curr.y - 1 >= 0)
                {
                    int nIdx = (curr.y - 1) * p_cols + curr.x;
                    if (p_staticCostMap[nIdx] == 0)
                    {
                        p_staticCostMap[nIdx] = 1;
                        fillQueue.Enqueue(new int2(curr.x, curr.y - 1));
                    }
                }
            }

            // 步骤三：大收网！那些直到最后都没有被水流滋润到、值依然为 0 的格子
            // 必然是被 255 外壳墙体彻底封闭起来的“内部虚无空间”，无条件批量格式化为 255 铁板障碍！
            for (int i = 0; i < p_staticCostMap.Length; i++)
            {
                if (p_staticCostMap[i] == 0)
                {
                    p_staticCostMap[i] = 255;
                }
            }
            fillQueue.Dispose();
        }
    }

    // 仅在建筑改变时运行。只迭代个位数的动态建筑，开销极低
    [BurstCompile(CompileSynchronously = true)]
    public struct BuildDynamicCostMapJob : IJob
    {
        public NativeArray<byte> p_costMap;
        [ReadOnly] public NativeArray<GridEntityData> p_dynamicBlockers;

        public int p_mapId;
        public float2 p_origin;
        public float p_cellSize;
        public int p_cols;
        public int p_rows;
        public float p_obstacleTolerance;

        public void Execute()
        {
            // 注意：此时 p_costMap 里已经躺着由老地图单帧 Copy 复制过来的实心静态障碍物数据了，
            // 这里只需要顺手把新修的几座动态防御塔的位置改写为 255 即可。
            for (int i = 0; i < p_dynamicBlockers.Length; i++)
            {
                GridEntityData entity = p_dynamicBlockers[i];
                if (entity.p_mapId != p_mapId) continue;

                float r = entity.p_radius;
                int minC = math.max(0, (int)math.floor((entity.p_position.x - r - p_origin.x) / p_cellSize));
                int maxC = math.min(p_cols - 1, (int)math.floor((entity.p_position.x + r - p_origin.x) / p_cellSize));
                int minR = math.max(0, (int)math.floor((entity.p_position.y - r - p_origin.y) / p_cellSize));
                int maxR = math.min(p_rows - 1, (int)math.floor((entity.p_position.y + r - p_origin.y) / p_cellSize));

                float sqrRadius = r * r;

                for (int c = minC; c <= maxC; c++)
                {
                    for (int rCell = minR; rCell <= maxR; rCell++)
                    {
                        float cellMinX = p_origin.x + c * p_cellSize + p_obstacleTolerance;
                        float cellMaxX = p_origin.x + (c + 1) * p_cellSize - p_obstacleTolerance;
                        float cellMinY = p_origin.y + rCell * p_cellSize + p_obstacleTolerance;
                        float cellMaxY = p_origin.y + (rCell + 1) * p_cellSize - p_obstacleTolerance;

                        float closestX = math.clamp(entity.p_position.x, cellMinX, cellMaxX);
                        float closestY = math.clamp(entity.p_position.y, cellMinY, cellMaxY);

                        float dx = entity.p_position.x - closestX;
                        float dy = entity.p_position.y - closestY;

                        if (dx * dx + dy * dy <= sqrRadius)
                        {
                            p_costMap[rCell * p_cols + c] = 255;
                        }
                    }
                }
            }
        }
    }
    [BurstCompile(CompileSynchronously = true)]
    public struct BuildCostMapJob : IJob
    {
        public NativeArray<byte> p_costMap;
        [ReadOnly] public NativeArray<GridEntityData> p_blockers;

        public int p_mapId;
        public float2 p_origin;
        public float p_cellSize;
        public int p_cols;
        public int p_rows;
        public float p_obstacleTolerance;

        public void Execute()
        {
            for (int i = 0; i < p_costMap.Length; i++)
            {
                p_costMap[i] = 1;
            }

            //遍历所有静态障碍物并写入 255
            for (int i = 0; i < p_blockers.Length; i++)
            {
                GridEntityData entity = p_blockers[i];

                if (entity.p_mapId != p_mapId)
                    continue;

                float r = entity.p_radius;
                int minC = math.max(0, (int)math.floor((entity.p_position.x - r - p_origin.x) / p_cellSize));
                int maxC = math.min(p_cols - 1, (int)math.floor((entity.p_position.x + r - p_origin.x) / p_cellSize));
                int minR = math.max(0, (int)math.floor((entity.p_position.y - r - p_origin.y) / p_cellSize));
                int maxR = math.min(p_rows - 1, (int)math.floor((entity.p_position.y + r - p_origin.y) / p_cellSize));

                float sqrRadius = r * r;

                for (int c = minC; c <= maxC; c++)
                {
                    for (int rCell = minR; rCell <= maxR; rCell++)
                    {
                        float cellMinX = p_origin.x + c * p_cellSize + p_obstacleTolerance;
                        float cellMaxX = p_origin.x + (c + 1) * p_cellSize - p_obstacleTolerance;
                        float cellMinY = p_origin.y + rCell * p_cellSize + p_obstacleTolerance;
                        float cellMaxY = p_origin.y + (rCell + 1) * p_cellSize - p_obstacleTolerance;

                        float closestX = math.clamp(entity.p_position.x, cellMinX, cellMaxX);
                        float closestY = math.clamp(entity.p_position.y, cellMinY, cellMaxY);

                        float dx = entity.p_position.x - closestX;
                        float dy = entity.p_position.y - closestY;

                        if (dx * dx + dy * dy <= sqrRadius)
                        {
                            p_costMap[rCell * p_cols + c] = 255;
                        }
                    }
                }
            }
        }
    }

    // ================== 异步 A* 批处理 Job ==================
    [BurstCompile(CompileSynchronously = true)]
    public struct AStarBatchJob : IJob
    {
        [ReadOnly] public NativeArray<byte> costMap;
        [ReadOnly] public NativeArray<float2> startPoints;
        [ReadOnly] public NativeArray<float2> endPoints;
        public int cols, rows, totalCells;
        public float2 origin;
        public float cellSize;

        public NativeList<float2> allPaths;
        public NativeArray<int2> pathOffsets; // x: startIndex in allPaths, y: length

        public void Execute()
        {
            NativeArray<int> parentMap = new NativeArray<int>(totalCells, Allocator.Temp);
            NativeArray<int> gCostMap = new NativeArray<int>(totalCells, Allocator.Temp);
            NativeArray<byte> stateMap = new NativeArray<byte>(totalCells, Allocator.Temp); // 0: unvisited, 1: open, 2: closed
            NativeList<int2> openList = new NativeList<int2>(1024, Allocator.Temp); // x: index, y: F-cost
            NativeList<float2> rawPath = new NativeList<float2>(256, Allocator.Temp);
            NativeList<float2> smoothedPath = new NativeList<float2>(256, Allocator.Temp);

            NativeArray<int2> neighbors = new NativeArray<int2>(8, Allocator.Temp);
            neighbors[0] = new int2(0, 1); neighbors[1] = new int2(0, -1);
            neighbors[2] = new int2(1, 0); neighbors[3] = new int2(-1, 0);
            neighbors[4] = new int2(1, 1); neighbors[5] = new int2(1, -1);
            neighbors[6] = new int2(-1, 1); neighbors[7] = new int2(-1, -1);

            NativeArray<int> moveCosts = new NativeArray<int>(8, Allocator.Temp);
            for (int i = 0; i < 4; i++) moveCosts[i] = 10;
            for (int i = 4; i < 8; i++) moveCosts[i] = 14;

            for (int req = 0; req < startPoints.Length; req++)
            {
                for (int si = 0; si < totalCells; si++) stateMap[si] = 0;
                openList.Clear();
                rawPath.Clear();
                smoothedPath.Clear();

                float2 startPos = startPoints[req];
                float2 endPos = endPoints[req];

                int2 startGrid = new int2(
                    math.clamp((int)math.floor((startPos.x - origin.x) / cellSize), 0, cols - 1),
                    math.clamp((int)math.floor((startPos.y - origin.y) / cellSize), 0, rows - 1)
                );
                int2 endGrid = new int2(
                    math.clamp((int)math.floor((endPos.x - origin.x) / cellSize), 0, cols - 1),
                    math.clamp((int)math.floor((endPos.y - origin.y) / cellSize), 0, rows - 1)
                );

                if (startGrid.x == endGrid.x && startGrid.y == endGrid.y)
                {
                    WritePath(req, endPos, ref smoothedPath);
                    continue;
                }

                int startIndex = startGrid.y * cols + startGrid.x;
                gCostMap[startIndex] = 0;
                stateMap[startIndex] = 1;
                openList.Add(new int2(startIndex, GetHeuristic(startGrid, endGrid)));

                int maxIterations = 1500;
                int finalTargetIdx = -1;

                while (openList.Length > 0 && maxIterations-- > 0)
                {
                    int bestIdxInList = 0;
                    int minF = openList[0].y;
                    for (int i = 1; i < openList.Length; i++)
                    {
                        if (openList[i].y < minF) { minF = openList[i].y; bestIdxInList = i; }
                    }

                    int currIndex = openList[bestIdxInList].x;
                    openList.RemoveAtSwapBack(bestIdxInList);
                    stateMap[currIndex] = 2; // Closed

                    int2 currPos = new int2(currIndex % cols, currIndex / cols);
                    if (currPos.x == endGrid.x && currPos.y == endGrid.y)
                    {
                        finalTargetIdx = currIndex;
                        break;
                    }

                    for (int i = 0; i < 8; i++)
                    {
                        int2 nPos = currPos + neighbors[i];
                        if (nPos.x < 0 || nPos.x >= cols || nPos.y < 0 || nPos.y >= rows) continue;

                        int nIndex = nPos.y * cols + nPos.x;
                        if (stateMap[nIndex] == 2 || costMap[nIndex] == 255) continue;

                        if (i >= 4) // 穿墙保护
                        {
                            int cross1 = currPos.y * cols + nPos.x;
                            int cross2 = nPos.y * cols + currPos.x;
                            if (costMap[cross1] == 255 && costMap[cross2] == 255) continue;
                        }

                        int tentativeG = gCostMap[currIndex] + moveCosts[i];
                        if (stateMap[nIndex] == 0 || tentativeG < gCostMap[nIndex])
                        {
                            parentMap[nIndex] = currIndex;
                            gCostMap[nIndex] = tentativeG;
                            int fCost = tentativeG + GetHeuristic(nPos, endGrid);
                            if (stateMap[nIndex] == 0)
                            {
                                stateMap[nIndex] = 1;
                                openList.Add(new int2(nIndex, fCost));
                            }
                            else
                            {
                                for (int j = 0; j < openList.Length; j++)
                                {
                                    if (openList[j].x == nIndex) { openList[j] = new int2(nIndex, fCost); break; }
                                }
                            }
                        }
                    }
                }

                if (finalTargetIdx != -1)
                {
                    int curr = finalTargetIdx;
                    while (curr != startIndex)
                    {
                        int2 p = new int2(curr % cols, curr / cols);
                        rawPath.Add(new float2(origin.x + p.x * cellSize + cellSize * 0.5f, origin.y + p.y * cellSize + cellSize * 0.5f));
                        curr = parentMap[curr];
                    }

                    // 反转路径
                    for (int i = 0; i < rawPath.Length / 2; i++)
                    {
                        float2 temp = rawPath[i];
                        rawPath[i] = rawPath[rawPath.Length - 1 - i];
                        rawPath[rawPath.Length - 1 - i] = temp;
                    }

                    rawPath.Add(float2.zero); // 先扩容1个长度
                    for (int i = rawPath.Length - 1; i > 0; i--)
                    {
                        rawPath[i] = rawPath[i - 1]; // 整体后移一位
                    }

                    rawPath[0] = startPos;
                    SmoothPath(ref rawPath, ref smoothedPath);
                }

                if (smoothedPath.Length > 0)
                {
                    smoothedPath.RemoveAt(0); // 去掉起点（即自身当前位置）
                    WritePath(req, endPos, ref smoothedPath);
                }
                else
                {
                    pathOffsets[req] = new int2(allPaths.Length, 0);
                }
            }

            parentMap.Dispose();
            gCostMap.Dispose();
            stateMap.Dispose();
            openList.Dispose();
            rawPath.Dispose();
            smoothedPath.Dispose();
            neighbors.Dispose();
            moveCosts.Dispose();
        }

        private int GetHeuristic(int2 a, int2 b)
        {
            int dx = math.abs(a.x - b.x);
            int dy = math.abs(a.y - b.y);
            return 10 * (dx + dy) + (14 - 2 * 10) * math.min(dx, dy);
        }

        private void WritePath(int reqIdx, float2 finalPos, ref NativeList<float2> smoothedPath)
        {
            int startIdx = allPaths.Length;
            for (int i = 0; i < smoothedPath.Length; i++)
                allPaths.Add(smoothedPath[i]);
            pathOffsets[reqIdx] = new int2(startIdx, smoothedPath.Length);
        }

        private void SmoothPath(ref NativeList<float2> raw, ref NativeList<float2> smoothed)
        {
            if (raw.Length <= 2)
            {
                for (int i = 0; i < raw.Length; i++) smoothed.Add(raw[i]);
                return;
            }

            smoothed.Add(raw[0]);
            int currentIndex = 0;
            while (currentIndex < raw.Length - 1)
            {
                int furthestVisibleIndex = currentIndex + 1;
                for (int i = currentIndex + 2; i < raw.Length; i++)
                {
                    if (CheckLineOfSight(raw[currentIndex], raw[i]))
                        furthestVisibleIndex = i;
                    else break;
                }
                smoothed.Add(raw[furthestVisibleIndex]);
                currentIndex = furthestVisibleIndex;
            }
        }

        private bool CheckLineOfSight(float2 p1, float2 p2)
        {
            float2 localP1 = p1 - origin;
            float2 localP2 = p2 - origin;

            int x0 = (int)math.floor(localP1.x / cellSize);
            int y0 = (int)math.floor(localP1.y / cellSize);
            int x1 = (int)math.floor(localP2.x / cellSize);
            int y1 = (int)math.floor(localP2.y / cellSize);

            if (x0 == x1 && y0 == y1) return true;

            float dx = localP2.x - localP1.x;
            float dy = localP2.y - localP1.y;

            int stepX = (dx > 0) ? 1 : ((dx < 0) ? -1 : 0);
            int stepY = (dy > 0) ? 1 : ((dy < 0) ? -1 : 0);

            float tMaxX = (stepX != 0) ? (((x0 + (stepX > 0 ? 1 : 0)) * cellSize) - localP1.x) / dx : float.MaxValue;
            float tMaxY = (stepY != 0) ? (((y0 + (stepY > 0 ? 1 : 0)) * cellSize) - localP1.y) / dy : float.MaxValue;
            float tDeltaX = (stepX != 0) ? (cellSize / math.abs(dx)) : float.MaxValue;
            float tDeltaY = (stepY != 0) ? (cellSize / math.abs(dy)) : float.MaxValue;

            int currentX = x0;
            int currentY = y0;
            int maxDist = math.abs(x1 - x0) + math.abs(y1 - y0) + 1;

            for (int i = 0; i <= maxDist; i++)
            {
                if (currentX >= 0 && currentX < cols && currentY >= 0 && currentY < rows)
                {
                    if (costMap[currentY * cols + currentX] == 255) return false;
                }
                if (currentX == x1 && currentY == y1) break;

                if (tMaxX < tMaxY) { tMaxX += tDeltaX; currentX += stepX; }
                else { tMaxY += tDeltaY; currentY += stepY; }
            }
            return true;
        }
    }
}
