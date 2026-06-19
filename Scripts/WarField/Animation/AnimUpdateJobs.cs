using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace WarField.Anim
{
    //AnimUpdateSystem 和 AnimationUpdateJob 一起负责更新动画帧

    // 用独立静态类持有全局 fps，完全绕开 Burst struct 的静态字段限制。
    // AnimCtrl.LateUpdate 每帧写入，AnimUpdateSystem.OnUpdate 读取（均在主线程，无竞态）。
    public static class AnimGlobalData
    {
        public static float s_animFPS = 12f;
    }

    [BurstCompile]
    public partial struct AnimUpdateSystem : ISystem
    {
        // OnUpdate 不加 [BurstCompile]，与 AnimRenderExportSystem 完全一致的模式：
        // 主线程读取托管静态字段，再调度 Burst Job（Job.Execute 仍是 BurstCompile）。
        public void OnUpdate(ref SystemState state) //由world默认每帧调用
        {
            float fps = AnimGlobalData.s_animFPS > 0f ? AnimGlobalData.s_animFPS : 12f;
            var animJob = new AnimationUpdateJob
            {
                p_deltaTime = state.WorldUnmanaged.Time.DeltaTime,
                p_animFPS   = fps
            };

            state.Dependency = animJob.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct AnimationUpdateJob : IJobEntity
    {
        // 传递当前帧的步长
        public float p_deltaTime;
        // 全局基准 fps，由 AnimCtrl._animFPS 通过 AnimGlobalData 驱动
        public float p_animFPS;

        public void Execute(ref AnimationRuntimeState runtimeState)
        {
            if (runtimeState.p_elementBlobRef.IsCreated == false)
                return;

            ref var elementData = ref runtimeState.p_elementBlobRef.Value;
            int stateCount = elementData.p_states.Length;
            int stateIndex = runtimeState.p_cachedStateIndex;

            // 命中缓存：直接复用上一帧定位过的索引
            if ((uint)stateIndex >= (uint)stateCount ||
                elementData.p_states[stateIndex].p_stateId != runtimeState.p_currentStateId)
            {
                stateIndex = -1;
                for (int i = 0; i < stateCount; i++)
                {
                    if (elementData.p_states[i].p_stateId == runtimeState.p_currentStateId)
                    {
                        stateIndex = i;
                        break;
                    }
                }
                runtimeState.p_cachedStateIndex = stateIndex;
            }

            if (stateIndex == -1)
            {
                return;
            }

            ref var targetState = ref elementData.p_states[stateIndex];
            int prevFrame = runtimeState.p_currentFrameIndex; //记录当前播放的帧

            // 检测到状态发生改变：重置时间戳并动态计算变种（Variation）随机值
            if (runtimeState.p_currentStateId != runtimeState.p_previousStateId)
            {
                runtimeState.p_elapsedTime = 0f;
                runtimeState.p_currentFrameIndex = 0;
                runtimeState.p_previousStateId = runtimeState.p_currentStateId;
                runtimeState.p_eventTriggerCount = 0;
                runtimeState.p_finishCount = 0;
                prevFrame = -1;

                int variationCount = targetState.p_variations.Length;
                if (variationCount > 0) // 使用内置的 p_random 改写当前变种索引
                    runtimeState.p_currentVariationIndex = runtimeState.p_random.NextInt(0, variationCount);
                else  //只有一个动画  没有变种
                    runtimeState.p_currentVariationIndex = 0;
            }

            if (runtimeState.p_currentVariationIndex >= targetState.p_variations.Length)
                return;

            ref var variation = ref targetState.p_variations[runtimeState.p_currentVariationIndex];

            // 累加已播放时间：p_frameRate 现为速率倍率（1.0 = 正常速度），p_animFPS 为基准 fps
            runtimeState.p_elapsedTime = runtimeState.p_elapsedTime + (p_deltaTime * runtimeState.p_animRate * variation.p_frameRate);
            float totalDuration = variation.p_animFrameCount / p_animFPS;

            int nextFrame = (int)(runtimeState.p_elapsedTime * p_animFPS);
            // 边界检查：处理循环与非循环边界卡帧逻辑
            if (runtimeState.p_elapsedTime >= totalDuration) //已经超过动画一个循环的时间
            {
                if (targetState.p_isLoop == true)
                {
                    // 事件帧通知
                    if (variation.p_eventFrame != -1 && nextFrame >= variation.p_eventFrame && prevFrame < variation.p_eventFrame)
                        runtimeState.p_eventTriggerCount = runtimeState.p_eventTriggerCount + 1;
                    runtimeState.p_elapsedTime = runtimeState.p_elapsedTime % totalDuration;
                    int wrappedFrame = (int)(runtimeState.p_elapsedTime * p_animFPS);
                    runtimeState.p_currentFrameIndex = math.clamp(wrappedFrame, 0, variation.p_animFrameCount - 1); //这里不能用nextFrame,因为runtimeState.p_elapsedTime又更新了
                }
                else //不是循环动画
                {
                    runtimeState.p_elapsedTime = totalDuration - 0.001f; //保持在最后一帧, 保证时间小于动画总时长一点
                    runtimeState.p_currentFrameIndex = variation.p_animFrameCount - 1; //保持在最后一帧

                    //  播放完成，通知结束
                    if (prevFrame < variation.p_animFrameCount - 1)
                        runtimeState.p_finishCount = runtimeState.p_finishCount + 1;

                    // 事件帧通知
                    if (variation.p_eventFrame != -1 && runtimeState.p_currentFrameIndex >= variation.p_eventFrame && prevFrame < variation.p_eventFrame)
                        runtimeState.p_eventTriggerCount = runtimeState.p_eventTriggerCount + 1;
                }
            }
            else
            {
                // 计算局部当前帧索引
                runtimeState.p_currentFrameIndex = math.clamp(nextFrame, 0, variation.p_animFrameCount - 1);

                // 事件帧通知
                if (variation.p_eventFrame != -1 && runtimeState.p_currentFrameIndex >= variation.p_eventFrame && prevFrame < variation.p_eventFrame)
                    runtimeState.p_eventTriggerCount = runtimeState.p_eventTriggerCount + 1;
            }

            // 根据当前朝向映射出在大纹理阵列（Texture2DArray）中的绝对切片索引
            if (runtimeState.p_currentDirectionIndex < variation.p_animStartOffset.Length)
            {
                int baseOffset = variation.p_animStartOffset[runtimeState.p_currentDirectionIndex];
                runtimeState.p_targetTextureSliceIndex = baseOffset + runtimeState.p_currentFrameIndex;
            }
        }
    }
}
