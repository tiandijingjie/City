using System.Collections.Generic;
using System.Xml;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using WarField.EffectAnim;

// NOTE: EffectCtrl previously managed a prefab pool (EffectBase / SkillEffect* subclasses).
//       That model has been abolished. All VFX are now ECS entities rendered via
//       DrawMeshInstanced with the EffectAnimTex2dArrayUnlit shader.
//
// Migration path for old EffectBase subclasses:
//   1. Implement IEffectAnimInfo on the owner (weapon, skill, building, etc.)
//   2. Bake the sprite sheet with Tools/Effect Animation Baker → EffectAnimConf.xml
//   3. Call EffectCtrl.Instance.BindEffectAnimWithEntity once per element type.
//   4. Spawn via EffectCtrl.Instance.AddEffectAt — get back an EffectHandle.
//   5. Release via EffectCtrl.Instance.ReleaseEffect.

namespace WarField
{
    using WE = WarFieldElements;
    using GD = GlobalDefines;

    public class EffectCtrl : MonoBehaviour
    {
#region public parameters
        public static EffectCtrl Instance;
#endregion

#region serialized fields
        [SerializeField] private float _animFPS     = 30f;
        [SerializeField] private float _cullPadding = 3f;

        // Skill-targeting indicator prefabs (non-VFX; still GameObject-based)
        [SerializeField] private GameObject[] _effectIndicatorPfb;
#endregion

#region private — ECS plumbing

        private static EntityManager   _entityManager;
        private static EntityArchetype _effectArchetype;

        private NativeList<EffectAnimSyncCommand>  _syncCommands;
        private NativeArray<EffectAnimSyncCommand> _cmdBuffer;
        private NativeArray<EffectAnimRenderSnapshot> _renderSnapshots;

#endregion

#region private — per-slot hot data

        // EffectSlotData holds every field the two hot loops (rendering + event detection)
        // touch each frame.  Stored in a NativeArray so iterations are cache-line sequential
        // instead of chasing heap pointers like a managed class would require.
        //
        // Layout: Entity(8) + uint(4) + int(4) + float(4) + bool(1+3pad) + int(4) + float(4)
        //         + int(4) + int(4) = 40 bytes/slot → fits in one 64-byte cache line.
        // 5000 active effects ≈ 200 KB → comfortably in L2 cache.
        private struct EffectSlotData
        {
            public Entity p_entity;           // ECS handle — also used for stale-handle validation
            public uint   p_effectAnimId;     // which RenderGroup this slot belongs to
            public int    p_groupSlotIndex;   // position within RenderGroup.p_slotList (O(1) swap-back)
            public float  p_alpha;
            public bool   p_isVisible;
            public int    p_directionIndex;
            public float  p_animRate;
            public int    p_lastEventCount;   // last snapshot.p_eventCount seen by event loop
            public int    p_lastFinishCount;  // last snapshot.p_finishCount seen by event loop
        }

        // Hot data — all rendering/event reads go here.  Length == kCapacity; active range is [0, _slotCount).
        private NativeArray<EffectSlotData> _slotData;

        // Cold data — callback host reference; accessed only when an event actually fires.
        // Plain managed array: managed references cannot live in NativeArray.
        private IEffectAnimInfo[] _slotHosts;

        // Active slot count (== "filled" prefix of _slotData / _slotHosts / _worldMatrixCache)
        private int _slotCount;

        private NativeArray<Matrix4x4> _worldMatrixCache;

        private const int kCapacity = 8192; // headroom for 5k+ simultaneous effects

#endregion

#region private — render groups

        private class RenderGroup
        {
            public uint      p_effectAnimId;
            public Material  p_material;
            public Mesh      p_mesh;
            public List<int> p_slotList = new List<int>();
        }

        private Dictionary<uint, RenderGroup> _renderGroups;
        private List<RenderGroup>             _activeGroups;

#endregion

#region private — GPU instancing batch

        private Matrix4x4[]           _matrixBatch;
        private float[]               _sliceBatch;
        private float[]               _alphaBatch;
        private MaterialPropertyBlock _mpb;
        private int _slicePropId;
        private int _alphaPropId;

#endregion

#region private — deferred event queue

        // Uses slot index + entity snapshot to avoid stale-slot dispatch if ReleaseEffect is
        // called from inside a callback (which swaps the slot's content).
        private struct PendingEvent { public int p_slot; public int p_stateId; public Entity p_entity; }
        private List<PendingEvent> _pendingEvents;

#endregion

#region private — animation config (from merged EffectAnimCtrl)

        private Dictionary<string, ElementEffectAnimBakedData>           _elementDataDict;
        private Dictionary<uint, BlobAssetReference<BlobEffectAnimData>> _animBlobs;
        private Dictionary<uint, ElementEffectAnimBakedData>             _bakedDataDict;
        private Dictionary<uint, Material>                               _runtimeMaterials;
        private readonly List<AsyncOperationHandle>                      _addressableHandles = new List<AsyncOperationHandle>();

        private WE.LodLevel _curGlobLodLevel = WE.LodLevel.MIN;
        private bool        _beInited;

#endregion

#region private — indicators (still GameObject-based)

        private EffectIndicator[] _effectIndicatorArr;

#endregion

#region private — CircleAreaEffectAnim pool

        private readonly Queue<CircleAreaEffectAnim> _circleAreaPool = new Queue<CircleAreaEffectAnim>();

#endregion

#region Unity callbacks

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            _animBlobs        = new Dictionary<uint, BlobAssetReference<BlobEffectAnimData>>();
            _bakedDataDict    = new Dictionary<uint, ElementEffectAnimBakedData>();
            _runtimeMaterials = new Dictionary<uint, Material>();

            _entityManager   = World.DefaultGameObjectInjectionWorld.EntityManager;
            _effectArchetype = _entityManager.CreateArchetype(typeof(EffectAnimRuntimeState));

            _syncCommands = new NativeList<EffectAnimSyncCommand>(512, Allocator.Persistent);
            _cmdBuffer    = new NativeArray<EffectAnimSyncCommand>(512, Allocator.Persistent);

            _slotData         = new NativeArray<EffectSlotData>(kCapacity, Allocator.Persistent);
            _slotHosts        = new IEffectAnimInfo[kCapacity];
            _slotCount        = 0;
            _worldMatrixCache = new NativeArray<Matrix4x4>(kCapacity, Allocator.Persistent);
            _renderSnapshots  = new NativeArray<EffectAnimRenderSnapshot>(kCapacity, Allocator.Persistent);
            EffectAnimRenderExportSystem.Initialize(_renderSnapshots);

            _renderGroups  = new Dictionary<uint, RenderGroup>();
            _activeGroups  = new List<RenderGroup>();
            _pendingEvents = new List<PendingEvent>(256);

            _matrixBatch = new Matrix4x4[GD.MaxGPUInstances];
            _sliceBatch  = new float[GD.MaxGPUInstances];
            _alphaBatch  = new float[GD.MaxGPUInstances];
            _mpb         = new MaterialPropertyBlock();
            _slicePropId = Shader.PropertyToID("_FinalSliceIndex");
            _alphaPropId = Shader.PropertyToID("_Alpha");

            _effectIndicatorArr = new EffectIndicator[(int)EffectDefines.SkillIndicatorType.MAX];

            LoadEffectAnimConf();
            _beInited = true;
        }

        private void Update()
        {
            if (!_beInited) return;
            if (_curGlobLodLevel != CameraCtrl.Instance.gs_lodLevel)
            {
                _curGlobLodLevel = CameraCtrl.Instance.gs_lodLevel;
                ApplyGlobalMaterialLodSwitch(_curGlobLodLevel);
            }
        }

        private void LateUpdate()
        {
            if (!_beInited) return;

            EffectAnimGlobalData.s_animFPS = _animFPS;

            // ── Step 1: wait for ECS export, then draw ───────────────────────────
            EffectAnimRenderExportSystem.CompleteExport();

            NativeArray<EffectAnimRenderSnapshot> snaps = _renderSnapshots;
            NativeArray<Matrix4x4>               mats  = _worldMatrixCache;
            NativeArray<EffectSlotData>           slots = _slotData;
            int snapLen    = snaps.Length;
            int groupCount = _activeGroups.Count;

            float camCX = 0f, camCY = 0f, camHalfW = float.MaxValue, camHalfH = float.MaxValue;
            Camera mainCam = CameraCtrl.Instance != null ? CameraCtrl.Instance.gs_mainCamera : null;
            if (mainCam != null)
            {
                Vector3 cp = mainCam.transform.position;
                camCX    = cp.x;
                camCY    = cp.y;
                camHalfH = mainCam.orthographicSize + _cullPadding;
                camHalfW = camHalfH * mainCam.aspect;
            }

            // Rendering loop — all hot reads come from NativeArrays (cache-line sequential)
            for (int g = 0; g < groupCount; g++)
            {
                RenderGroup group     = _activeGroups[g];
                List<int>   slotList  = group.p_slotList;
                int         slotCount = slotList.Count;
                if (slotCount == 0) continue;

                int batchCount = 0;
                for (int i = 0; i < slotCount; i++)
                {
                    int slot = slotList[i];
                    if ((uint)slot >= (uint)_slotCount || (uint)slot >= (uint)snapLen) continue;

                    EffectSlotData sd = slots[slot];    // NativeArray struct read
                    if (!sd.p_isVisible) continue;

                    float px = mats[slot].m03;
                    float py = mats[slot].m13;
                    if (math.abs(px - camCX) > camHalfW || math.abs(py - camCY) > camHalfH)
                        continue;

                    _matrixBatch[batchCount] = mats[slot];
                    _sliceBatch[batchCount]  = snaps[slot].p_sliceIndex;
                    _alphaBatch[batchCount]  = sd.p_alpha;
                    batchCount++;

                    if (batchCount == GD.MaxGPUInstances)
                    {
                        FlushDrawBatch(group.p_mesh, group.p_material, batchCount);
                        batchCount = 0;
                    }
                }
                if (batchCount > 0)
                    FlushDrawBatch(group.p_mesh, group.p_material, batchCount);
            }

            // ── Step 2: event detection — sequential NativeArray scan ─────────────
            _pendingEvents.Clear();
            int activeSlots = _slotCount;
            for (int i = 0; i < activeSlots; i++)
            {
                EffectSlotData sd = slots[i];
                if (sd.p_entity == Entity.Null) continue;
                if ((uint)i >= (uint)snapLen) continue;

                EffectAnimRenderSnapshot snap = snaps[i];
                bool dirty = false;

                // eventFrame crossing
                if (snap.p_eventCount != sd.p_lastEventCount)
                {
                    if (snap.p_eventCount > sd.p_lastEventCount)
                        _pendingEvents.Add(new PendingEvent { p_slot = i, p_stateId = 0, p_entity = sd.p_entity });
                    sd.p_lastEventCount = snap.p_eventCount;
                    dirty = true;
                }

                // animation finish
                if (snap.p_finishCount != sd.p_lastFinishCount)
                {
                    if (snap.p_finishCount > sd.p_lastFinishCount)
                        _pendingEvents.Add(new PendingEvent { p_slot = i, p_stateId = -1, p_entity = sd.p_entity });
                    sd.p_lastFinishCount = snap.p_finishCount;
                    dirty = true;
                }

                if (dirty) _slotData[i] = sd; // write back only if changed
            }

            // Dispatch events — validate entity to guard against ReleaseEffect inside a callback
            // causing slot-reuse (swap-back changes what entity lives at ev.p_slot).
            int evtCount = _pendingEvents.Count;
            for (int e = 0; e < evtCount; e++)
            {
                PendingEvent ev = _pendingEvents[e];
                if ((uint)ev.p_slot < (uint)_slotCount &&
                    _slotData[ev.p_slot].p_entity == ev.p_entity)
                    _slotHosts[ev.p_slot]?.IEffectAnimInfo_OnEffectAnimEvent(ev.p_stateId);
            }

            // ── Step 3: flush ECS sync commands ──────────────────────────────────
            EffectAnimSyncSystem.WaitForLastJob();

            int cmdCount = _syncCommands.Length;
            if (cmdCount > 0)
            {
                EnsureCmdBufferCapacity(cmdCount);
                NativeArray<EffectAnimSyncCommand>.Copy(_syncCommands.AsArray(), _cmdBuffer, cmdCount);
                EffectAnimSyncSystem.SetCommandBuffer(_cmdBuffer, cmdCount);
                _syncCommands.Clear();
            }
        }

        private void OnDestroy()
        {
            EffectAnimSyncSystem.Shutdown();
            EffectAnimRenderExportSystem.Shutdown();

            // Destroy every active ECS entity BEFORE disposing blobs.
            // If the ECS World continues after this MonoBehaviour is destroyed,
            // EffectAnimUpdateJob would otherwise access the now-invalid BlobAssetReferences.
            if (_slotData.IsCreated && _entityManager != default)
            {
                for (int i = 0; i < _slotCount; i++)
                {
                    Entity e = _slotData[i].p_entity;
                    if (e != Entity.Null && _entityManager.Exists(e))
                        _entityManager.DestroyEntity(e);
                }
            }

            foreach (var pair in _animBlobs)
                if (pair.Value.IsCreated) pair.Value.Dispose();
            _animBlobs.Clear();

            foreach (var mat in _runtimeMaterials.Values)
                if (mat != null) Destroy(mat);
            _runtimeMaterials.Clear();

            if (_syncCommands.IsCreated)     _syncCommands.Dispose();
            if (_cmdBuffer.IsCreated)        _cmdBuffer.Dispose();
            if (_slotData.IsCreated)         _slotData.Dispose();
            if (_worldMatrixCache.IsCreated) _worldMatrixCache.Dispose();
            if (_renderSnapshots.IsCreated)  _renderSnapshots.Dispose();

            foreach (var h in _addressableHandles)
                if (h.IsValid()) Addressables.Release(h);
            _addressableHandles.Clear();

            if (Instance == this) Instance = null;
        }

#endregion

#region public functions — animation binding

        // ── EffectAnimConf.xml XML format ─────────────────────────────────────────
        //
        //   <effectAnimCfgs>
        //     <anim>
        //       <description>...</description>
        //       <name>FireBall</name>
        //       <path>Animation/Effects/Projectiles/FireBall</path>
        //       <clip frameCount="8" frameRate="1.0" eventFrame="-1"
        //             isLoop="false" worldSize="1.0" offsets="0,8,16"/>
        //     </anim>
        //   </effectAnimCfgs>

        /// <summary>
        /// Build the BlobEffectAnimData and runtime Material for <paramref name="entityName"/>.
        /// Call ONCE per element type before any AddEffectAt for that type.
        /// </summary>
        public bool BindEffectAnimWithEntity(uint effectAnimId, string entityName,
            out BlobAssetReference<BlobEffectAnimData> blobRef)
        {
            blobRef = default;
            if (!_beInited) return false;

            if (!_elementDataDict.TryGetValue(entityName, out var conf) || conf == null)
            {
                GameLogger.LogError($"EffectCtrl: '{entityName}' not found in EffectAnimConf.xml. " +
                                    $"Run Tools/Effect Animation Baker first.");
                return false;
            }

            if (_animBlobs.ContainsKey(effectAnimId))
            {
                GameLogger.LogDebug($"EffectCtrl: {entityName} ({effectAnimId}) already bound.");
                return false;
            }

            EffectAnimClipData clip = conf.p_clip;

            using var builder = new BlobBuilder(Allocator.Temp);
            ref BlobEffectAnimData root = ref builder.ConstructRoot<BlobEffectAnimData>();
            root.p_effectAnimId = effectAnimId;
            root.p_frameCount   = clip.p_frameCount;
            root.p_frameRate    = clip.p_frameRate;
            root.p_eventFrame   = clip.p_eventFrame;
            root.p_isLoop       = clip.p_isLoop;

            int dirCount = clip.p_dirStartOffsets.Count;
            BlobBuilderArray<int> blobOffsets = builder.Allocate(ref root.p_dirStartOffsets, dirCount);
            for (int i = 0; i < dirCount; i++)
                blobOffsets[i] = clip.p_dirStartOffsets[i];

            var blob = builder.CreateBlobAssetReference<BlobEffectAnimData>(Allocator.Persistent);
            _animBlobs.Add(effectAnimId, blob);
            _bakedDataDict[effectAnimId] = conf;

            Shader sh = Shader.Find("WarField/EffectAnimTex2dArrayUnlit");
            if (sh != null)
            {
                var mat = new Material(sh);
                if (conf.p_hdColorArray != null)
                    mat.SetTexture("_MainTexArray", conf.p_hdColorArray);
                mat.SetFloat("_TotalWorldSize", clip.p_worldSize > 0f ? clip.p_worldSize : 1f);
                mat.enableInstancing = true;
                _runtimeMaterials[effectAnimId] = mat;
            }
            else
            {
                GameLogger.LogError("EffectCtrl: Shader 'WarField/EffectAnimTex2dArrayUnlit' not found in build.");
            }

            blobRef = blob;
            return true;
        }

#endregion

#region public functions — effect lifecycle

        /// <summary>
        /// Spawn an effect at <paramref name="worldPos"/>.
        /// Returns an <see cref="EffectHandle"/> used to control or release the effect.
        /// </summary>
        public EffectHandle AddEffectAt(Vector3 worldPos, IEffectAnimInfo animInfo, int dirIndex = 0)
        {
            if (!_beInited || animInfo == null) return EffectHandle.Invalid;

            uint effectAnimId = animInfo.IEffectAnimInfo_GetEffectAnimId();

            if (!_animBlobs.TryGetValue(effectAnimId, out var blobRef))
            {
                GameLogger.LogError($"EffectCtrl: effectAnimId {effectAnimId} not bound.");
                return EffectHandle.Invalid;
            }

            if (!_runtimeMaterials.TryGetValue(effectAnimId, out var mat))
            {
                GameLogger.LogError($"EffectCtrl: No runtime material for effectAnimId {effectAnimId}.");
                return EffectHandle.Invalid;
            }

            if (!_renderGroups.TryGetValue(effectAnimId, out var group))
            {
                if (!_bakedDataDict.TryGetValue(effectAnimId, out var data))
                    return EffectHandle.Invalid;

                group = new RenderGroup
                {
                    p_effectAnimId = effectAnimId,
                    p_material     = mat,
                    p_mesh         = data.p_bakedMesh,
                };
                _renderGroups.Add(effectAnimId, group);
                _activeGroups.Add(group);
            }

            int slot = _slotCount;
            if (slot >= _worldMatrixCache.Length)
                ResizeBuffers(slot * 2 + 128);

            Entity entity = _entityManager.CreateEntity(_effectArchetype);
            _entityManager.SetComponentData(entity, new EffectAnimRuntimeState
            {
                p_blobRef                 = blobRef,
                p_currentDirectionIndex   = dirIndex,
                p_elapsedTime             = 0f,
                p_currentFrameIndex       = 0,
                p_targetTextureSliceIndex = 0,
                p_eventTriggerCount       = 0,
                p_finishCount             = 0,
                p_animRate                = 1.0f,
                p_renderSlot              = slot,
                p_shouldReset             = false,
            });

            _worldMatrixCache[slot] = Matrix4x4.Translate(worldPos);

            _slotData[slot] = new EffectSlotData
            {
                p_entity          = entity,
                p_effectAnimId    = effectAnimId,
                p_groupSlotIndex  = group.p_slotList.Count,
                p_alpha           = 1.0f,
                p_isVisible       = true,
                p_directionIndex  = dirIndex,
                p_animRate        = 1.0f,
                p_lastEventCount  = 0,
                p_lastFinishCount = 0,
            };
            _slotHosts[slot] = animInfo;
            _slotCount++;

            group.p_slotList.Add(slot);
            return new EffectHandle { p_renderSlot = slot, p_entity = entity };
        }

        /// <summary>
        /// Destroy the effect entity and free its slot.  The handle is invalid after this call.
        /// </summary>
        public void ReleaseEffect(EffectHandle handle)
        {
            int slot = ValidateSlot(handle);
            if (slot < 0) return;

            EffectSlotData sd = _slotData[slot];

            // Destroy ECS entity
            if (_entityManager.Exists(sd.p_entity))
                _entityManager.DestroyEntity(sd.p_entity);

            // Remove from render group (swap-back)
            if (_renderGroups.TryGetValue(sd.p_effectAnimId, out var group))
            {
                int gi    = sd.p_groupSlotIndex;
                int gLast = group.p_slotList.Count - 1;
                if (gi >= 0 && gi <= gLast)
                {
                    if (gi < gLast)
                    {
                        int movedSlot = group.p_slotList[gLast];
                        group.p_slotList[gi] = movedSlot;
                        // Patch group index of the moved slot
                        var md = _slotData[movedSlot];
                        md.p_groupSlotIndex = gi;
                        _slotData[movedSlot] = md;
                    }
                    group.p_slotList.RemoveAt(gLast);
                }
            }

            // Remove from flat arrays (swap-back)
            int last = _slotCount - 1;
            if (slot < last)
            {
                EffectSlotData moved = _slotData[last];
                _slotData[slot]         = moved;
                _slotHosts[slot]        = _slotHosts[last];
                _worldMatrixCache[slot] = _worldMatrixCache[last];

                // Patch moved slot's render group list
                if (_renderGroups.TryGetValue(moved.p_effectAnimId, out var movedGroup))
                {
                    int idx = movedGroup.p_slotList.IndexOf(last);
                    if (idx >= 0) movedGroup.p_slotList[idx] = slot;
                }

                // Patch moved entity's ECS render slot
                if (_entityManager.Exists(moved.p_entity))
                {
                    var st = _entityManager.GetComponentData<EffectAnimRuntimeState>(moved.p_entity);
                    st.p_renderSlot = slot;
                    _entityManager.SetComponentData(moved.p_entity, st);
                }
            }
            _slotHosts[last] = null; // release managed ref so GC can collect
            _slotCount--;
        }

#endregion

#region public functions — per-effect control

        /// <summary>Update world position (call every frame for moving effects like projectiles).</summary>
        public void UpdateEffectPosition(EffectHandle handle, Vector3 worldPos)
        {
            // ValidateSlot guards against writing the wrong matrix when a handle is stale
            // (effect was released and the slot was reused by another effect).
            int slot = ValidateSlot(handle);
            if (slot < 0) return;
            _worldMatrixCache[slot] = Matrix4x4.Translate(worldPos);
        }

        public void ChangeEffectDirection(EffectHandle handle, int dirIndex)
        {
            int slot = ValidateSlot(handle);
            if (slot < 0) return;
            var sd = _slotData[slot];
            if (sd.p_directionIndex == dirIndex) return;
            sd.p_directionIndex = dirIndex;
            _slotData[slot] = sd;
            EnqueueSync(sd.p_entity, dirIndex, sd.p_animRate, false);
        }

        public void SetEffectAlpha(EffectHandle handle, float alpha)
        {
            int slot = ValidateSlot(handle);
            if (slot < 0) return;
            var sd = _slotData[slot];
            sd.p_alpha = alpha;
            _slotData[slot] = sd;
        }

        public void SetEffectVisible(EffectHandle handle, bool visible)
        {
            int slot = ValidateSlot(handle);
            if (slot < 0) return;
            var sd = _slotData[slot];
            sd.p_isVisible = visible;
            _slotData[slot] = sd;
        }

        public void SetEffectAnimRate(EffectHandle handle, float animRate)
        {
            int slot = ValidateSlot(handle);
            if (slot < 0) return;
            var sd = _slotData[slot];
            if (math.abs(sd.p_animRate - animRate) < 0.001f) return;
            sd.p_animRate = animRate;
            _slotData[slot] = sd;
            EnqueueSync(sd.p_entity, sd.p_directionIndex, animRate, false);
        }

        public void ResetEffectAnim(EffectHandle handle)
        {
            int slot = ValidateSlot(handle);
            if (slot < 0) return;
            var sd = _slotData[slot];
            sd.p_lastEventCount  = 0;
            sd.p_lastFinishCount = 0;
            _slotData[slot] = sd;
            EnqueueSync(sd.p_entity, sd.p_directionIndex, sd.p_animRate, true);
        }

        /// <summary>Borrow a pooled <see cref="CircleAreaEffectAnim"/> instance.</summary>
        public CircleAreaEffectAnim AcquireCircleAreaEffect()
        {
            return _circleAreaPool.Count > 0
                ? _circleAreaPool.Dequeue()
                : new CircleAreaEffectAnim(ReturnCircleAreaEffect);
        }

        private void ReturnCircleAreaEffect(CircleAreaEffectAnim item)
        {
            _circleAreaPool.Enqueue(item);
        }

        /// <summary>Get (or lazy-create) the targeting indicator overlay.</summary>
        public EffectIndicator GetEffectIndicator(EffectDefines.SkillIndicatorType type)
        {
            int idx = (int)type;
            if (_effectIndicatorArr[idx] == null && _effectIndicatorPfb != null && idx < _effectIndicatorPfb.Length)
                _effectIndicatorArr[idx] = Instantiate(_effectIndicatorPfb[idx], transform)
                    .GetComponent<EffectIndicator>();
            return _effectIndicatorArr[idx];
        }

#endregion

#region private helpers

        /// <summary>Returns the render slot if the handle is valid and not stale; -1 otherwise.</summary>
        private int ValidateSlot(EffectHandle handle)
        {
            if (!handle.IsValid()) return -1;
            int slot = handle.p_renderSlot;
            if ((uint)slot >= (uint)_slotCount) return -1;
            return _slotData[slot].p_entity == handle.p_entity ? slot : -1;
        }

        private void EnqueueSync(Entity entity, int dirIndex, float animRate, bool forceReset)
        {
            _syncCommands.Add(new EffectAnimSyncCommand
            {
                p_targetEntity   = entity,
                p_directionIndex = dirIndex,
                p_animRate       = animRate,
                p_forceReset     = forceReset,
            });
        }

        private void FlushDrawBatch(Mesh mesh, Material mat, int count)
        {
            _mpb.Clear();
            _mpb.SetFloatArray(_slicePropId, _sliceBatch);
            _mpb.SetFloatArray(_alphaPropId, _alphaBatch);
            Graphics.DrawMeshInstanced(mesh, 0, mat, _matrixBatch, count, _mpb,
                UnityEngine.Rendering.ShadowCastingMode.Off, false);
        }

        private void ResizeBuffers(int newCap)
        {
            newCap = math.max(newCap, kCapacity);

            // Complete any in-flight ECS export job before disposing the old snapshot array.
            // ResizeBuffers can be called outside LateUpdate (e.g. during building init), so the
            // export job from the current ECS tick might still be writing to _renderSnapshots.
            EffectAnimRenderExportSystem.CompleteExport();

            var newSlots = new NativeArray<EffectSlotData>(newCap, Allocator.Persistent);
            var newMats  = new NativeArray<Matrix4x4>(newCap, Allocator.Persistent);
            var newSnaps = new NativeArray<EffectAnimRenderSnapshot>(newCap, Allocator.Persistent);

            int copy = math.min(_slotCount, _slotData.Length);
            NativeArray<EffectSlotData>.Copy(_slotData, newSlots, copy);
            NativeArray<Matrix4x4>.Copy(_worldMatrixCache, newMats, copy);
            int snapCopy = math.min(copy, _renderSnapshots.Length);
            NativeArray<EffectAnimRenderSnapshot>.Copy(_renderSnapshots, newSnaps, snapCopy);

            _slotData.Dispose();
            _worldMatrixCache.Dispose();
            _renderSnapshots.Dispose();
            _slotData         = newSlots;
            _worldMatrixCache = newMats;
            _renderSnapshots  = newSnaps;

            // Resize managed host array
            var newHosts = new IEffectAnimInfo[newCap];
            System.Array.Copy(_slotHosts, newHosts, math.min(_slotHosts.Length, copy));
            _slotHosts = newHosts;

            EffectAnimRenderExportSystem.Initialize(_renderSnapshots);
        }

        private void EnsureCmdBufferCapacity(int count)
        {
            if (_cmdBuffer.IsCreated && _cmdBuffer.Length >= count) return;
            int newCap = _cmdBuffer.IsCreated ? math.max(count, _cmdBuffer.Length * 2) : 512;
            newCap = math.max(newCap, count);
            if (_cmdBuffer.IsCreated) _cmdBuffer.Dispose();
            _cmdBuffer = new NativeArray<EffectAnimSyncCommand>(newCap, Allocator.Persistent);
        }

#endregion

#region private — config loading (merged from EffectAnimCtrl)

        private void LoadEffectAnimConf()
        {
            _elementDataDict = new Dictionary<string, ElementEffectAnimBakedData>();

            TextAsset xmlAsset = Resources.Load<TextAsset>("Conf/EffectAnimConf");
            if (xmlAsset == null)
            {
                GameLogger.LogError("EffectCtrl: Resources/Conf/EffectAnimConf.xml not found. " +
                                    "Run Tools/Effect Animation Baker to generate it.");
                return;
            }

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xmlAsset.text);

            XmlNodeList animNodes = doc.SelectNodes("effectAnimCfgs/anim");
            if (animNodes == null) return;

            foreach (XmlNode node in animNodes)
            {
                if (node.NodeType == XmlNodeType.Comment) continue;

                string name = node.SelectSingleNode("name")?.InnerText?.Trim();
                string path = node.SelectSingleNode("path")?.InnerText?.Trim();

                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path))
                {
                    GameLogger.LogWarning("EffectCtrl: EffectAnimConf.xml has an <anim> missing <name> or <path>, skipped.");
                    continue;
                }

                var data = LoadElementEffectAnimData(name, path, node);
                if (data != null)
                    _elementDataDict[name] = data;
            }

            GameLogger.LogDebug($"EffectCtrl: Loaded {_elementDataDict.Count} element(s) from EffectAnimConf.xml.");
        }

        private ElementEffectAnimBakedData LoadElementEffectAnimData(string name, string path, XmlNode animNode)
        {
            var data = new ElementEffectAnimBakedData { p_elementName = name };

            data.p_hdColorArray = LoadAddressable<Texture2DArray>($"{path}/{name}_HD_Color");
            data.p_mdColorArray = LoadAddressable<Texture2DArray>($"{path}/{name}_MD_Color");
            data.p_ldColorArray = LoadAddressable<Texture2DArray>($"{path}/{name}_LD_Color");

            if (data.p_hdColorArray == null)
                GameLogger.LogWarning($"EffectCtrl: Addressable '{path}/{name}_HD_Color' not found. Rebake the element.");

            data.p_bakedMesh = LoadAddressable<Mesh>($"{path}/{name}_OctagonMesh");
            data.p_clip = ParseClipFromXml(animNode.SelectSingleNode("clip"));
            return data;
        }

        private T LoadAddressable<T>(string address) where T : UnityEngine.Object
        {
            var handle = Addressables.LoadAssetAsync<T>(address);
            T result = handle.WaitForCompletion();
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                _addressableHandles.Add(handle);
                return result;
            }
            Addressables.Release(handle);
            return null;
        }

        private static EffectAnimClipData ParseClipFromXml(XmlNode clipNode)
        {
            var clip = new EffectAnimClipData
            {
                p_frameCount = 1,
                p_frameRate  = 1f,
                p_eventFrame = -1,
                p_isLoop     = false,
                p_worldSize  = 1f,
            };
            if (clipNode == null) return clip;

            if (int.TryParse(clipNode.Attributes?["frameCount"]?.Value, out int fc))
                clip.p_frameCount = fc;

            if (float.TryParse(clipNode.Attributes?["frameRate"]?.Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float fr))
                clip.p_frameRate = fr;

            if (int.TryParse(clipNode.Attributes?["eventFrame"]?.Value, out int ef))
                clip.p_eventFrame = ef;

            string loopStr = clipNode.Attributes?["isLoop"]?.Value ?? "false";
            clip.p_isLoop = string.Equals(loopStr, "true", System.StringComparison.OrdinalIgnoreCase);

            if (float.TryParse(clipNode.Attributes?["worldSize"]?.Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float ws) && ws > 0f)
                clip.p_worldSize = ws;

            string offsetsStr = clipNode.Attributes?["offsets"]?.Value ?? "";
            foreach (string part in offsetsStr.Split(','))
                if (int.TryParse(part.Trim(), out int offset))
                    clip.p_dirStartOffsets.Add(offset);

            return clip;
        }

        private void ApplyGlobalMaterialLodSwitch(WE.LodLevel newLod)
        {
            foreach (var kv in _runtimeMaterials)
            {
                if (!_bakedDataDict.TryGetValue(kv.Key, out var data)) continue;
                Material mat = kv.Value;
                if (mat == null) continue;

                Texture2DArray target = data.p_hdColorArray;
                switch (newLod)
                {
                    case WE.LodLevel.MD: if (data.p_mdColorArray != null) target = data.p_mdColorArray; break;
                    case WE.LodLevel.LD: if (data.p_ldColorArray != null) target = data.p_ldColorArray; break;
                }
                if (target != null)
                    mat.SetTexture("_MainTexArray", target);
            }
        }

#endregion
    }
}
