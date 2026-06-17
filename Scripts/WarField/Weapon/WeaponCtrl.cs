using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace WarField
{
    using WD = WeaponDefines;
    using WE = WarFieldElements;

    public class WeaponCtrl : MonoBehaviour
    {
#region public parameters
        public static WeaponCtrl Instance;

        // --- 表现层同步数据 (直接供 ECS 的 SyncJob 极速写入) ---
        public NativeArray<float2> p_positions;
        public NativeArray<float> p_rotations;
        public TransformAccessArray p_transformAccessArray;
        public JobHandle p_jobHandle;
#endregion

#region private parameters
        // --- GameObject 对象池 ---
        private List<GameObject> _activeObjects;
        private List<Entity> _activeEntities;
        private Queue<GameObject> _pool;
        private int _capacity = 4096;

        // --- ECS 逻辑层数据 ---
        private EntityManager _entityManager;
        private EntityArchetype _bezierArchetype;
        private EntityArchetype _linearArchetype;
        private bool _beInited = false;
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
#endregion

#region public functions
        public bool InitWeaponCtrl()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            // 预定义抛物线/单体追踪弹道的内存原型
            _bezierArchetype = _entityManager.CreateArchetype(
                typeof(WD.ProjectileBaseComponent),
                typeof(WD.ProjectilePositionComponent),
                typeof(WD.BezierMoveComponent),
                typeof(WD.VfxRenderSlotComponent)
            );

            // 预定义直线穿透弹道的内存原型
            _linearArchetype = _entityManager.CreateArchetype(
                typeof(WD.ProjectileBaseComponent),
                typeof(WD.ProjectilePositionComponent),
                typeof(WD.LinearMoveComponent),
                typeof(WD.VfxRenderSlotComponent)
            );

            _beInited = true;

            return true;
        }

        // ECS 回收逻辑，返回被交换位置的 Entity 以便更新它的 RenderSlot (必须保证 O(1) 连续性)
        public Entity ReleaseProjectileAndSwapBack(int slotIndex)
        {
            p_jobHandle.Complete();

            GameObject objToRelease = _activeObjects[slotIndex];
            objToRelease.SetActive(false);
            _pool.Enqueue(objToRelease);

            int lastIndex = _activeObjects.Count - 1;
            Entity movedEntity = Entity.Null;

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

        // 发射贝塞尔类投射物 (合并了组装 ECS 与申请 GameObject 的逻辑)
        public void FireBezierProjectile(
            long configId, WE.FactionType faction, float damage,
            int casterEleType, int casterGridIndex, bool triggerSkill, // <--- 参数增加 casterEleType
            Vector2 startPos, Vector2 endPos, float maxHeight, float speed,
            GameObject prefab)
        {
            if (_beInited == false)
            {
                return;
            }

            // 创建纯逻辑实体
            Entity entity = _entityManager.CreateEntity(_bezierArchetype);

            // 写入基础信息 (记录地图与网格索引用于回调)
            WD.ProjectileBaseComponent baseComp = new WD.ProjectileBaseComponent();
            baseComp.p_configId = configId;
            baseComp.p_faction = faction;
            baseComp.p_baseDamage = damage;
            baseComp.p_casterEleType = casterEleType;
            baseComp.p_casterGridIndex = casterGridIndex;
            baseComp.p_triggerSkill = triggerSkill;

            _entityManager.SetComponentData(entity, baseComp);

            WD.ProjectilePositionComponent posComp = new WD.ProjectilePositionComponent();
            posComp.p_position = new float2(startPos.x, startPos.y);
            posComp.p_rotationAngle = 0f;
            _entityManager.SetComponentData(entity, posComp);

            WD.BezierMoveComponent moveComp = new WD.BezierMoveComponent();
            moveComp.p_startPos = new float2(startPos.x, startPos.y);
            moveComp.p_endPos = new float2(endPos.x, endPos.y);
            moveComp.p_maxHeight = maxHeight;
            moveComp.p_speed = speed;
            moveComp.p_progress = 0f;
            _entityManager.SetComponentData(entity, moveComp);

            // 申请表现层 GameObject
            int slotIndex = AddProjectile(prefab, entity);

            WD.VfxRenderSlotComponent slotComp = new WD.VfxRenderSlotComponent();
            slotComp.p_slotIndex = slotIndex;
            _entityManager.SetComponentData(entity, slotComp);
        }
#endregion

#region private functions
        private int AddProjectile(GameObject prefab, Entity ecsEntity)
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
#endregion
    }
}
