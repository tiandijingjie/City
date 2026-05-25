using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace WarField
{
    public interface ICollection
    {
        Transform gs_transform { get; } //就是对应gameobject 的 tranform
        float gs_maxMoveSpeed { get; } //飞向目标的最大速度

        // 开始被搜集时的通知
        //return :true 可以采集  false不能采集
        bool OnStartCollection(Transform target);

        // 完成采集
        void OnCompleteCollection();
    }

    public class CollectionTask : IDisposable
    {
        [BurstCompile]
        private struct CollectionFollowJob : IJobParallelForTransform
        {
            public float3 p_targetPos;
            public float p_deltaTime;
            public float p_sqrCollectDist;
            public float p_acceleration;
            public NativeArray<float> p_currentSpeeds;
            [ReadOnly] public NativeArray<float> p_maxSpeeds;

            // 结果通知数组
            [WriteOnly] public NativeArray<bool> p_isReached;
            public void Execute(int index, TransformAccess transform)
            {
                float3 pos = transform.position;
                float3 delta = p_targetPos - pos;
                delta.z = 0; // 2D 采集忽略 Z

                float sqrDist = math.lengthsq(delta);
                if (sqrDist <= p_sqrCollectDist)
                {
                    p_isReached[index] = true;
                    return;
                }

                p_isReached[index] = false;

                float currentSpeed = p_currentSpeeds[index];
                currentSpeed += p_acceleration * p_deltaTime;
                currentSpeed = math.min(currentSpeed, p_maxSpeeds[index]); // 限制最高速度
                p_currentSpeeds[index] = currentSpeed; // 把新速度写回
                float moveStep = currentSpeed * p_deltaTime;
                float maxDelta = math.max(math.abs(delta.x), math.abs(delta.y));

                if (maxDelta > 0.01f)
                {
                    if (maxDelta > moveStep)
                    {
                        delta *= (moveStep / maxDelta);
                    }

                    transform.position = pos + delta;
                }
            }
        }

        private Transform _owner; // 采集者（目标）
        private List<ICollection> _items;
        private TransformAccessArray _transformAccessArray;
        private NativeArray<float> _currentSpeeds;
        private NativeArray<float> _maxSpeeds;
        private NativeArray<bool> _isReached;

        private int _capacity;
        private float _sqrCollectDist = 0.25f; // 0.5 * 0.5

		private float _acceleration = 8f; //加速度
        private float _initialStartSpeed = 2f;  //初速度

#region public functions

        public CollectionTask(Transform owner, int initialCapacity = 32)
        {
            _owner = owner;
            _capacity = initialCapacity;
            _items = new List<ICollection>(_capacity);
            _transformAccessArray = new TransformAccessArray(_capacity);

            _currentSpeeds = new NativeArray<float>(_capacity, Allocator.Persistent);
            _maxSpeeds = new NativeArray<float>(_capacity, Allocator.Persistent);
            _isReached = new NativeArray<bool>(_capacity, Allocator.Persistent);
        }

        // 批量添加需要采集的物体
        public void AddCollectionItems<T>(List<T> newItems) where T : class
        {
            if (newItems == null || newItems.Count == 0)
                return;

            // 自动扩容检测
            int required = _items.Count + newItems.Count;
            if (required > _capacity)
                Expand(required);

            int count = newItems.Count;
            for (int i = 0; i < count; i++)
            {
                if (newItems[i] is ICollection item)
                {
                    // 通知物体：搜集开始了
                    if (item.OnStartCollection(_owner) == true)
                    {
                        _items.Add(item);
                        _transformAccessArray.Add(item.gs_transform);

                        int newIndex = _items.Count - 1;
                        _currentSpeeds[newIndex] = _initialStartSpeed + (Utils.GetRandomInt()-50f)/100f; // 起步很慢,  有[-0.5,0.5]的一个随机抖动
                        _maxSpeeds[newIndex] = item.gs_maxMoveSpeed;     // 极限速度
                    }
                }
                else
                    GameLogger.LogError($"Add item is not the type of ICollection ");
            }
        }

        public int RunCollectionTask(float deltaTime)
        {
            int count = _items.Count;
            if (count == 0)
                return count;

            // 调度 Job
            var job = new CollectionFollowJob
            {
                p_targetPos = _owner.position,
                p_deltaTime = deltaTime,
                p_sqrCollectDist = _sqrCollectDist,
                p_acceleration = _acceleration,      // 传入加速度
                p_currentSpeeds = _currentSpeeds,        // 读写数组
                p_maxSpeeds = _maxSpeeds,                // 只读数组
                p_isReached = _isReached
            };

            JobHandle handle = job.Schedule(_transformAccessArray);
            handle.Complete();

            // 处理到达目标的物体
            for (int i = count - 1; i >= 0; i--)
            {
                if (_isReached[i])
                {
                    var item = _items[i];
                    int last = _items.Count - 1;
                    Utils.ExecuteSwapAndPop(_items, i); // O(1) 移除逻辑
                    // 同步处理所有数组！
                    _currentSpeeds[i] = _currentSpeeds[last];
                    _maxSpeeds[i] = _maxSpeeds[last];
                    _transformAccessArray.RemoveAtSwapBack(i);

                    // 通知采集完成
                    item.OnCompleteCollection();
                }
            }
            return count;
        }

        public void Dispose()
        {
            if (_transformAccessArray.isCreated) _transformAccessArray.Dispose();
            if (_currentSpeeds.IsCreated) _currentSpeeds.Dispose();
            if (_maxSpeeds.IsCreated) _maxSpeeds.Dispose();
            if (_isReached.IsCreated) _isReached.Dispose();
            _items.Clear();
        }

#endregion

#region private functions

        private void Expand(int required)
        {
            while (_capacity < required)
                _capacity *= 2;

            var newCurrentSpeeds = new NativeArray<float>(_capacity, Allocator.Persistent);
            var newMaxSpeeds = new NativeArray<float>(_capacity, Allocator.Persistent);
            var newIsReached = new NativeArray<bool>(_capacity, Allocator.Persistent);

            if (_maxSpeeds.IsCreated)
            {
                NativeArray<float>.Copy(_currentSpeeds, newCurrentSpeeds, _currentSpeeds.Length);
                NativeArray<float>.Copy(_maxSpeeds, newMaxSpeeds, _maxSpeeds.Length);

                _currentSpeeds.Dispose();
                _maxSpeeds.Dispose();
                _isReached.Dispose();
            }

            _currentSpeeds = newCurrentSpeeds;
            _maxSpeeds = newMaxSpeeds;
            _isReached = newIsReached;
            _transformAccessArray.capacity = _capacity;
        }

#endregion
    }
}
