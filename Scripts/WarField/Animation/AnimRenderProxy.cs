using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace WarField.Anim
{
    public class AnimRenderProxy : MonoBehaviour
    {
#region public parameters
        [HideInInspector] public Mesh p_mesh;
        [HideInInspector] public Material p_sharedMaterial;
        [HideInInspector] public Matrix4x4 p_localMatrix;


#endregion

#region private parameters

        private Entity _ecsEntity = Entity.Null;
        private BlobAssetReference<BlobAnimData> _blobAnimData;
        private Transform _parentTransform;
        private IAnimInfo _animInfoHost;
        private uint _eleAnimId;
        private bool[] _hasStateAnim; //state是不是有动画
        //因为ECS和逻辑的不同步,所以这里需要在逻辑层做过滤
        private uint _curAnimStateId = 0;
        private int _prvSyncedDirIndex = 0;
        private int _lastEventCount = 0;
        private int _lastFinishCount = 0;

        private float _alpha = 1.0f;
        private float _animRate;

        private bool _isVisible;
        private bool _isEcsDirty = false;
        // 当需要强制重播同一动画状态时置 true，令 AnimSyncJob 重置 p_previousStateId 触发 AnimUpdateJob 重新播放
        private bool _forceReplay = false;

        // 在 AnimCtrl._allProxies 扁平列表中的槽位索引，供 WorldMatrixCacheJob 和 _worldMatrixCache 使用
        private int _worldMatrixSlot = -1;
        // 在 RenderGroup.p_proxies 列表内的索引，供 Unregister 时 O(1) swap-back 移除
        private int _groupListIndex = -1;
#endregion

#region private parameters' get set

        public uint gs_curAnimStateId
        {
            get { return _curAnimStateId; }
        }

        public bool[] gs_hasStateAnim
        {
            get { return _hasStateAnim; }
        }

        public float gs_alpha
        {
            get { return _alpha; }
        }

        public bool gs_isVisible
        {
            get { return _isVisible; }
        }

        public Entity gs_ecsEntity
        {
            get { return _ecsEntity; }
        }

        public uint gs_eleAnimId
        {
            get { return _eleAnimId; }
        }

        public int gs_lastEventCount
        {
            get { return _lastEventCount; }
            set  { _lastEventCount = value; }
        }

        public int gs_lastFinishCount
        {
            get { return _lastFinishCount; }
            set { _lastFinishCount = value; }
        }

        public int gs_worldMatrixSlot
        {
            get { return _worldMatrixSlot; }
        }

        public int gs_groupListIndex
        {
            get { return _groupListIndex; }
        }
#endregion

#region Unity callbacks

#endregion

#region public functions
        //maxState: the max state id
        public void InitProxy(IAnimInfo host, GameObject parent, int maxState)
        {
            if (host == null || parent == null)
            {
                GameLogger.LogError("Error parameter");
                return;
            }

            _animInfoHost = host;
            _eleAnimId = host.IAnimInfo_GetEleAnimId();
            _ecsEntity = AnimCtrl.Instance.CreateAnimEntity(_eleAnimId, parent);
            _blobAnimData = AnimCtrl.Instance.GetRuntimeState(_ecsEntity).p_elementBlobRef;
            _parentTransform = parent.transform;

            // 获取prefab Editor 中调整的参数
            p_localMatrix = Matrix4x4.TRS(transform.localPosition, transform.localRotation, transform.localScale);

            MeshFilter mf = GetComponent<MeshFilter>();
            if (mf != null)
                p_mesh = mf.sharedMesh;

            MeshRenderer mr = GetComponent<MeshRenderer>();
            if (mr != null)
            {
                p_sharedMaterial = mr.sharedMaterial;
                // 运行时彻底关闭 Renderer，免除引擎底层的 Culling 和 DrawCall 消耗！
                mr.enabled = false;
            }

            _curAnimStateId = 0;
            _prvSyncedDirIndex = 0;
            _lastEventCount = 0;
            _lastFinishCount = 0;
            _animRate = 1.0f;
            _isEcsDirty = false;
            _forceReplay = false;

            _hasStateAnim = new bool[maxState];
            for (int i = 0; i < maxState; i++)
            {
                _hasStateAnim[i] = HasStateAnim(i);
            }

            _isVisible = true;
            _alpha = 1.0f;

            AnimCtrl.Instance.RegisterRenderProxy(this);
        }

        public void DeinitProxy()
        {
            if (AnimCtrl.Instance != null)
            {
                AnimCtrl.Instance.UnregisterRenderProxy(this);
                if (_ecsEntity != Entity.Null)
                    AnimCtrl.Instance.DestroyAnimEntity(_ecsEntity);
            }
            _ecsEntity = Entity.Null;
            _animInfoHost = null;
            _worldMatrixSlot = -1;
            _groupListIndex  = -1;
        }

        // 供 AnimCtrl 设置/更新扁平列表槽位
        public void SetWorldMatrixSlot(int slot) => _worldMatrixSlot = slot;
        // 供 AnimCtrl 维护 RenderGroup.p_proxies swap-back 时回写索引
        public void SetGroupListIndex(int idx) => _groupListIndex = idx;

        // 供 AnimCtrl 构建 TransformAccessArray 时使用
        public Transform GetParentTransform() => _parentTransform;

        public void ChangeAnimState(int stateId)
        {
            if(stateId == -1)
                return;

            if (_curAnimStateId != (uint)stateId )
            {
                _curAnimStateId = (uint)stateId;
                _lastEventCount = 0;
                _lastFinishCount = 0;
                _isEcsDirty = true; // 仅标记脏数据
            }
        }

        // 强制重播相同的动画状态（用于攻击动画等需要再次播放同一 state 的场景）
        // 即使 stateId 未变也会重置 ECS 侧的 p_previousStateId，令 AnimUpdateJob 重新从第0帧开始播放
        // 不重置 _lastEventCount / _lastFinishCount：AnimCtrl 通过检测 snap.count < proxy.lastCount 来自动同步
        public void ForceReplayAnimState(int stateId)
        {
            if (stateId == -1)
                return;

            _curAnimStateId = (uint)stateId;
            _forceReplay = true;
            _isEcsDirty = true;
        }

        // 查询当前动画状态是否被配置为循环播放
        public bool IsCurrentAnimLooping()
        {
            if (!_blobAnimData.IsCreated)
                return false;
            ref var elementData = ref _blobAnimData.Value;
            for (int i = 0; i < elementData.p_states.Length; i++)
            {
                if (elementData.p_states[i].p_stateId == _curAnimStateId)
                    return elementData.p_states[i].p_isLoop;
            }
            return false;
        }

        // -1 means not change
        public void ChangeDirection(int dirIndex)
        {
            if(dirIndex == -1)
                return;
            if (_prvSyncedDirIndex != dirIndex)
            {
                _prvSyncedDirIndex = dirIndex;
                _isEcsDirty = true; // 仅标记脏数据
            }
        }

        //隐藏/显示动画
        public void ChangeAnimVisible(bool value)
        {
                _isVisible = value;
        }

        public void ChangeAlpha(float value)
        {
            _alpha = value;
        }

        public void ChangeAnimRate(float value)
        {
            if(math.abs(_animRate - value) > 0.001f)
            {
                _animRate = value;
                _isEcsDirty = true; // 仅标记脏数据
            }
        }

        public void SyncDirtyToEcs()
        {
            if (_isEcsDirty && _ecsEntity != Entity.Null)
            {
                AnimCtrl.Instance.SyncAnimState(_ecsEntity, _curAnimStateId, _prvSyncedDirIndex, _animRate, _forceReplay);
                _isEcsDirty = false;
                _forceReplay = false;
            }
        }

        // 运行时计算最终渲染用的世界矩阵
        public Matrix4x4 GetWorldMatrix()
        {
            if (_parentTransform != null)
            {
                // 直接拿底层算好的局域坐标矩阵，避免万次调用 TRS 产生巨大开销
                return _parentTransform.localToWorldMatrix * p_localMatrix;
            }
            return p_localMatrix;
        }

		// 被 AnimCtrl 内联触发
        public void TriggerAnimEvent(int stateId)
        {
            _animInfoHost?.IAnimInfo_OnAnimEvent(stateId);
        }
#endregion

#region private functions
        private bool HasStateAnim(int stateId)
        {
            if (_blobAnimData.IsCreated == false)
				return false;

            int cnt = _blobAnimData.Value.p_states.Length;
            for (int i = 0; i < cnt; i++)
            {
                if(_blobAnimData.Value.p_states[i].p_stateId == stateId)
                    return true;
            }
            return false;
        }
#endregion
    }
}
