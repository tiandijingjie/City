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

        // 发射抛物线单体子弹 (如：弓箭、魔法球)
        //casterGridIndex :发射者的gridIndex
        public void FireBezierBullet(
            long weaponId, WE.FactionType faction, float damage,
            int casterMapId, int casterEleType, int casterGridIndex,
            int targetEleType, int targetGridIndex, bool triggerSkill,
            Vector2 startPos, Vector2 endPos, float maxHeight, float speed, GameObject prefab)
        {
            if (_beInited == false)
                return;

            Entity entity = CreateBaseProjectile(_bezierArchetype, weaponId, faction, damage, casterMapId, casterEleType, casterGridIndex, triggerSkill, prefab);
            BuildBezierMovement(entity, startPos, endPos, maxHeight, speed);
            AttachTarget(entity, targetEleType, targetGridIndex);

            _entityManager.AddComponentData(entity, new WD.SingleTargetComponent());
        }

        // 发射抛物线范围炮弹 (如：投石车、迫击炮)
        // damageRange:伤害范围
        // otherDamage:主目标之外其他目标的伤害
        // canAttackBuilding:是否允许伤害建筑
        public void FireBezierShell(
            long weaponId, WE.FactionType faction, float damage,
            int casterMapId, int casterEleType, int casterGridIndex,
            int targetEleType, int targetGridIndex, bool triggerSkill,
            Vector2 startPos, Vector2 endPos, float maxHeight, float speed,
            float damageRange, float otherDamage, GameObject prefab, bool canAttackBuilding = true)
        {
            if (_beInited == false)
                return;

            Entity entity = CreateBaseProjectile(_bezierArchetype, weaponId, faction, damage, casterMapId, casterEleType, casterGridIndex, triggerSkill, prefab);
            BuildBezierMovement(entity, startPos, endPos, maxHeight, speed);
            AttachTarget(entity, targetEleType, targetGridIndex);

            WD.AreaDamageComponent areaComp = new WD.AreaDamageComponent();
            areaComp.p_damageRange = damageRange;
            areaComp.p_otherDamage = otherDamage;
            areaComp.p_canAttackBuilding = canAttackBuilding;
            _entityManager.AddComponentData(entity, areaComp);
        }

        // 发射直线单体子弹 (如：火枪、平飞无抛物线的魔法)
        public void FireLinearBullet(
            long weaponId, WE.FactionType faction, float damage,
            int casterMapId, int casterEleType, int casterGridIndex,
            int targetEleType, int targetGridIndex, bool triggerSkill,
            Vector2 startPos, Vector2 direction, float speed, float maxDistance, GameObject prefab)
        {
            if (_beInited == false)
                return;

            Entity entity = CreateBaseProjectile(_linearArchetype, weaponId, faction, damage, casterMapId, casterEleType, casterGridIndex, triggerSkill, prefab);
            BuildLinearMovement(entity, startPos, direction, speed, maxDistance);
            AttachTarget(entity, targetEleType, targetGridIndex);

            _entityManager.AddComponentData(entity, new WD.SingleTargetComponent());
        }

        // 发射直线穿透技能 (如：风行者强力击、穿透激光)
        public void FireLinearNoTarget(
            long weaponId, WE.FactionType faction, float damage,
            int casterMapId, int casterEleType, int casterGridIndex,
            Vector2 startPos, Vector2 direction, float speed, float maxDistance,
            float colliderRadius, GameObject prefab) // 删除了 maxPierceCount
        {
            if (_beInited == false)
                return;

            Entity entity = CreateBaseProjectile(_linearArchetype, weaponId, faction, damage, casterMapId, casterEleType, casterGridIndex, false, prefab);
            BuildLinearMovement(entity, startPos, direction, speed, maxDistance);

            // 挂载判定范围组件
            WD.LinearPenetrationComponent penComp = new WD.LinearPenetrationComponent();
            penComp.p_colliderRadius = colliderRadius;
            _entityManager.AddComponentData(entity, penComp);

            // 挂载动态记忆数组 (刚发射时是空的)
            _entityManager.AddBuffer<WD.HitRecordElement>(entity);
        }
#endregion

#region private functions
        // 提取公共基础组件生成，消除重复代码
        private Entity CreateBaseProjectile(
            EntityArchetype archetype, long weaponId, WE.FactionType faction, float damage,
            int mapId, int casterEleType, int casterGridIndex, bool triggerSkill, GameObject prefab)
        {
            Entity entity = _entityManager.CreateEntity(archetype);

            WD.ProjectileBaseComponent baseComp = new WD.ProjectileBaseComponent();
            baseComp.p_weaponId = weaponId;
            baseComp.p_faction = faction;
            baseComp.p_baseDamage = damage;
            baseComp.p_mapId = mapId;
            baseComp.p_casterEleType = casterEleType;
            baseComp.p_casterGridIndex = casterGridIndex;
            baseComp.p_triggerSkill = triggerSkill;
            _entityManager.SetComponentData(entity, baseComp);

            int slotIndex = AddProjectile(prefab, entity);

            WD.VfxRenderSlotComponent slotComp = new WD.VfxRenderSlotComponent();
            slotComp.p_slotIndex = slotIndex;
            _entityManager.SetComponentData(entity, slotComp);

            return entity;
        }

        // 提取抛物线运动组件组装
        private void BuildBezierMovement(Entity entity, Vector2 startPos, Vector2 endPos, float maxHeight, float speed)
        {
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
        }

        // 提取直线运动组件组装
        private void BuildLinearMovement(Entity entity, Vector2 startPos, Vector2 direction, float speed, float maxDistance)
        {
            WD.ProjectilePositionComponent posComp = new WD.ProjectilePositionComponent();
            posComp.p_position = new float2(startPos.x, startPos.y);
            posComp.p_rotationAngle = math.degrees(math.atan2(direction.y, direction.x));
            _entityManager.SetComponentData(entity, posComp);

            WD.LinearMoveComponent moveComp = new WD.LinearMoveComponent();
            moveComp.p_direction = new float2(direction.x, direction.y);
            moveComp.p_speed = speed;
            moveComp.p_maxDistance = maxDistance;
            moveComp.p_movedDistance = 0f;
            _entityManager.SetComponentData(entity, moveComp);
        }

        // 提取目标锁定组件组装
        private void AttachTarget(Entity entity, int targetEleType, int targetGridIndex)
        {
            WD.ProjectileTargetComponent targetComp = new WD.ProjectileTargetComponent();
            targetComp.p_targetEleType = targetEleType;
            targetComp.p_targetGridIndex = targetGridIndex;
            _entityManager.AddComponentData(entity, targetComp);
        }

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
