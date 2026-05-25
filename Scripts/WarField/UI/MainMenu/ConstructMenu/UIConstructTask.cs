using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace WarField
{
    using UD = UIDefines;
    using WRD = WarResDefine;
    using WE = WarFieldElements;
    using WBD = WarBuildingDefines;

    public class UIConstructTask : MonoBehaviour, ITask, IoObserverIntf
    {
#region public parameters
        public static UIConstructTask Instance;
#endregion

#region private parameters
        private IoObserver _ioObserver;
        private BuildingConf _candidateConf;
        private GameObject _candidate;
        private WarBuildingCandidateCheck _candidateScript;
        private Camera _camera;
        private bool _beInited;

        private Action<WarBuilding, bool, Vector2> _onTaskFinish;//放置成功、放弃的回调

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
            _ioObserver = new IoObserver(UD.UIEventGroupType.CONSTRUCT);
            _camera = Camera.main;
            _beInited = false;
        }

        private void Start()
        {
            WarFieldGameManager.Instance.RegisterTask(this, WE.TaskType.NORMAL);
        }
#endregion

#region public functions

        public bool InitConstructTask()
        {
            _candidate = null;
            _candidateScript = null;
            _candidateConf = null;

            _beInited = true;
            return true;
        }

        public void RunNormalTask(float deltaTime)
        {
            if (_candidate != null)
            {
                Vector3 pos = _camera.ScreenToWorldPoint(Input.mousePosition);
                pos.z = 0;
                _candidate.transform.position = pos;
            }
        }

        public void OnIoEvtNotification(string keyAlias, UIDefines.UiIoEventType evtType)
        {
            switch (keyAlias, evtType)
            {
                case { keyAlias: "MouseLeft", evtType: UD.UiIoEventType.KEYDOWN }:
                    if (EventSystem.current.IsPointerOverGameObject() == true)
                        return;
                    if(_candidate == null)
                        return;
                    if(_candidateScript.gs_isBuildable == false)
                        return;
                    FinishConstruct(true);
                    break;
                case { keyAlias: "MouseRight", evtType: UD.UiIoEventType.KEYDOWN }:
                case { keyAlias: "Construct ESC", evtType: UD.UiIoEventType.KEYDOWN }:
                    FinishConstruct(false);
                    break;
                default:
                    GameLogger.LogWarning($"Receive unknown key {keyAlias} {evtType}");
                    break;
            }
        }

        public void StartConstructTask(BuildingConf candidateConf)
        {
            if(candidateConf == null)
                return;

            if (_candidate != null)
            {
                GameLogger.LogError($"Already has candidate can not set a new one {candidateConf.gs_name}");
                return;
            }
            UiIoTask.Instance.OccupyExclusiveOwnerShip(UD.UIEventGroupType.CONSTRUCT); //有了建筑菜单之后删掉
            GameObject pfb = WarBuildingCtrl.Instance.GetWarBuildingPfb(candidateConf.gs_race, candidateConf.gs_mode, candidateConf.gs_subType);
            if(pfb == null)
                return;
            _candidate = Instantiate(pfb, new Vector3(-10000, -10000, 0), Quaternion.identity, WarBuildingCtrl.Instance.transform);
            if (_candidate == null)
            {
                GameLogger.LogError($"Unable to create candidate");
                return;
            }
            _candidateConf = candidateConf;
            _candidate.name = "Candidate";
            _candidate.layer = LayerMask.NameToLayer("Default");
            DestroyImmediate(_candidate.GetComponent<Rigidbody2D>()); //删除rigidbody,不让这个建筑被场景中其他物理实体检查到
            _candidateScript =  _candidate.AddComponent<WarBuildingCandidateCheck>();//添加检查位置是否可以建造的脚本
            _candidateScript.InitCandidateCheck(_candidateConf);

            //Cursor.visible = false;
            _ioObserver.RegisterListener(this, KeyCode.Mouse0, "MouseLeft", UD.UiIoEventType.KEYDOWN, UD.IoEvtScheduleType.LIST);
            _ioObserver.RegisterListener(this, KeyCode.Mouse1, "MouseRight", UD.UiIoEventType.KEYDOWN, UD.IoEvtScheduleType.LIST);
            _ioObserver.RegisterListener(this, KeyCode.Escape, "Construct ESC", UD.UiIoEventType.KEYDOWN, UD.IoEvtScheduleType.LIST);
            WarFieldGameManager.Instance.ActiveTask(this, WE.TaskType.NORMAL);

            //显示所有防御塔的覆盖范围
            if(_candidateConf.gs_mode == WBD.BuildingMode.DEFENCE)
                WarBuildingCtrl.Instance.SetDefenceCoverRange(true, WarMapCtrl.Instance.gs_curMapId, _candidateConf.gs_race, _candidateConf.gs_mode);
            else //其他类型（道具）就只显示同类型的建筑范围
                WarBuildingCtrl.Instance.SetDefenceCoverRange(true, WarMapCtrl.Instance.gs_curMapId, _candidateConf.gs_race, _candidateConf
                .gs_mode, _candidateConf.gs_subType);
        }

        public void RegisterConstructCallback(Action<WarBuilding, bool, Vector2> callback)
        {
            _onTaskFinish = callback;
        }

        public void RunFixTask(float deltaTime)
        {
            throw new NotImplementedException();
        }

#endregion

#region private functions

        //isPlace: true -> the building place to field successfully
        //         false -> no building is placed
        public void FinishConstruct(bool isPlace)
        {
            //隐藏所有防御塔的覆盖范围
            if(_candidateConf.gs_mode == WBD.BuildingMode.DEFENCE)
                WarBuildingCtrl.Instance.SetDefenceCoverRange(false, WarMapCtrl.Instance.gs_curMapId,_candidateConf.gs_race, _candidateConf.gs_mode);
            else //其他类型（道具）就只隐藏同类型的建筑范围
                WarBuildingCtrl.Instance.SetDefenceCoverRange(false, WarMapCtrl.Instance.gs_curMapId, _candidateConf.gs_race, _candidateConf
                .gs_mode, _candidateConf.gs_subType);

            WarBuilding bd = null;
            if (isPlace)
                bd = WarBuildingCtrl.Instance.AddBuildingDuringRunning(_candidateConf, _candidate.transform.position, (object)null, CameraCtrl.Instance
                    .gs_curMapId);
            else
                WarResCtrl.Instance.AddRes(WRD.ResTypes.GOLDCOIN, _candidateConf.gs_price);//取消建设，退回金钱

            if(_onTaskFinish != null)
                _onTaskFinish(bd, isPlace, _candidate.transform.position); //回调
            Destroy(_candidate);
            _candidate = null;
            _candidateScript = null;
            _candidateConf = null;
            _onTaskFinish = null;
            _ioObserver.UnregisterListener(this, KeyCode.Mouse0, "MouseLeft", UD.UiIoEventType.KEYDOWN, UD.IoEvtScheduleType.LIST);
            _ioObserver.UnregisterListener(this, KeyCode.Mouse1, "MouseRight", UD.UiIoEventType.KEYDOWN, UD.IoEvtScheduleType.LIST);
            _ioObserver.UnregisterListener(this, KeyCode.Escape, "Construct ESC", UD.UiIoEventType.KEYDOWN, UD.IoEvtScheduleType.LIST);
            UiIoTask.Instance.ReleaseExclusiveOwnerShip(UD.UIEventGroupType.CONSTRUCT); //在建筑菜单激活时占用的IO
            WarFieldGameManager.Instance.SuspendTask(this, WE.TaskType.NORMAL);
        }
#endregion
    }
}
