using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace WarField.EffectAnim
{
    // Applies direction / animRate / forceReset commands from the main thread before EffectAnimUpdateSystem runs.
    [UpdateBefore(typeof(EffectAnimUpdateSystem))]
    [BurstCompile]
    public partial struct EffectAnimSyncSystem : ISystem
    {
        private static NativeArray<EffectAnimSyncCommand> s_buffer;
        private static int      s_count;
        private static bool     s_hasBuffer;
        private static JobHandle s_lastJobHandle;

        private ComponentLookup<EffectAnimRuntimeState> _stateLookup;

        /// <summary>EffectAnimCtrl calls this every LateUpdate to hand off the command slice.</summary>
        public static void SetCommandBuffer(NativeArray<EffectAnimSyncCommand> buffer, int count)
        {
            s_buffer    = buffer;
            s_count     = count;
            s_hasBuffer = true;
        }

        /// <summary>EffectAnimCtrl calls this before overwriting the buffer to avoid a data race.</summary>
        public static void WaitForLastJob() => s_lastJobHandle.Complete();

        /// <summary>EffectAnimCtrl.OnDestroy calls this to neutralise the static references.</summary>
        public static void Shutdown()
        {
            s_lastJobHandle.Complete();
            s_buffer        = default;
            s_count         = 0;
            s_hasBuffer     = false;
            s_lastJobHandle = default;
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _stateLookup = state.GetComponentLookup<EffectAnimRuntimeState>(false);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!s_hasBuffer || s_count == 0) return;

            s_hasBuffer = false;
            _stateLookup.Update(ref state);

            var job = new EffectAnimSyncJob
            {
                p_commands    = s_buffer.GetSubArray(0, s_count),
                p_stateLookup = _stateLookup,
            };

            s_lastJobHandle  = job.Schedule(s_count, 64, state.Dependency);
            state.Dependency = s_lastJobHandle;
        }
    }

    [BurstCompile]
    public struct EffectAnimSyncJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<EffectAnimSyncCommand>       p_commands;
        [NativeDisableParallelForRestriction]
        public ComponentLookup<EffectAnimRuntimeState>             p_stateLookup;

        public void Execute(int index)
        {
            EffectAnimSyncCommand cmd = p_commands[index];
            if (!p_stateLookup.HasComponent(cmd.p_targetEntity)) return;

            EffectAnimRuntimeState st = p_stateLookup[cmd.p_targetEntity];
            st.p_currentDirectionIndex = cmd.p_directionIndex;
            st.p_animRate              = cmd.p_animRate;
            if (cmd.p_forceReset)
                st.p_shouldReset = true;
            p_stateLookup[cmd.p_targetEntity] = st;
        }
    }
}
