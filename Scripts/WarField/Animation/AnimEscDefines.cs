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

        public float p_elapsedTime;
        public int p_currentFrameIndex;
        public int p_targetTextureSliceIndex;

        // 用于有多个variation是计算随机数
        public Unity.Mathematics.Random p_random;
    }
}
