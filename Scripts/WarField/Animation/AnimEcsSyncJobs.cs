using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace WarField.Anim
{
    // AnimSyncSystem 负责同步动画状态变化（state/dir/animRate），强制在 AnimUpdateSystem 之前运行
    [UpdateBefore(typeof(AnimUpdateSystem))]
    [BurstCompile]
    public partial struct AnimSyncSystem : ISystem
    {
        // 由 AnimCtrl 持有的 Persistent NativeArray，本系统只读不释放（修复瓶颈5：消除 TempJob 泄露风险）
        private static NativeArray<AnimSyncCommand> s_buffer;
        private static int  s_count;
        private static bool s_hasBuffer;

        // 上一帧调度的 Job 句柄，供 AnimCtrl 在下一帧覆写缓冲区前调用 Complete() 等待
        private static JobHandle s_lastJobHandle;

        private ComponentLookup<AnimationRuntimeState> _stateLookup;

        /// <summary>
        /// AnimCtrl 每帧 LateUpdate 调用，传入 Persistent 缓冲区的有效前 count 条指令。
        /// 不转移所有权，AnimCtrl 负责缓冲区生命周期。
        /// </summary>
        public static void SetCommandBuffer(NativeArray<AnimSyncCommand> buffer, int count)
        {
            s_buffer    = buffer;
            s_count     = count;
            s_hasBuffer = true;
        }

        /// <summary>AnimCtrl 在下一帧覆写缓冲区之前必须调用，等待上一帧的同步 Job 完成。</summary>
        public static void WaitForLastJob() => s_lastJobHandle.Complete();

        /// <summary>AnimCtrl.OnDestroy 调用，避免 ECS World 仍存活时静态字段引用悬空 NativeArray。</summary>
        public static void Shutdown()
        {
            s_lastJobHandle.Complete();
            s_buffer    = default;
            s_count     = 0;
            s_hasBuffer = false;
            s_lastJobHandle = default;
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _stateLookup = state.GetComponentLookup<AnimationRuntimeState>(false);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!s_hasBuffer || s_count == 0)
                return;

            s_hasBuffer = false;
            _stateLookup.Update(ref state);

            var syncJob = new AnimSyncJob
            {
                p_commands   = s_buffer.GetSubArray(0, s_count),
                p_stateLookup = _stateLookup
            };

            s_lastJobHandle  = syncJob.Schedule(s_count, 64, state.Dependency);
            state.Dependency = s_lastJobHandle;
            // 缓冲区由 AnimCtrl 管理，此处不 Dispose
        }
    }

    [BurstCompile]
    public struct AnimSyncJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<AnimSyncCommand> p_commands;
        [NativeDisableParallelForRestriction]
        public ComponentLookup<AnimationRuntimeState> p_stateLookup;

        public void Execute(int index)
        {
            AnimSyncCommand cmd    = p_commands[index];
            Entity          target = cmd.p_targetAnimEntity;

            if (!p_stateLookup.HasComponent(target))
                return;

            AnimationRuntimeState st = p_stateLookup[target];
            st.p_currentStateId       = cmd.p_stateId;
            st.p_currentDirectionIndex = cmd.p_directionIndex;
            st.p_animRate             = cmd.p_animRate;
            // p_forceReset: 强制重播同一 state（如攻击动画结束后再次播放）
            // 将 p_previousStateId 置为 uint.MaxValue，令 AnimUpdateJob 误判状态发生变化，触发完整动画重置
            if (cmd.p_forceReset)
                st.p_previousStateId = uint.MaxValue;
            p_stateLookup[target]     = st;
        }
    }
}
