using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace WarField.Anim
{
    using WE = WarFieldElements;

    public class AnimCtrl : MonoBehaviour
    {
#region public parameters
        public static AnimCtrl Instance;
#endregion

#region private parameters

        [SerializeField] private GlobalAnimConfig _animConfig; //所有动画的记录
        private Dictionary<uint, BlobAssetReference<BlobElementData>> _animBlobs;

        private bool _beInited;

#endregion

#region private parameters' get set

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
            _animBlobs = new Dictionary<uint, BlobAssetReference<BlobElementData>>();
            _beInited = false;
        }

#endregion

#region public functions

        public bool InitAnimCtrl()
        {
            if (_beInited == false)
                return false;

            _animConfig = AssetDatabase.LoadAssetAtPath<GlobalAnimConfig>("Assets/Animation/GlobalAnimConfig.asset");
            if (_animConfig == null)
            {
                GameLogger.LogError("Not set the animation config file !");
                return false;
            }

            return true;
        }

        public bool BindAnimWithEntity(uint eleAnimId, string entityName, Dictionary<string, uint> stateDic)
        {
            if(_beInited == false)
                return false;

            var conf = _animConfig.GetElementData(entityName);
            if (conf == null)
            {
                GameLogger.LogError($"Fail to find the animation {entityName}");
                return false;
            }

            if (_animBlobs.ContainsKey(eleAnimId) == false)
            {
                // 使用 BlobBuilder 分配临时的内存
                using var builder = new BlobBuilder(Allocator.Temp);
                // 构建根节点
                ref BlobElementData root = ref builder.ConstructRoot<BlobElementData>();
                root.p_eleAnimId = eleAnimId;

                int stateCount = conf.p_stateAnim.Count;
                BlobBuilderArray<BlobStateData> blobStates = builder.Allocate(ref root.p_states, stateCount);
                for (int i = 0; i < stateCount; i++)
                {
                    StateAnimData srcState = conf.p_stateAnim[i];
                    if (stateDic.TryGetValue(conf.p_stateAnim[i].p_stateName, out var stateId) == true)
                    {
                        blobStates[i].p_stateId = stateId;
                        blobStates[i].p_isLoop = srcState.p_isLoop;

                        // 分配并填充 Variations 数组
                        int varCount = srcState.p_variations.Count;
                        BlobBuilderArray<BlobVariationData> blobVars = builder.Allocate(ref blobStates[i].p_variations, varCount);
                        for (int v = 0; v < varCount; v++)
                        {
                            VariationAnimData srcVar = srcState.p_variations[v];
                            blobVars[v].p_eventFrame = srcVar.p_eventFrame;
                            blobVars[v].p_frameRate = srcVar.p_frameRate;
                            blobVars[v].p_animFrameCount = srcVar.p_animFrameCount;

                            // 分配并填充每个朝向的起始偏移量 StartOffset 数组
                            int offsetCount = srcVar.p_animStartOffset.Count;
                            BlobBuilderArray<int> blobOffsets = builder.Allocate(ref blobVars[v].p_animStartOffset, offsetCount);

                            for (int o = 0; o < offsetCount; o++)
                            {
                                blobOffsets[o] = srcVar.p_animStartOffset[o];
                            }
                        }
                    }
                    else
                    {
                        GameLogger.LogError($"Fail to find the animation {srcState.p_stateName} for {entityName}");
                        return false;
                    }
                }
                _animBlobs.Add(eleAnimId, builder.CreateBlobAssetReference<BlobElementData>(Allocator.Persistent));
            }
            else
            {
                GameLogger.LogDebug($"Already added this entity {entityName} {eleAnimId}");
                return false;
            }
            return true;
        }
#endregion

#region private functions

        private BlobAssetReference<BlobElementData> ConvertToBlob(ElementAnimBakedData bakedData, uint animId, uint stateId, string entityName, string stateName)
        {
            // 使用 BlobBuilder 分配临时的内存脚手架
            using var builder = new BlobBuilder(Allocator.Temp);

            // 构建根节点
            ref BlobElementData root = ref builder.ConstructRoot<BlobElementData>();
            root.p_eleAnimId = animId;

            // 分配并填充 States 数组
            int stateCount = bakedData.p_stateAnim.Count;
            BlobBuilderArray<BlobStateData> blobStates = builder.Allocate(ref root.p_states, stateCount);

            for (int s = 0; s < stateCount; s++)
            {
                StateAnimData srcState = bakedData.p_stateAnim[s];

                // 根据你在 BindAnimWithEntity 注册时定义的逻辑分配状态识别码
                // 示例：如果是字符串直接转 uint，如果你有明确的枚举/行为码，请在这里按你的映射转换

                blobStates[s].p_stateId = stateId;
                blobStates[s].p_isLoop = srcState.p_isLoop;

                // 2. 分配并填充 Variations 数组
                int varCount = srcState.p_variations.Count;
                BlobBuilderArray<BlobVariationData> blobVars = builder.Allocate(ref blobStates[s].p_variations, varCount);

                for (int v = 0; v < varCount; v++)
                {
                    VariationAnimData srcVar = srcState.p_variations[v];
                    blobVars[v].p_eventFrame = srcVar.p_eventFrame;
                    blobVars[v].p_frameRate = srcVar.p_frameRate;
                    blobVars[v].p_animFrameCount = srcVar.p_animFrameCount;

                    // 3. 分配并填充每个朝向的起始偏移量 StartOffset 数组
                    int offsetCount = srcVar.p_animStartOffset.Count;
                    BlobBuilderArray<int> blobOffsets = builder.Allocate(ref blobVars[v].p_animStartOffset, offsetCount);

                    for (int o = 0; o < offsetCount; o++)
                    {
                        blobOffsets[o] = srcVar.p_animStartOffset[o];
                    }
                }
            }

            // 正式冻结并持久化非托管资产
            BlobAssetReference<BlobElementData> result = builder.CreateBlobAssetReference<BlobElementData>(Allocator.Persistent);
            return result;
        }
#endregion
    }
}
