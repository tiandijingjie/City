using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace WarField
{
    using RD = WarResDefine;
    using WE = WarFieldElements;

    //不同于SpatialGridManager, 这个是管理地图中所有的可拾取的资源的(曈石...)
    //空间网格(p_resGrid)已统一移入 GridMapContext 由 SpatialGridManager 管理
    //目前在洞穴地图中 敌人不会掉落资源
    public class ResourceGrid : IDisposable, ITask
    {
#region public parameters

        static public ResourceGrid Instance;
#endregion

#region private parameters

        // 时间轮配置
        private const int TIME_WHEEL_SIZE = 512;
        private const int TIME_WHEEL_MASK = 511;

        // 所有资源的数据存在这里，基于索引访问
        private NativeArray<ResEntityData> _resPool;
        private NativeQueue<int> _freeIndices;
        private NativeArray<int> _timeWheel;

        public const int MAX_RESOURCE_COUNT = 4096; // 资源池初始容量，不足时自动翻倍扩容
        private float _cellSize = 2.0f;
        private int _maxResourceCount = MAX_RESOURCE_COUNT;

        private int _currentWheelSlot = 0;
#endregion

#region private parameters' get set

        public NativeArray<ResEntityData> gs_resPool
        {
            get { return _resPool; }
        }

#endregion

#region public functions

        public ResourceGrid()
        {
            if (Instance == null)
                Instance = this;
            else
                GameLogger.LogError("Resource grid instance already exists !!");
        }

        //OnGround map bounds
        public void InitResGrid(Bounds bounds)
        {
            _resPool = new NativeArray<ResEntityData>(_maxResourceCount, Allocator.Persistent);
            _freeIndices = new NativeQueue<int>(Allocator.Persistent);
            _timeWheel = new NativeArray<int>(TIME_WHEEL_SIZE, Allocator.Persistent);

            // 初始化数据池状态
            for (int i = 0; i < _resPool.Length; i++)
            {
                ResEntityData data = _resPool[i];
                data.p_isActive = false;
                data.p_poolIndex = i;
                _resPool[i] = data;
                _freeIndices.Enqueue(i);
            }

            for (int i = 0; i < _timeWheel.Length; i++)
            {
                _timeWheel[i] = -1;
            }

            // 在 SpatialGridManager 的 GridMapContext 中创建资源空间网格
            SpatialGridManager.Instance.InitResGrid(WE.OnGroundMapIndex, bounds, _cellSize);

            WarFieldGameManager.Instance.RegisterTask(this, WE.TaskType.NORMAL);
            WarFieldGameManager.Instance.ActiveTask(this, WE.TaskType.NORMAL, 1); //1s调用一次
        }

        public void RunNormalTask(float deltaTime)
        {
            if (!_resPool.IsCreated)
				return;

            NativeList<int> expiredIndices = new NativeList<int>(Allocator.Temp);
            TickTimeWheel(Time.time, ref expiredIndices);

            WarResCtrl.Instance.HandleExpiredResources(expiredIndices);
            expiredIndices.Dispose();
        }

        public void RunFixTask(float deltaTime)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            if (_resPool.IsCreated)
                _resPool.Dispose();

            if (_freeIndices.IsCreated)
                _freeIndices.Dispose();

            if (_timeWheel.IsCreated)
                _timeWheel.Dispose();
        }

        public int AddResource(float2 pos, int resType, int weight, int value, float expireTimeSecs, float currentTime)
        {
            SearchManager.Instance.CompleteResSearchJobs();
            if (_freeIndices.IsEmpty())
                ExpandResPool();

            int poolIndex = _freeIndices.Dequeue();
            ResEntityData data = _resPool[poolIndex];

            data.p_position = pos;
            data.p_type = (byte)resType;
            data.p_weight = (ushort)weight;
            data.p_value = (ushort)value;
            data.p_expirationTime = currentTime + expireTimeSecs;
            data.p_isTargeted = false;
            data.p_isActive = true;

            int steps = (int)math.ceil(expireTimeSecs);
            int targetSlot = (_currentWheelSlot + steps) & TIME_WHEEL_MASK;

            data.p_nextTimeoutPoolIndex = _timeWheel[targetSlot];
            _timeWheel[targetSlot] = poolIndex;

            _resPool[poolIndex] = data;

            SpatialGridManager.Instance?.MarkResGridDirty(WE.OnGroundMapIndex);
            return poolIndex;
        }

        public void RemoveResource(int poolIndex)
        {
            if (poolIndex < 0 || poolIndex >= _maxResourceCount)
                return;

            SearchManager.Instance.CompleteResSearchJobs();

            ResEntityData data = _resPool[poolIndex];
            data.p_isActive = false;
            data.p_isTargeted = false;
            _resPool[poolIndex] = data;

            _freeIndices.Enqueue(poolIndex);
            SpatialGridManager.Instance?.MarkResGridDirty(WE.OnGroundMapIndex);
        }

        // 锁定资源. 不能再被查询到
        public void SetResourceTargeted(int poolIndex, bool targeted)
        {
            if (poolIndex < 0 || poolIndex >= _maxResourceCount)
                return;

            SearchManager.Instance.CompleteResSearchJobs();

            ResEntityData data = _resPool[poolIndex];
            if (data.p_isActive)
            {
                data.p_isTargeted = targeted;
                _resPool[poolIndex] = data;
                // p_isTargeted 是查询时过滤条件，不影响格子的空间位置，无需重建网格
            }
        }

#endregion

#region private functions

        // 时间轮推进，收集超时的 poolIndex
        private void TickTimeWheel(float currentTime, ref NativeList<int> outExpiredIndices)
        {
            int headPoolIndex = _timeWheel[_currentWheelSlot];
            int currentPoolIndex = headPoolIndex;

            while (currentPoolIndex != -1)
            {
                ResEntityData data = _resPool[currentPoolIndex];
                int nextIndex = data.p_nextTimeoutPoolIndex;

                if (data.p_isActive)
                {
                    if (currentTime >= data.p_expirationTime)
                    {
                        outExpiredIndices.Add(currentPoolIndex);
                    }
                }

                currentPoolIndex = nextIndex;
            }

            _timeWheel[_currentWheelSlot] = -1;
            _currentWheelSlot = (_currentWheelSlot + 1) & TIME_WHEEL_MASK;
        }

        // 资源池容量翻倍（_freeIndices 耗尽时触发）
        // _activeResMap 在 WarResCtrl.SetActiveRes 中同步自动扩容
        private void ExpandResPool()
        {
            int oldSize = _maxResourceCount;
            int newSize = oldSize * 2;
            GameLogger.LogWarning($"[ResourceGrid] Pool full, expanding {oldSize} -> {newSize}");

            var newPool = new NativeArray<ResEntityData>(newSize, Allocator.Persistent);
            NativeArray<ResEntityData>.Copy(_resPool, newPool, oldSize);
            _resPool.Dispose();
            _resPool = newPool;

            for (int i = oldSize; i < newSize; i++)
            {
                ResEntityData data = new ResEntityData();
                data.p_isActive = false;
                data.p_poolIndex = i;
                _resPool[i] = data;
                _freeIndices.Enqueue(i);
            }

            _maxResourceCount = newSize;
            SpatialGridManager.Instance?.MarkResGridDirty(WE.OnGroundMapIndex);
        }
#endregion
    }
}
