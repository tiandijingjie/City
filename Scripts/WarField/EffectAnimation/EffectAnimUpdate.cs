using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace WarField.EffectAnim
{
    // Static bridge between the main-thread EffectAnimCtrl and Burst jobs.
    // Same pattern as WarField.Anim.AnimGlobalData — bypasses Burst's static-field restrictions.
    public static class EffectAnimGlobalData
    {
        public static float s_animFPS = 12f;
    }

    // Runs after EffectAnimSyncSystem so direction/rate/reset commands land before frame advance.
    [UpdateAfter(typeof(EffectAnimSyncSystem))]
    [BurstCompile]
    public partial struct EffectAnimUpdateSystem : ISystem
    {
        // OnUpdate is NOT [BurstCompile] — must read the managed static field on the main thread
        // then hand off to the Burst job, identical pattern to AnimUpdateSystem.
        public void OnUpdate(ref SystemState state)
        {
            float fps = EffectAnimGlobalData.s_animFPS > 0f ? EffectAnimGlobalData.s_animFPS : 12f;
            var job = new EffectAnimUpdateJob
            {
                p_deltaTime = state.WorldUnmanaged.Time.DeltaTime,
                p_animFPS   = fps,
            };
            state.Dependency = job.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct EffectAnimUpdateJob : IJobEntity
    {
        public float p_deltaTime;
        public float p_animFPS;

        public void Execute(ref EffectAnimRuntimeState st)
        {
            if (!st.p_blobRef.IsCreated) return;
            ref var data = ref st.p_blobRef.Value;
            if (data.p_frameCount <= 0 || p_animFPS <= 0f) return;

            // Hard reset triggered by EffectAnimSyncJob (forceReset flag or explicit ResetAnim call)
            if (st.p_shouldReset)
            {
                st.p_elapsedTime       = 0f;
                st.p_currentFrameIndex = 0;
                st.p_eventTriggerCount = 0;
                st.p_finishCount       = 0;
                st.p_shouldReset       = false;
            }

            int   prevFrame     = st.p_currentFrameIndex;
            float totalDuration = data.p_frameCount / p_animFPS;

            st.p_elapsedTime += p_deltaTime * st.p_animRate * data.p_frameRate;
            int nextFrame = (int)(st.p_elapsedTime * p_animFPS);

            if (st.p_elapsedTime >= totalDuration)
            {
                if (data.p_isLoop)
                {
                    // Fire event before wrapping so the frame crossing is not missed
                    if (data.p_eventFrame != -1 && nextFrame >= data.p_eventFrame && prevFrame < data.p_eventFrame)
                        st.p_eventTriggerCount++;

                    st.p_elapsedTime %= totalDuration;
                    int wrapped = (int)(st.p_elapsedTime * p_animFPS);
                    st.p_currentFrameIndex = math.clamp(wrapped, 0, data.p_frameCount - 1);
                }
                else
                {
                    // Clamp at last frame; bump finishCount only once per playthrough
                    st.p_elapsedTime       = totalDuration - 0.001f;
                    st.p_currentFrameIndex = data.p_frameCount - 1;

                    if (prevFrame < data.p_frameCount - 1)
                        st.p_finishCount++;

                    if (data.p_eventFrame != -1 && st.p_currentFrameIndex >= data.p_eventFrame && prevFrame < data.p_eventFrame)
                        st.p_eventTriggerCount++;
                }
            }
            else
            {
                st.p_currentFrameIndex = math.clamp(nextFrame, 0, data.p_frameCount - 1);
                if (data.p_eventFrame != -1 && st.p_currentFrameIndex >= data.p_eventFrame && prevFrame < data.p_eventFrame)
                    st.p_eventTriggerCount++;
            }

            // Map direction index → absolute Texture2DArray slice.
            // (uint) cast guards against both negative values and >= Length in one compare.
            if ((uint)st.p_currentDirectionIndex < (uint)data.p_dirStartOffsets.Length)
                st.p_targetTextureSliceIndex = data.p_dirStartOffsets[st.p_currentDirectionIndex] + st.p_currentFrameIndex;
        }
    }
}
