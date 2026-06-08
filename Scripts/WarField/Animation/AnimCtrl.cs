using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

using WarField.Anim;
namespace WarField
{
    using WE = WarFieldElements;

    public class AnimCtrl : MonoBehaviour
    {
#region public parameters
        public static AnimCtrl Instance;
#endregion

#region private parameters

        [SerializeField] private GlobalAnimConfig _animConfig;
        private Dictionary<uint, BlobAssetReference<BlobElementData>> _animBlobs;

        // ECS
        private Dictionary<uint, ElementAnimBakedData> _bakedDataDict;
        private static EntityManager _entityManager;
        private static EntityArchetype _animArchetype;

        // 每帧积攒的同步指令（NativeList，主线程写）
        private NativeList<AnimSyncCommand> _syncCommands;

        // 持久化同步命令缓冲区（替代 TempJob，修复瓶颈5）
        private NativeArray<AnimSyncCommand> _cmdBuffer;

        // --- 渲染快照 NativeArray（修复瓶颈1：消除 GetComponentData 随机访问）---
        // 由 AnimRenderExportSystem 每帧写入，AnimCtrl.LateUpdate 按"稳定 slot"读取
        // slot 同时是 _allProxies / _proxyLocalMatrices / _worldMatrixCache 的下标
        private NativeArray<AnimRenderSnapshot> _renderSnapshots;

        // _allProxies[i] 与 _proxyTransformAccess[i] 一一对应
        private List<AnimRenderProxy> _allProxies;
        private TransformAccessArray  _proxyTransformAccess;
        private NativeArray<float3>    _proxyLocalOffsets;   // SoldierAnim 相对父节点的 localPosition，注册时写入
        private NativeArray<Matrix4x4> _worldMatrixCache;    // WorldMatrixCacheJob 每帧写入
        private const int kMatrixCapacity = 16384;
        private JobHandle _matrixJobHandle;

        // --- 待发送给 GPU 的批次数组（每批最多 1023 个，DrawMeshInstanced 硬限制）---
        private Matrix4x4[] _matrixBatch;
        private float[]     _sliceBatch;
        private float[]     _alphaBatch;
        private MaterialPropertyBlock _mpb;
        private int _slicePropId;
        private int _alphaPropId;

        // --- 渲染分组 ---
        private class RenderGroup
        {
            public uint             p_eleAnimId;
            public Material         p_sharedMaterial;
            public Mesh             p_mesh;
            public List<AnimRenderProxy> p_proxies = new List<AnimRenderProxy>();
        }
        private Dictionary<int, RenderGroup> _renderGroups;
        private List<RenderGroup>            _activeGroups;

        // --- 事件延迟队列（修复瓶颈3：渲染收集与业务逻辑解耦）---
        private struct PendingAnimEvent
        {
            public AnimRenderProxy p_proxy;
            public int             p_stateId;
        }
        private List<PendingAnimEvent> _pendingEvents;

        private WE.LodLevel _curGlobLodLevel = WE.LodLevel.MIN;
        private bool        _beInited;

        // 视锥剔除：相机 AABB 的扩展边距（世界单位），防止边缘精灵因中心离屏而突然消失
        // 建议 = 最大精灵半宽，通常 2~4 个世界单位即可
        [SerializeField] private float _cullPadding = 3f;

#endregion

#region Unity callbacks

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _animBlobs    = new Dictionary<uint, BlobAssetReference<BlobElementData>>();
            _bakedDataDict = new Dictionary<uint, ElementAnimBakedData>();

            _entityManager  = World.DefaultGameObjectInjectionWorld.EntityManager;
            _animArchetype  = _entityManager.CreateArchetype(typeof(AnimationRuntimeState));

            _syncCommands = new NativeList<AnimSyncCommand>(2048, Allocator.Persistent);
            _cmdBuffer    = new NativeArray<AnimSyncCommand>(2048, Allocator.Persistent);

            // 世界矩阵缓存 / 渲染快照三者共用 kMatrixCapacity，索引同步推进
            _allProxies           = new List<AnimRenderProxy>(kMatrixCapacity);
            _proxyTransformAccess = new TransformAccessArray(kMatrixCapacity);
            _proxyLocalOffsets    = new NativeArray<float3>(kMatrixCapacity, Allocator.Persistent);
            _worldMatrixCache     = new NativeArray<Matrix4x4>(kMatrixCapacity, Allocator.Persistent);
            _renderSnapshots      = new NativeArray<AnimRenderSnapshot>(kMatrixCapacity, Allocator.Persistent);
            AnimRenderExportSystem.Initialize(_renderSnapshots);

            _renderGroups  = new Dictionary<int, RenderGroup>();
            _activeGroups  = new List<RenderGroup>();
            _pendingEvents = new List<PendingAnimEvent>(2048);

            _matrixBatch = new Matrix4x4[1023];
            _sliceBatch  = new float[1023];
            _alphaBatch  = new float[1023];
            _mpb         = new MaterialPropertyBlock();
            _slicePropId = Shader.PropertyToID("_FinalSliceIndex");
            _alphaPropId = Shader.PropertyToID("_Alpha");

            // 必须在 Awake 里完成初始化：
            // SoldierCtrl.Awake -> LoadSoldierPrefab -> AnimCtrl.BindAnimWithEntity 同样在 Awake 阶段触发，
            // 如果留到 WarFieldGameManager 协程里再 init，BindAnimWithEntity 会因 _beInited==false 静默 return false，
            // 之后 CreateAnimEntity 拿不到 blob -> Entity.Null -> 渲染循环全部跳过 -> 完全看不到士兵。
            if (_animConfig == null)
            {
                GameLogger.LogError("AnimCtrl: _animConfig is null");
                _beInited = false;
                return;
            }
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
            if (!_beInited)
                return;

            // =================================================================
            // 第一步：【提前收尾渲染依赖 Job】完成上帧写入快照和矩阵的两个 Job，解除 Safety 锁，
            //         随后立刻渲染，令 Draw Call 尽可能早地提交给 GPU。
            //         AnimSyncJob 只影响 _cmdBuffer 写入，推迟到第四步再等。
            // =================================================================

            AnimRenderExportSystem.CompleteExport();
            _matrixJobHandle.Complete();

            NativeArray<AnimRenderSnapshot> snaps     = _renderSnapshots;
            NativeArray<Matrix4x4>          worldMats = _worldMatrixCache;
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

            for (int g = 0; g < groupCount; g++)
            {
                RenderGroup group = _activeGroups[g];
                var  list         = group.p_proxies;
                int  count        = list.Count;
                if (count == 0) continue;

                Mesh     mesh  = group.p_mesh;
                Material mat   = group.p_sharedMaterial;
                int batchCount = 0;

                for (int i = 0; i < count; i++)
                {
                    AnimRenderProxy proxy = list[i];
                    if (!proxy.gs_isVisible) continue;
                    if (proxy.gs_ecsEntity == Entity.Null) continue;

                    int slot = proxy.gs_worldMatrixSlot;
                    if ((uint)slot >= (uint)snapLen) continue;

                    // 视锥剔除：从 NativeArray 直接读世界坐标，无 P/Invoke
                    // worldMats[slot].m03 = translation X，m13 = translation Y（列主序 TRS 矩阵）
                    float px = worldMats[slot].m03;
                    float py = worldMats[slot].m13;
                    if (math.abs(px - camCX) > camHalfW || math.abs(py - camCY) > camHalfH)
                        continue;

                    _matrixBatch[batchCount] = worldMats[slot];
                    _sliceBatch[batchCount]  = snaps[slot].p_sliceIndex;
                    _alphaBatch[batchCount]  = proxy.gs_alpha;
                    batchCount++;

                    if (batchCount == 1023)
                    {
                        _mpb.Clear();
                        _mpb.SetFloatArray(_slicePropId, _sliceBatch);
                        _mpb.SetFloatArray(_alphaPropId,  _alphaBatch);
                        Graphics.DrawMeshInstanced(mesh, 0, mat, _matrixBatch, batchCount, _mpb,
                            UnityEngine.Rendering.ShadowCastingMode.Off, false);
                        batchCount = 0;
                    }
                }

                if (batchCount > 0)
                {
                    _mpb.Clear();
                    _mpb.SetFloatArray(_slicePropId, _sliceBatch);
                    _mpb.SetFloatArray(_alphaPropId,  _alphaBatch);
                    Graphics.DrawMeshInstanced(mesh, 0, mat, _matrixBatch, batchCount, _mpb,
                        UnityEngine.Rendering.ShadowCastingMode.Off, false);
                }
            }

            // =================================================================
            // 第二步：【动画事件提取】快照已 Complete，直接读取，无额外等待
            // =================================================================

            // 遍历 _allProxies 扁平列表。事件由 AnimUpdateJob 按"时间跨越 eventFrame"产生，
            // 完全独立于是否渲染，屏幕外单位的攻击/结束事件同样必须正确派发。
            _pendingEvents.Clear();
            int allCount = _allProxies.Count;
            for (int i = 0; i < allCount; i++)
            {
                AnimRenderProxy proxy = _allProxies[i];
                if (proxy.gs_ecsEntity == Entity.Null) continue;

                int slot = proxy.gs_worldMatrixSlot;
                if ((uint)slot >= (uint)snapLen) continue;

                AnimRenderSnapshot snap = snaps[slot];
                if (snap.p_currentStateId != proxy.gs_curAnimStateId) continue;

                int lastEvt = proxy.gs_lastEventCount;
                if (snap.p_eventCount > lastEvt)
                {
                    int times = snap.p_eventCount - lastEvt;
                    proxy.gs_lastEventCount = snap.p_eventCount;
                    for (int t = 0; t < times; t++)
                        _pendingEvents.Add(new PendingAnimEvent { p_proxy = proxy, p_stateId = (int)proxy.gs_curAnimStateId });
                }

                int lastFin = proxy.gs_lastFinishCount;
                if (snap.p_finishCount > lastFin)
                {
                    proxy.gs_lastFinishCount = snap.p_finishCount;
                    _pendingEvents.Add(new PendingAnimEvent { p_proxy = proxy, p_stateId = -1 });
                }
            }

            // 统一派发本帧的动画事件通知
            int evtCount = _pendingEvents.Count;
            for (int e = 0; e < evtCount; e++)
            {
                PendingAnimEvent ev = _pendingEvents[e];
                if (ev.p_proxy != null && ev.p_proxy.gs_ecsEntity != Entity.Null)
                    ev.p_proxy.TriggerAnimEvent(ev.p_stateId);
            }

            // =================================================================
            // 第三步：【为下一帧蓄力】收集脏标记，等待 AnimSyncJob 完成后覆写 _cmdBuffer，
            //         最后调度矩阵 Job 扔给后台 Worker 线程，主线程不再 Complete，直接退出。
            // =================================================================

            for (int g = 0; g < groupCount; g++)
            {
                var list   = _activeGroups[g].p_proxies;
                int pCount = list.Count;
                for (int i = 0; i < pCount; i++)
                    list[i].SyncDirtyToEcs();
            }

            // 覆写 _cmdBuffer 前才需要等 AnimSyncJob，将等待推迟到最晚必要时刻
            AnimSyncSystem.WaitForLastJob();

            // 将命令拷贝到 Persistent 缓冲区发往 AnimSyncSystem 进行修改动画状态
            int cmdCount = _syncCommands.Length;
            if (cmdCount > 0)
            {
                EnsureCmdBufferCapacity(cmdCount);
                NativeArray<AnimSyncCommand>.Copy(_syncCommands.AsArray(), _cmdBuffer, cmdCount);
                AnimSyncSystem.SetCommandBuffer(_cmdBuffer, cmdCount);
                _syncCommands.Clear();
            }

            // 盲调本帧矩阵计算，主线程绝不在此处 Complete 阻断，直接结束 LateUpdate 退出，
            // 把计算完全推给并行的后台 Worker 线程
            if (_allProxies.Count > 0)
            {
                var matJob = new WorldMatrixCacheJob
                {
                    p_localOffsets  = _proxyLocalOffsets,
                    p_worldMatrices = _worldMatrixCache,
                };
                _matrixJobHandle = matJob.Schedule(_proxyTransformAccess);
            }
        }

        private void OnDestroy()
        {
            _matrixJobHandle.Complete();

            // 先复位 ECS 静态状态，避免 World 仍存活时下一帧再调度 Job 写入已 Dispose 的 NativeArray
            AnimSyncSystem.Shutdown();
            AnimRenderExportSystem.Shutdown();

            foreach (var pair in _animBlobs)
            {
                if (pair.Value.IsCreated)
                    pair.Value.Dispose();
            }
            _animBlobs.Clear();

            if (_syncCommands.IsCreated)  _syncCommands.Dispose();
            if (_cmdBuffer.IsCreated)     _cmdBuffer.Dispose();
            if (_renderSnapshots.IsCreated) _renderSnapshots.Dispose();
            if (_proxyLocalOffsets.IsCreated) _proxyLocalOffsets.Dispose();
            if (_worldMatrixCache.IsCreated)   _worldMatrixCache.Dispose();
            if (_proxyTransformAccess.isCreated) _proxyTransformAccess.Dispose();

            if (Instance == this) Instance = null;
        }

#endregion

#region public functions

        // 兼容外部调用：实际初始化在 Awake 完成，这里只返回当前状态。
        // 不再做 "lazy init" 以避免在 Awake 失败后被覆盖掉错误状态。
        public bool InitAnimCtrl()
        {
            return _beInited;
        }

        public void RegisterRenderProxy(AnimRenderProxy proxy)
        {
            if (proxy.p_mesh == null || proxy.p_sharedMaterial == null) return;

            // 修改 TransformAccessArray / 矩阵缓存前，必须先完成上一帧 Job，避免竞态
            _matrixJobHandle.Complete();

            // 按 Material 实例 ID 分组
            int key = proxy.p_sharedMaterial.GetInstanceID();
            if (!_renderGroups.TryGetValue(key, out var group))
            {
                group = new RenderGroup
                {
                    p_eleAnimId      = proxy.gs_eleAnimId,
                    p_mesh           = proxy.p_mesh,
                    p_sharedMaterial = proxy.p_sharedMaterial
                };
                _renderGroups.Add(key, group);
                _activeGroups.Add(group);
            }
            // O(1) 记录索引，供 Unregister 时 swap-back
            proxy.SetGroupListIndex(group.p_proxies.Count);
            group.p_proxies.Add(proxy);

            // 加入扁平列表并分配世界矩阵槽位
            int slot = _allProxies.Count;
            proxy.SetWorldMatrixSlot(slot);
            _allProxies.Add(proxy);

            // 扩容（若接近上限）
            if (slot >= _proxyLocalOffsets.Length)
                ResizeMatrixBuffers(slot * 2 + 128);

            // 只存 localPosition offset（运行时无旋转/缩放变化，平移加法即可得到 worldPos）
            _proxyLocalOffsets[slot] = (float3)(Vector3)proxy.p_localMatrix.GetColumn(3);
            _proxyTransformAccess.Add(proxy.GetParentTransform()); // 直接 Add

            // 把 slot 写回 entity，让 AnimRenderExportJob 能直接定位输出位置
            // 注意：SetComponentData 会触发 ECS 内部同步，但 Register 不在热循环里，可接受
            WriteRenderSlotToEntity(proxy.gs_ecsEntity, slot);
        }

        public void UnregisterRenderProxy(AnimRenderProxy proxy)
        {
            if (proxy.p_sharedMaterial == null) return;

            // 修改 TransformAccessArray / 矩阵缓存前，必须先完成上一帧 Job，避免竞态
            _matrixJobHandle.Complete();

            // 从分组中 O(1) swap-back 移除
            int key = proxy.p_sharedMaterial.GetInstanceID();
            if (_renderGroups.TryGetValue(key, out var group))
            {
                int gIdx = proxy.gs_groupListIndex;
                if (gIdx >= 0)
                {
                    int gLast = group.p_proxies.Count - 1;
                    if (gIdx < gLast)
                    {
                        var movedInGroup = group.p_proxies[gLast];
                        group.p_proxies[gIdx] = movedInGroup;
                        movedInGroup.SetGroupListIndex(gIdx);
                    }
                    group.p_proxies.RemoveAt(gLast);
                    proxy.SetGroupListIndex(-1);
                }
            }

            // 从扁平列表中 swap-back 移除，O(1) 且保持连续
            int slot = proxy.gs_worldMatrixSlot;
            if (slot < 0) return;

            int last = _allProxies.Count - 1;
            if (slot < last)
            {
                AnimRenderProxy moved = _allProxies[last];
                moved.SetWorldMatrixSlot(slot);
                _allProxies[slot]       = moved;
                _proxyLocalOffsets[slot] = _proxyLocalOffsets[last];
                // 被搬来的 entity 也要更新它的 renderSlot
                WriteRenderSlotToEntity(moved.gs_ecsEntity, slot);
            }
            _allProxies.RemoveAt(last);
            proxy.SetWorldMatrixSlot(-1);
            // 释放离开的 entity 槽位
            WriteRenderSlotToEntity(proxy.gs_ecsEntity, -1);
            _proxyTransformAccess.RemoveAtSwapBack(slot);
        }

        // 把 renderSlot 写回 entity；Entity.Null/已销毁 entity/World 已 dispose 都安全跳过
        private static void WriteRenderSlotToEntity(Entity entity, int slot)
        {
            if (entity == Entity.Null) return;
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;
            if (!_entityManager.Exists(entity)) return;

            var st = _entityManager.GetComponentData<AnimationRuntimeState>(entity);
            st.p_renderSlot = slot;
            _entityManager.SetComponentData(entity, st);
        }

        public void SyncAnimState(Entity targetEntity, uint stateId, int dirIndex, float animRate)
        {
            if (!_beInited || targetEntity == Entity.Null) return;

            _syncCommands.Add(new AnimSyncCommand
            {
                p_targetAnimEntity = targetEntity,
                p_stateId          = stateId,
                p_directionIndex   = dirIndex,
                p_animRate         = animRate
            });
        }

        public AnimationRuntimeState GetRuntimeState(Entity entity)
        {
            if (_beInited && _entityManager.Exists(entity))
                return _entityManager.GetComponentData<AnimationRuntimeState>(entity);
            return default;
        }

        public Entity CreateAnimEntity(uint eleAnimId, GameObject gObj)
        {
            if (!_beInited) return Entity.Null;

            if (!_animBlobs.TryGetValue(eleAnimId, out var blobRef))
            {
                GameLogger.LogError($"Fail to find animation {eleAnimId}");
                return Entity.Null;
            }

            Entity entity = _entityManager.CreateEntity(_animArchetype);

            uint seed = (uint)gObj.GetInstanceID() + 1000u;
            if (seed == 0u) seed = 1u;

            _entityManager.SetComponentData(entity, new AnimationRuntimeState
            {
                p_elementBlobRef       = blobRef,
                p_currentStateId       = 0,
                p_previousStateId      = 0,
                p_currentVariationIndex = 0,
                p_currentDirectionIndex = 0,
                p_elapsedTime          = 0f,
                p_currentFrameIndex    = 0,
                p_targetTextureSliceIndex = 0,
                p_random               = new Unity.Mathematics.Random(seed),
                p_eventTriggerCount    = 0,
                p_finishCount          = 0,
                p_animRate             = 1.0f,
                p_renderSlot           = -1, // RegisterRenderProxy 时分配
                p_cachedStateIndex     = -1  // AnimUpdateJob 首次访问时填充
            });

            return entity;
        }

        public void DestroyAnimEntity(Entity entity)
        {
            if (!_beInited) return;
            if (_entityManager.Exists(entity))
                _entityManager.DestroyEntity(entity);
        }

        public bool BindAnimWithEntity(uint eleAnimId, string entityName, Dictionary<string, uint> stateDic)
        {
            if (!_beInited) return false;

            var conf = _animConfig.GetElementData(entityName);
            if (conf == null)
            {
                GameLogger.LogError($"Fail to find the animation {entityName}");
                return false;
            }

            if (_animBlobs.ContainsKey(eleAnimId))
            {
                GameLogger.LogDebug($"Already added this entity {entityName} {eleAnimId}");
                return false;
            }

            using var builder = new BlobBuilder(Allocator.Temp);
            ref BlobElementData root = ref builder.ConstructRoot<BlobElementData>();
            root.p_eleAnimId = eleAnimId;

            int stateCount = conf.p_stateAnim.Count;
            BlobBuilderArray<BlobStateData> blobStates = builder.Allocate(ref root.p_states, stateCount);

            for (int i = 0; i < stateCount; i++)
            {
                StateAnimData srcState = conf.p_stateAnim[i];
                if (!stateDic.TryGetValue(srcState.p_stateName, out var stateId))
                {
                    GameLogger.LogError($"Fail to find the animation {srcState.p_stateName} for {entityName}");
                    return false; // 提前 return，未污染 _bakedDataDict / _animBlobs
                }

                blobStates[i].p_stateId = stateId;
                blobStates[i].p_isLoop  = srcState.p_isLoop;

                int varCount = srcState.p_variations.Count;
                BlobBuilderArray<BlobVariationData> blobVars = builder.Allocate(ref blobStates[i].p_variations, varCount);

                for (int v = 0; v < varCount; v++)
                {
                    VariationAnimData srcVar = srcState.p_variations[v];
                    blobVars[v].p_eventFrame      = srcVar.p_eventFrame;
                    blobVars[v].p_frameRate        = srcVar.p_frameRate;
                    blobVars[v].p_animFrameCount   = srcVar.p_animFrameCount;

                    int offsetCount = srcVar.p_animStartOffset.Count;
                    BlobBuilderArray<int> blobOffsets = builder.Allocate(ref blobVars[v].p_animStartOffset, offsetCount);
                    for (int o = 0; o < offsetCount; o++)
                        blobOffsets[o] = srcVar.p_animStartOffset[o];
                }
            }

            _animBlobs.Add(eleAnimId, builder.CreateBlobAssetReference<BlobElementData>(Allocator.Persistent));
            // 所有 state 都成功映射后才记录 baked 数据，避免一致性不匹配
            _bakedDataDict[eleAnimId] = conf;
            return true;
        }

#endregion

#region private functions

        // 扩容矩阵缓冲区（Persistent，Dispose 旧 + 分配新）
        // 必须在 _matrixJobHandle.Complete() 之后调用；保留旧 world cache + 同步 snapshots
        private void ResizeMatrixBuffers(int newCap)
        {
            newCap = math.max(newCap, kMatrixCapacity);

            var newOffsets = new NativeArray<float3>(newCap, Allocator.Persistent);
            var newWorld   = new NativeArray<Matrix4x4>(newCap, Allocator.Persistent);
            var newSnaps   = new NativeArray<AnimRenderSnapshot>(newCap, Allocator.Persistent);

            int copyLen = math.min(_allProxies.Count, _proxyLocalOffsets.Length);
            NativeArray<float3>.Copy(_proxyLocalOffsets, newOffsets, copyLen);
            NativeArray<Matrix4x4>.Copy(_worldMatrixCache, newWorld, copyLen);
            int snapCopy = math.min(copyLen, _renderSnapshots.Length);
            NativeArray<AnimRenderSnapshot>.Copy(_renderSnapshots, newSnaps, snapCopy);

            _proxyLocalOffsets.Dispose();
            _worldMatrixCache.Dispose();
            _renderSnapshots.Dispose();
            _proxyLocalOffsets = newOffsets;
            _worldMatrixCache  = newWorld;
            _renderSnapshots   = newSnaps;

            AnimRenderExportSystem.Initialize(_renderSnapshots);
        }

        private void EnsureCmdBufferCapacity(int count)
        {
            if (_cmdBuffer.IsCreated && _cmdBuffer.Length >= count) return;

            int newCap = _cmdBuffer.IsCreated ? math.max(count, _cmdBuffer.Length * 2) : 2048;
            newCap = math.max(newCap, count);

            if (_cmdBuffer.IsCreated) _cmdBuffer.Dispose();
            _cmdBuffer = new NativeArray<AnimSyncCommand>(newCap, Allocator.Persistent);
        }

        private void ApplyGlobalMaterialLodSwitch(WE.LodLevel newLod)
        {
            int groupCount = _activeGroups.Count;
            for (int i = 0; i < groupCount; i++)
            {
                RenderGroup group = _activeGroups[i];
                if (group.p_sharedMaterial == null) continue;

                if (!_bakedDataDict.TryGetValue(group.p_eleAnimId, out var data)) continue;

                Texture2DArray targetColor  = data.p_hdColorArray;
                Texture2DArray targetNormal = data.p_hdNormalArray;

                switch (newLod)
                {
                    case WE.LodLevel.HD:
                        if (data.p_hdColorArray  != null) targetColor  = data.p_hdColorArray;
                        if (data.p_hdNormalArray  != null) targetNormal = data.p_hdNormalArray;
                        break;
                    case WE.LodLevel.MD:
                        if (data.p_mdColorArray  != null) targetColor  = data.p_mdColorArray;
                        if (data.p_mdNormalArray  != null) targetNormal = data.p_mdNormalArray;
                        break;
                    case WE.LodLevel.LD:
                        if (data.p_ldColorArray  != null) targetColor  = data.p_ldColorArray;
                        if (data.p_ldNormalArray  != null) targetNormal = data.p_ldNormalArray;
                        break;
                }

                if (targetColor  != null) group.p_sharedMaterial.SetTexture("_MainTexArray",   targetColor);
                if (targetNormal != null) group.p_sharedMaterial.SetTexture("_NormalTexArray", targetNormal);
            }
        }

#endregion
    }
}
