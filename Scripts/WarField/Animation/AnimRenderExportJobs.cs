using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace WarField.Anim
{
    // 每帧由 AnimRenderExportJob 写入的轻量渲染快照，AnimCtrl 在主线程直读，彻底消除 GetComponentData 随机访问
    public struct AnimRenderSnapshot
    {
        public int  p_sliceIndex;
        public int  p_eventCount;
        public int  p_finishCount;
        public uint p_currentStateId;
    }

    // 在 AnimUpdateSystem 之后运行，把所有 Entity 的渲染所需数据并行写入扁平 NativeArray
    // AnimCtrl 在 LateUpdate 里直接按 entity.Index 读取，零 ECS 跨界访问
    [UpdateAfter(typeof(AnimUpdateSystem))]
    [BurstCompile]
    public partial struct AnimRenderExportSystem : ISystem
    {
        private static NativeArray<AnimRenderSnapshot> s_snapshots;
        private static JobHandle s_lastHandle;
        private static bool s_initialized;

        // AnimCtrl.Awake 时调用，把 Persistent NativeArray 传进来
        public static void Initialize(NativeArray<AnimRenderSnapshot> snapshots)
        {
            s_snapshots  = snapshots;
            s_initialized = true;
        }

        // AnimCtrl.LateUpdate 开头调用，确保上一帧写入已完成
        public static void CompleteExport()
        {
            if (s_initialized)
                s_lastHandle.Complete();
        }

        // AnimCtrl.OnDestroy 调用，避免 ECS World 仍存活时再次写入已 Dispose 的 NativeArray
        public static void Shutdown()
        {
            s_lastHandle.Complete();
            s_snapshots   = default;
            s_lastHandle  = default;
            s_initialized = false;
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state) { }

        public void OnUpdate(ref SystemState state)
        {
            if (!s_initialized || !s_snapshots.IsCreated)
                return;

            var job = new AnimRenderExportJob
            {
                p_snapshots = s_snapshots,
                p_maxIndex  = s_snapshots.Length,
            };

            s_lastHandle    = job.ScheduleParallel(state.Dependency);
            state.Dependency = s_lastHandle;
        }
    }

    [BurstCompile]
    public partial struct AnimRenderExportJob : IJobEntity
    {
        // 按 p_renderSlot 写入；slot 在 AnimCtrl.Register/Unregister 时通过 swap-back 维护
        // 不同 entity 的 slot 必然唯一，无并行写冲突
        [NativeDisableParallelForRestriction]
        public NativeArray<AnimRenderSnapshot> p_snapshots;
        public int p_maxIndex;

        public void Execute(in AnimationRuntimeState runtimeState)
        {
            int slot = runtimeState.p_renderSlot;
            if ((uint)slot >= (uint)p_maxIndex)
                return;

            p_snapshots[slot] = new AnimRenderSnapshot
            {
                p_sliceIndex    = runtimeState.p_targetTextureSliceIndex,
                p_eventCount    = runtimeState.p_eventTriggerCount,
                p_finishCount   = runtimeState.p_finishCount,
                p_currentStateId = runtimeState.p_currentStateId,
            };
        }
    }
}
