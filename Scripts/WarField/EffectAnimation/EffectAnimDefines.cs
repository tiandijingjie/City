using System;
using System.Collections.Generic;
using UnityEngine;

namespace WarField.EffectAnim
{
    // EffectAnim has a single clip per element (no state, no variation).
    // Multiple directions are supported via per-direction start offsets into the Texture2DArray.
    [Serializable]
    public class EffectAnimClipData
    {
        // Frames per direction
        public int   p_frameCount;
        // Speed multiplier (1.0 = normal). Works together with EffectCtrl._animFPS.
        public float p_frameRate;
        // Texture2DArray slice index (relative to direction start) that fires the event callback.
        // -1 = no event frame.
        public int   p_eventFrame;
        // Loop or one-shot
        public bool  p_isLoop;
        // Physical size of the sprite in world units (meters). Used to set _TotalWorldSize on the
        // runtime Material so the shader scales the Mesh to the correct world footprint.
        public float p_worldSize = 1.0f;
        // Absolute start slice index in Texture2DArray for each direction.
        // dirCount == 1 for omnidirectional effects.
        public List<int> p_dirStartOffsets = new List<int>();
    }

    // All runtime data needed for one EffectAnim element — textures, mesh, and clip timing.
    // Loaded from EffectAnimConf.xml + Addressables during EffectAnimCtrl.Awake.
    [Serializable]
    public class ElementEffectAnimBakedData
    {
        public string p_elementName;
        public EffectAnimClipData p_clip = new EffectAnimClipData();
        public Mesh p_bakedMesh;

        // Three LOD tiers (colour only — EffectAnim does not use normal maps)
        public Texture2DArray p_hdColorArray;
        public Texture2DArray p_mdColorArray;
        public Texture2DArray p_ldColorArray;
    }

    // Implement on any host object that drives an EffectAnimRenderProxy.
    public interface IEffectAnimInfo
    {
        // Returns a stable per-element ID used to look up the blob in EffectAnimCtrl.
        uint IEffectAnimInfo_GetEffectAnimId();

        // Called by EffectAnimCtrl when an animation event fires.
        //   eventId == -1  → animation finished (non-loop reached last frame)
        //   eventId >=  0  → the configured eventFrame was crossed
        void IEffectAnimInfo_OnEffectAnimEvent(int eventId);
    }
}
