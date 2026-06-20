using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

namespace WarField.Anim
{
    //Blob Asset, 将ElementAnimBakedData中数据提取出来
    public struct BlobVariationData
    {
        public int p_eventFrame;
        // 变体级播放速率倍率，1.0 = 正常速度，与 AnimUpdateSystem.s_animFPS 配合使用
        public float p_frameRate;
        public int p_animFrameCount;
        public BlobArray<int> p_animStartOffset;
    }

    public struct BlobStateData
    {
        public uint p_stateId;
        public bool p_isLoop;
        public BlobArray<BlobVariationData> p_variations;
    }

    public struct BlobElementData
    {
        public AnimEneityId p_eleAnimId; //每种entity一个id
        public BlobArray<BlobStateData> p_states;
    }

    //每个entity使用AnimationRuntimeState记录当前的动画info
    public struct AnimationRuntimeState : IComponentData
    {
        public BlobAssetReference<BlobElementData> p_elementBlobRef;

        public uint p_currentStateId;
        public uint p_previousStateId; // 用于检测状态是否改变

        public int p_currentVariationIndex;
        public int p_currentDirectionIndex;

        public float p_elapsedTime;  //是计算在一个循环之内的时间的累加,不是总的时间, 比如运行了1.5个循环了,这个时间就是0.5个循环的时间
        public int p_currentFrameIndex;
        public int p_targetTextureSliceIndex;

        // 用于有多个variation是计算随机数
        public Unity.Mathematics.Random p_random;

        public int p_eventTriggerCount;
        public int p_finishCount;

        public float p_animRate;

        // 渲染快照槽位 (稳定，不随 Entity.Index 复用变化)。-1 表示未注册。
        // 由 AnimCtrl 在 Register/Unregister 时通过 SetComponentData 维护。
        public int p_renderSlot;

        // 缓存 BlobElementData.p_states 中匹配 currentStateId 的索引，
        // 消除 AnimUpdateJob 每帧线性查找。-1 表示未缓存。
        public int p_cachedStateIndex;
    }

    // 向ECS层批量传递动画状态的跟新, state的变化或者dir的变化, 与entity的增删没有关系
    public struct AnimSyncCommand
    {
        public Entity p_targetAnimEntity;
        public uint p_stateId;
        public int p_directionIndex;
        public float p_animRate;
        // 强制重播同一动画状态时置 true：AnimSyncJob 会将 p_previousStateId 设为 uint.MaxValue
        // 使 AnimUpdateJob 检测到"状态变化"从而重置动画计时和事件计数
        public bool p_forceReset;
    }
}
