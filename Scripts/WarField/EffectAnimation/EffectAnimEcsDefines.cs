using Unity.Entities;

namespace WarField.EffectAnim
{
    // BlobEffectAnimData mirrors ElementEffectAnimBakedData inside a Blob for zero-copy Burst access.
    // Intentionally separate from WarField.Anim.BlobAnimData — the two systems must never cross-query.
    public struct BlobEffectAnimData
    {
        public uint  p_effectAnimId;
        public int   p_frameCount;
        public float p_frameRate;      // speed multiplier, works with EffectAnimGlobalData.s_animFPS
        public int   p_eventFrame;     // -1 = no event
        public bool  p_isLoop;
        // p_dirStartOffsets[dirIndex] = absolute slice index where that direction's frames begin
        public BlobArray<int> p_dirStartOffsets;
    }

    // Per-entity runtime state for EffectAnim entities.
    // Distinct component type from AnimationRuntimeState so ECS systems never accidentally query both.
    public struct EffectAnimRuntimeState : IComponentData
    {
        public BlobAssetReference<BlobEffectAnimData> p_blobRef;

        public int   p_currentDirectionIndex;
        public float p_elapsedTime;
        public int   p_currentFrameIndex;
        public int   p_targetTextureSliceIndex;

        public int   p_eventTriggerCount;
        public int   p_finishCount;

        public float p_animRate;

        // Assigned by EffectAnimCtrl during RegisterRenderProxy; -1 = unregistered.
        public int   p_renderSlot;

        // Set true via EffectAnimSyncCommand to restart animation from frame 0 on the next ECS tick.
        public bool  p_shouldReset;
    }

    // Lightweight command transmitted from the main thread to EffectAnimSyncSystem each LateUpdate.
    public struct EffectAnimSyncCommand
    {
        public Entity p_targetEntity;
        public int    p_directionIndex;
        public float  p_animRate;
        // When true, EffectAnimSyncJob sets p_shouldReset = true, causing the update job
        // to restart elapsedTime, frameIndex, and event counters on the same tick.
        public bool   p_forceReset;
    }
}
