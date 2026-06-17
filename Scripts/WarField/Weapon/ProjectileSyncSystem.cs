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

    // 必须在移动计算完毕后执行
    [UpdateAfter(typeof(ProjectileMoveSystem))]
    public partial class ProjectileSyncSystem : SystemBase
    {
        // 1. 将 ECS 计算好的数据多线程搬运到 NativeArray 的 Job
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

        // 2. 利用 TransformAccessArray 极速操作 GameObject 的 Job
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
            ProjectileRenderBridge bridge = ProjectileRenderBridge.Instance;
            if (bridge == null)
            {
                return;
            }

            // 1. 收集需要销毁的实体与插槽 (脱离查询体避免报 StructuralChange 错误)
            NativeList<Entity> destroyList = new NativeList<Entity>(Allocator.TempJob);
            NativeList<int> slotList = new NativeList<int>(Allocator.TempJob);

            foreach (var (slotComp, entity) in SystemAPI.Query<RefRO<WD.VfxRenderSlotComponent>>().WithAll<WD.ProjectileDestroyTag>().WithEntityAccess())
            {
                destroyList.Add(entity);
                slotList.Add(slotComp.ValueRO.p_slotIndex);
            }

            int destroyCount = destroyList.Length;
            if (destroyCount > 0)
            {
                for (int i = 0; i < destroyCount; i++)
                {
                    int slot = slotList[i];
                    Entity entity = destroyList[i];

                    // 回收表现层对象，并处理内存连续性交换(Swap-Back)
                    Entity movedEntity = bridge.ReleaseProjectileAndSwapBack(slot);

                    // 如果因为数组填坑移动了某个 GameObject 的插槽，必须同步更新其对应 Entity 的插槽序号
                    if (movedEntity != Entity.Null)
                    {
                        WD.VfxRenderSlotComponent movedSlot = SystemAPI.GetComponent<WD.VfxRenderSlotComponent>(movedEntity);
                        movedSlot.p_slotIndex = slot;
                        SystemAPI.SetComponent(movedEntity, movedSlot);
                    }

                    // 彻底从 ECS 内存中清除纯逻辑实体
                    EntityManager.DestroyEntity(entity);
                }
            }

            destroyList.Dispose();
            slotList.Dispose();

            // 2. 将存活实体的坐标和旋转写入连续数组 (Burst 加速)
            WriteTransformDataJob writeJob = new WriteTransformDataJob
            {
                p_positions = bridge.p_positions,
                p_rotations = bridge.p_rotations
            };

            Dependency = writeJob.ScheduleParallel(Dependency);

            // 3. 调度 Transform 同步 Job (Unity 官方的 TransformAccessArray 多线程操作)
            SyncTransformJob syncJob = new SyncTransformJob
            {
                p_positions = bridge.p_positions,
                p_rotations = bridge.p_rotations
            };

            // 把句柄交给桥接器，防止下一帧生命周期错乱
            bridge.p_jobHandle = syncJob.Schedule(bridge.p_transformAccessArray, Dependency);
            Dependency = bridge.p_jobHandle;
        }
    }
}
