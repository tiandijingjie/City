using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace WarField.EffectAnim
{
    // Lightweight per-slot snapshot written each ECS tick and read by EffectAnimCtrl.LateUpdate.
    // Simpler than AnimRenderSnapshot: no stateId field (EffectAnim has no states).
    public struct EffectAnimRenderSnapshot
    {
        public int p_sliceIndex;
        public int p_eventCount;
        public int p_finishCount;
    }

    // Runs after EffectAnimUpdateSystem, exports render-relevant data into a flat NativeArray
    // that EffectAnimCtrl reads on the main thread with zero ECS API overhead.
    [UpdateAfter(typeof(EffectAnimUpdateSystem))]
    [BurstCompile]
    public partial struct EffectAnimRenderExportSystem : ISystem
    {
        private static NativeArray<EffectAnimRenderSnapshot> s_snapshots;
        private static JobHandle s_lastHandle;
        private static bool      s_initialized;

        /// <summary>EffectAnimCtrl.Awake calls this to wire up the persistent NativeArray.</summary>
        public static void Initialize(NativeArray<EffectAnimRenderSnapshot> snapshots)
        {
            s_snapshots   = snapshots;
            s_initialized = true;
        }

        /// <summary>EffectAnimCtrl.LateUpdate calls this at the top to wait for last frame's write.</summary>
        public static void CompleteExport()
        {
            if (s_initialized) s_lastHandle.Complete();
        }

        /// <summary>EffectAnimCtrl.OnDestroy calls this before disposing the NativeArray.</summary>
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
            if (!s_initialized || !s_snapshots.IsCreated) return;

            var job = new EffectAnimRenderExportJob
            {
                p_snapshots = s_snapshots,
                p_maxIndex  = s_snapshots.Length,
            };
            s_lastHandle     = job.ScheduleParallel(state.Dependency);
            state.Dependency = s_lastHandle;
        }
    }

    [BurstCompile]
    public partial struct EffectAnimRenderExportJob : IJobEntity
    {
        [NativeDisableParallelForRestriction]
        public NativeArray<EffectAnimRenderSnapshot> p_snapshots;
        public int p_maxIndex;

        public void Execute(in EffectAnimRuntimeState st)
        {
            int slot = st.p_renderSlot;
            if ((uint)slot >= (uint)p_maxIndex) return;

            p_snapshots[slot] = new EffectAnimRenderSnapshot
            {
                p_sliceIndex  = st.p_targetTextureSliceIndex,
                p_eventCount  = st.p_eventTriggerCount,
                p_finishCount = st.p_finishCount,
            };
        }
    }
}
