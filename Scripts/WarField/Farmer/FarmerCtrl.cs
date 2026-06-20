using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using WarField.Anim;

namespace WarField
{
    using FD = FarmerDefines;
    using WE = WarFieldElements;

    public class FarmerCtrl : MonoBehaviour
    {
#region public parameters

        public static FarmerCtrl Instance;

#endregion

#region private parameters

        private GameObject _fmPrefab;
        private List<FarmerConf> _confs;
        private AsyncDataPoolForTransform<Farmer> _fmOnField;
        private bool _beInited;

        // farmer move job
        private NativeArray<FarmerMoveCmd> _moveCmds;
        private int _moveJobCapacity;
        private JobHandle _moveJobHandle = default;
        private bool _moveJobScheduled = false;
#endregion

#region private parameters' get set

        public JobHandle gs_moveJobHandle
        {
            get { return _moveJobHandle; }
        }

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
            _confs = new List<FarmerConf>();
            _beInited = false;
        }

        private void OnDestroy()
        {
            _moveJobHandle.Complete();
            if (_moveCmds.IsCreated)
                _moveCmds.Dispose();
            _fmOnField?.Dispose();
        }

#endregion

#region public functions

        public bool InitFarmerCtrl()
        {
            if(_beInited == true)
                return false;
            if(ReadConfs("Conf/Farmer/") == false)
                return false;
            _fmPrefab = Resources.Load<GameObject>("Prefabs/Farmer/Farmer");
            if (_fmPrefab == null)
            {
                GameLogger.LogError("Farmer prefab path is invalid");
                return false;
            }

            if (AnimCtrl.Instance == null)
            {
                GameLogger.LogError("AnimCtrl.Instancenot ready");
                return false;
            }
            //register the state anim
            Type scriptType = typeof(Farmer);
            Component scriptComponent = _fmPrefab.GetComponent(scriptType);
            IAnimInfo animInfo = scriptComponent as IAnimInfo;
            uint eleAnimId = animInfo.IAnimInfo_GetEleAnimId();
            Dictionary<string, uint> stateDic = animInfo.IAnimInfo_GetStateId();
            ref BlobElementData data = ref System.Runtime.CompilerServices.Unsafe.NullRef<BlobElementData>();
            if (AnimCtrl.Instance.BindAnimWithEntity(eleAnimId, _fmPrefab.name, stateDic, out var blobAssetRef) == false)
            {
                GameLogger.LogWarning($"BindAnimWithEntity failed：{_fmPrefab.name} (eleAnimId={eleAnimId})");
                return false;
            }

            _fmOnField = new AsyncDataPoolForTransform<Farmer>();
            _fmOnField.EnableTransformSync(PopulationSystem.Instance.gs_maxPopulation, fmAdd => fmAdd.gs_transform);  //从人口系统获取的最大人口值
            _beInited = true;
            return true;
        }

        public void WaitForMoveJobFinish()
        {
            _moveJobHandle.Complete();
        }

        // 调度所有 farmer 的移动 job，dependency 为前置依赖（流场 job handle 等）
        public void AllFarmerMoveJob(JobHandle dependency)
        {
            if (_beInited == false)
                return;

            _fmOnField.ForEachAndFlush(null); // 先 flush 把新加入/移除的 farmer 同步进来

            int cnt = _fmOnField.Count;
            if (cnt == 0)
                return;

            EnsureMoveCapacity(cnt);

            var cmdSlice = _moveCmds.GetSubArray(0, cnt);
            _fmOnField.ForList(static (list, cmds) =>
            {
                for (int k = 0; k < list.Count; k++)
                {
                    var fm = list[k];
                    FD.FarmerStatus status = fm.gs_curStatus;
                    cmds[k] = new FarmerMoveCmd
                    {
                        p_currentPos    = new float2(fm.gs_transform.position.x, fm.gs_transform.position.y),
                        p_gridIndex     = fm.gs_gridIndex,
                        p_moveSpeed     = fm.gs_moveSpeed,
                        p_mass          = fm.gs_mass,
                        p_radius        = fm.gs_bodyRadius,
                        p_status        = (byte)status,
                        p_targetPos     = new float2(fm.gs_targetPos.x, fm.gs_targetPos.y),
                        p_desiredDir    = new float2(fm.gs_desiredMoveDir.x, fm.gs_desiredMoveDir.y),
                        p_flowIndex     = status == FD.FarmerStatus.GOBACK ? WE.HomeFlowFieldId : 0
                    };
                }
            }, cmdSlice);

            byte mapId = WE.OnGroundMapIndex;
            var flowMap     = WarMapCtrl.Instance.GetPathFinderMapByIndex(mapId);
            var dynamicQuery = SpatialGridManager.Instance.GetQueryHelper(mapId, (int)WE.WarEleType.FARMER);
            var staticQuery  = SpatialGridManager.Instance.GetQueryHelper(mapId, (int)WE.WarEleType.BUILDING);

            FarmerMoveJob job = new FarmerMoveJob
            {
                p_moveCmds      = cmdSlice,
                p_flowFieldPool = flowMap.gs_flowFieldPool,
                p_flowCellSize  = flowMap.gs_cellSize,
                p_flowMapOrigin = new float2(flowMap.gs_bounds.min.x, flowMap.gs_bounds.min.y),
                p_flowCols      = Mathf.CeilToInt(flowMap.gs_bounds.size.x / flowMap.gs_cellSize),
                p_flowRows      = Mathf.CeilToInt(flowMap.gs_bounds.size.y / flowMap.gs_cellSize),
                p_flowMapMax    = new float2(flowMap.gs_bounds.max.x, flowMap.gs_bounds.max.y),
                p_dynamicQuery  = dynamicQuery,
                p_staticQuery   = staticQuery,
                p_deltaTime     = Time.fixedDeltaTime
            };

            TransformAccessArray transformArray = _fmOnField.gs_transformArray;
            JobHandle mapDeps = JobHandle.CombineDependencies(dependency, flowMap.gs_flowFieldJobHandle);
            _moveJobHandle = job.Schedule(transformArray, mapDeps);
            _moveJobScheduled = true;
        }

        // 确保 farmer 位置计算完成并同步回 SpatialGrid，与 SoldiersLaterUpdate 对应
        public void FarmersLaterUpdate()
        {
            if (_moveJobScheduled == false)
                return;

            _moveJobHandle.Complete();
            _moveJobScheduled = false;

            _fmOnField.ForEachReadOnly(static fm => fm.SyncSpatialEntity());
        }

        public Farmer AddFarmerAt(Vector2 pos, byte mapId, FD.FarmerLevel fmLevel, WE.GenderType gender)
        {
            if (_beInited == false)
                return null;

            Farmer fm = Instantiate(_fmPrefab, pos, Quaternion.identity, transform).GetComponent<Farmer>();
            if (fm == null)
            {
                GameLogger.LogError($"Add farmer error");
                return null;
            }
            _fmOnField.AddItemAsync(fm);
            fm.InitFarmer(GetFarmerConf(fmLevel, gender), mapId);
            return fm;
        }
#endregion

#region private functions

        // 对移动命令数组扩容（翻倍策略，与 SoldierCtrl 一致）
        private void EnsureMoveCapacity(int neededCount)
        {
            if (neededCount <= _moveJobCapacity)
                return;

            int newCapacity = math.max(128, (int)math.ceilpow2(neededCount));

            if (_moveCmds.IsCreated)
                _moveCmds.Dispose();

            _moveCmds = new NativeArray<FarmerMoveCmd>(newCapacity, Allocator.Persistent);
            _moveJobCapacity = newCapacity;
        }

        private FarmerConf GetFarmerConf(FD.FarmerLevel fmLevel, WE.GenderType gender)
        {
            int cnt = _confs.Count;
            for (int i = 0; i < cnt; i++)
            {
                if(_confs[i].p_level == fmLevel &&  _confs[i].p_gender == gender)
                    return _confs[i];
            }
            return null;
        }


        private bool ReadConfs(string path)
        {
            TextAsset[] xmlFiles = Resources.LoadAll<TextAsset>(path);
            for (int i = 0; i < xmlFiles.Length; i++)
            {
                XmlDocument confXML = new XmlDocument();
                confXML.LoadXml(xmlFiles[i].text);
                if (confXML == null)
                    continue;

                FarmerConf fConf = new FarmerConf();
                XmlNodeList nodeList = confXML.SelectSingleNode("fmConf").ChildNodes;
                for (int j = 0; j < nodeList.Count; j++)
                {
                    if (nodeList[j].NodeType == XmlNodeType.Comment)
                        continue;

                    XmlElement tmp = (XmlElement)nodeList[j];
                    switch (tmp.Name)
                    {
                        case "level":
                            fConf.p_level = (FD.FarmerLevel)Enum.Parse(typeof(FD.FarmerLevel), tmp.GetAttribute("value"), ignoreCase: true);
                            break;
                        case "gender":
                            fConf.p_gender = (WE.GenderType)Enum.Parse(typeof(WE.GenderType), tmp.GetAttribute("value"), ignoreCase: true);
                            break;
                        case "moveSpeed":
                            fConf.p_moveSpeed = float.Parse(tmp.GetAttribute("value"));
                            break;
                        case "carryCapacity":
                            fConf.p_carryCapacity = int.Parse(tmp.GetAttribute("value"));
                            break;
                        case "enemyDetectRange":
                            fConf.p_enemyDetectRange = float.Parse(tmp.GetAttribute("value"));
                            break;
                        case "hideMinTime":
                            fConf.p_hideMinTime = float.Parse(tmp.GetAttribute("value"));
                            break;
                        case "hideDetectTime":
                            fConf.p_hideDetectTime = float.Parse(tmp.GetAttribute("value"));
                            break;
                        case "mass":
                            fConf.p_mass = float.Parse(tmp.GetAttribute("value"));
                            break;
                        default:
                            break;
                    }
                }
                _confs.Add(fConf);
            }
            return true;
        }
#endregion
    }
}
