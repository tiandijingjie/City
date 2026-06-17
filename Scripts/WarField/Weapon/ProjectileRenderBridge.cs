using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace WarField
{
    //暂时用来关联gamobject和move job
    public class ProjectileRenderBridge : MonoBehaviour
    {
        public static ProjectileRenderBridge Instance;

        public NativeArray<float2> p_positions;
        public NativeArray<float> p_rotations;
        public TransformAccessArray p_transformAccessArray;
        public JobHandle p_jobHandle;

        private List<GameObject> _activeObjects;
        private List<Entity> _activeEntities;
        private Queue<GameObject> _pool;
        private int _capacity = 4096;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _activeObjects = new List<GameObject>(_capacity);
            _activeEntities = new List<Entity>(_capacity);
            _pool = new Queue<GameObject>(_capacity);

            p_positions = new NativeArray<float2>(_capacity, Allocator.Persistent);
            p_rotations = new NativeArray<float>(_capacity, Allocator.Persistent);
            p_transformAccessArray = new TransformAccessArray(_capacity);
        }

        private void OnDestroy()
        {
            p_jobHandle.Complete();

            if (p_positions.IsCreated)
            {
                p_positions.Dispose();
            }

            if (p_rotations.IsCreated)
            {
                p_rotations.Dispose();
            }

            if (p_transformAccessArray.isCreated)
            {
                p_transformAccessArray.Dispose();
            }
        }

        // 业务层生成武器时调用这个接口分配 GameObject 和 Slot
        public int AddProjectile(GameObject prefab, Entity ecsEntity)
        {
            p_jobHandle.Complete();

            GameObject obj = null;
            if (_pool.Count > 0)
            {
                obj = _pool.Dequeue();
            }
            else
            {
                obj = Instantiate(prefab);
            }

            obj.SetActive(true);

            int slot = _activeObjects.Count;
            _activeObjects.Add(obj);
            _activeEntities.Add(ecsEntity);
            p_transformAccessArray.Add(obj.transform);

            return slot;
        }

        // ECS 回收逻辑，返回被交换位置的 Entity 以便更新它的 RenderSlot
        public Entity ReleaseProjectileAndSwapBack(int slotIndex)
        {
            p_jobHandle.Complete();

            GameObject objToRelease = _activeObjects[slotIndex];
            objToRelease.SetActive(false);
            _pool.Enqueue(objToRelease);

            int lastIndex = _activeObjects.Count - 1;
            Entity movedEntity = Entity.Null;

            // O(1) 移除算法：把数组最后一位搬到被删除的这个洞里
            if (slotIndex < lastIndex)
            {
                GameObject movedObj = _activeObjects[lastIndex];
                Entity entityToMove = _activeEntities[lastIndex];

                _activeObjects[slotIndex] = movedObj;
                _activeEntities[slotIndex] = entityToMove;
                movedEntity = entityToMove;

                p_positions[slotIndex] = p_positions[lastIndex];
                p_rotations[slotIndex] = p_rotations[lastIndex];
            }

            _activeObjects.RemoveAt(lastIndex);
            _activeEntities.RemoveAt(lastIndex);
            p_transformAccessArray.RemoveAtSwapBack(slotIndex);

            return movedEntity;
        }
    }
}
