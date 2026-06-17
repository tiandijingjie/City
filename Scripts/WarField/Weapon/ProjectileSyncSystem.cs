using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;
using UnityEngine;

namespace WarField
{
    using WD = WeaponDefines;

    [UpdateAfter(typeof(ProjectileHitSystem))] // 必须等待命中回调完成后再销毁表现
    public partial class ProjectileSyncSystem : SystemBase
    {
        // 辅助结构体：用于将需要销毁的插槽从大到小排序
        private struct DestroyItem : IComparable<DestroyItem>
        {
            public int p_slot;
            public Entity p_entity;

            public int CompareTo(DestroyItem other)
            {
                return other.p_slot.CompareTo(this.p_slot); // 倒序排序
            }
        }

        // 将 ECS 计算好的数据多线程搬运到 NativeArray 的 Job
        [BurstCompile]
        private partial struct WriteTransformDataJob : IJobEntity
        {
            [NativeDisableParallelForRestriction]
            public NativeArray<float2> p_positions;

            [NativeDisableParallelForRestriction]
            public NativeArray<float> p_rotations;

            public void Execute(in WD.ProjectilePositionComponent posComp, in WD.VfxRenderSlotComponent slotComp)
            {
                int slot = slotComp.p_slotIndex;
                p_positions[slot] = posComp.p_position;
                p_rotations[slot] = posComp.p_rotationAngle;
            }
        }

        // 利用 TransformAccessArray 极速操作 GameObject 的 Job
        [BurstCompile]
        private struct SyncTransformJob : IJobParallelForTransform
        {
            [ReadOnly] public NativeArray<float2> p_positions;
            [ReadOnly] public NativeArray<float> p_rotations;

            public void Execute(int index, TransformAccess transform)
            {
                float2 pos = p_positions[index];
                float rot = p_rotations[index];

                transform.position = new Vector3(pos.x, pos.y, 0f);
                transform.rotation = Quaternion.Euler(0f, 0f, rot);
            }
        }

        protected override void OnUpdate()
        {
            WeaponCtrl weaponCtrl = WeaponCtrl.Instance;

            // 收集需要销毁的实体与插槽 (脱离查询体避免报 StructuralChange 错误)
            NativeList<DestroyItem> destroyList = new NativeList<DestroyItem>(Allocator.TempJob);

            foreach (var (slotComp, entity) in SystemAPI.Query<RefRO<WD.VfxRenderSlotComponent>>().WithAll<WD.ProjectileDestroyTag>().WithEntityAccess())
            {
                destroyList.Add(new DestroyItem
                {
                    p_slot = slotComp.ValueRO.p_slotIndex,
                    p_entity = entity
                });
            }

            int destroyCount = destroyList.Length;
            if (destroyCount > 0)
            {
                destroyList.Sort();

                for (int i = 0; i < destroyCount; i++)
                {
                    int slot = destroyList[i].p_slot;
                    Entity entity = destroyList[i].p_entity;

                    // 倒序回收，绝对不会影响尚未处理的靠前插槽
                    Entity movedEntity = weaponCtrl.ReleaseProjectileAndSwapBack(slot);

                    if (movedEntity != Entity.Null)
                    {
                        WD.VfxRenderSlotComponent movedSlot = SystemAPI.GetComponent<WD.VfxRenderSlotComponent>(movedEntity);
                        movedSlot.p_slotIndex = slot;
                        SystemAPI.SetComponent(movedEntity, movedSlot);
                    }

                    EntityManager.DestroyEntity(entity);
                }
            }

            destroyList.Dispose();

            // 将存活实体的坐标和旋转写入连续数组 (Burst 加速)
            WriteTransformDataJob writeJob = new WriteTransformDataJob
            {
                p_positions = weaponCtrl.p_positions,
                p_rotations = weaponCtrl.p_rotations
            };

            Dependency = writeJob.ScheduleParallel(Dependency);

            // 调度 Transform 同步 Job (Unity 官方的 TransformAccessArray 多线程操作)
            SyncTransformJob syncJob = new SyncTransformJob
            {
                p_positions = weaponCtrl.p_positions,
                p_rotations = weaponCtrl.p_rotations
            };

            weaponCtrl.p_jobHandle = syncJob.Schedule(weaponCtrl.p_transformAccessArray, Dependency);
            Dependency = weaponCtrl.p_jobHandle;
        }
    }
}
