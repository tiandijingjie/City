using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace WarField.Anim
{
    [BurstCompile]
    public partial struct AnimEcsSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) //由world默认每帧调用
        {
            // 调度多线程并行 Job 驱动全场动画状态
            var animJob = new AnimationUpdateJob
            {
                p_deltaTime = SystemAPI.Time.DeltaTime
            };

            state.Dependency = animJob.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct AnimationUpdateJob : IJobEntity
    {
        // 传递当前帧的步长
        public float p_deltaTime;

        public void Execute(ref AnimationRuntimeState runtimeState)
        {
            if (runtimeState.p_elementBlobRef.IsCreated == false)
            {
                return;
            }

            ref var elementData = ref runtimeState.p_elementBlobRef.Value;
            int stateIndex = -1;

            // 线性遍历模拟非托管字典检索
            for (int i = 0; i < elementData.p_states.Length; i++)
            {
                if (elementData.p_states[i].p_stateId == runtimeState.p_currentStateId)
                {
                    stateIndex = i;
                    break;
                }
            }

            if (stateIndex == -1)
            {
                return;
            }

            ref var targetState = ref elementData.p_states[stateIndex];

            // 检测到状态发生改变：重置时间戳并动态计算变种（Variation）随机值
            if (runtimeState.p_currentStateId != runtimeState.p_previousStateId)
            {
                runtimeState.p_elapsedTime = 0f;
                runtimeState.p_currentFrameIndex = 0;
                runtimeState.p_previousStateId = runtimeState.p_currentStateId;

                int variationCount = targetState.p_variations.Length;
                if (variationCount > 0)
                {
                    // 使用内置的 p_random 改写当前变种索引
                    runtimeState.p_currentVariationIndex = runtimeState.p_random.NextInt(0, variationCount);
                }
                else
                {
                    runtimeState.p_currentVariationIndex = 0;
                }
            }

            if (runtimeState.p_currentVariationIndex >= targetState.p_variations.Length)
            {
                return;
            }

            ref var variation = ref targetState.p_variations[runtimeState.p_currentVariationIndex];

            // 累加已播放时间
            runtimeState.p_elapsedTime = runtimeState.p_elapsedTime + p_deltaTime;
            float totalDuration = variation.p_animFrameCount / variation.p_frameRate;

            // 边界检查：处理循环与非循环边界卡帧逻辑
            if (runtimeState.p_elapsedTime >= totalDuration)
            {
                if (targetState.p_isLoop == true)
                {
                    runtimeState.p_elapsedTime = runtimeState.p_elapsedTime % totalDuration;
                }
                else
                {
                    runtimeState.p_elapsedTime = totalDuration - 0.001f;
                }
            }

            // 计算局部当前帧索引
            runtimeState.p_currentFrameIndex = (int)(runtimeState.p_elapsedTime * variation.p_frameRate);
            runtimeState.p_currentFrameIndex = math.clamp(runtimeState.p_currentFrameIndex, 0, variation.p_animFrameCount - 1);

            // 根据当前朝向映射出在大纹理阵列（Texture2DArray）中的绝对切片索引
            if (runtimeState.p_currentDirectionIndex < variation.p_animStartOffset.Length)
            {
                int baseOffset = variation.p_animStartOffset[runtimeState.p_currentDirectionIndex];
                runtimeState.p_targetTextureSliceIndex = baseOffset + runtimeState.p_currentFrameIndex;
            }
        }
    }
}
