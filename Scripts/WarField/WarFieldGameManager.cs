using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;

using WarUpgrade;
using WarArchive;

namespace WarField
{
	using WE = WarFieldElements;

	public class WarFieldGameManager : MonoBehaviour
	{
#region public parameters
		static public WarFieldGameManager Instance;
#endregion

#region private parameters
        //task data
        private class TaskData
        {
            public ITask p_task;
            public bool[] p_isActive;  //[WE.TaskType]
            public float[] p_interval; //对于normal task是时间（s），对于fix task是fix uptime的count   [WE.TaskType]
            public float[] p_currentTimer;//[WE.TaskType]
        }

		private enum TaskCommandType : byte
		{
			MIN,
			REGISTER,
			UNREGISTER,
			ACTIVE,
			SUSPEND,
			MAX,
		}

		private struct TaskCommand
		{
			public TaskCommandType p_commType;
			public ITask p_task;
			public float p_interval;
		}

		//task manager
		private AsyncDataPool<TaskData>[] _taskPool; //all the fix and normal tasks [WE.TaskType]
		private Dictionary<ITask, TaskData> _taskMapping;
		//无锁并发队列，异步记录task的改变的指令
		private AsyncCommandBuffer<TaskCommand>[] _taskActionQueue; //[WE.TaskType]
		// 用于避免 GC 的内部对象池
		private Stack<TaskData> _taskDataPool;

        private JobHandle _mapJobs; //流场地图重构和spatial map重构的合并任务, 是移动和查找的前置
		private bool _beInited = false;
		private bool _canWork = false;

#endregion

#region private parameters' get set

        public JobHandle gs_mapJobs
        {
            get  { return _mapJobs; }
        }
#endregion

#region Unity callbacks
		private void Awake()
		{
			if(Instance == null)
				Instance = this;
			else
				Destroy(gameObject);
			Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); //避免出现鼠标图片被改变的情况

			_taskPool = new AsyncDataPool<TaskData>[(int)WE.TaskType.MAX];
			_taskPool[(int)WE.TaskType.NORMAL] = new AsyncDataPool<TaskData>(100);
			_taskPool[(int)WE.TaskType.FIXED] = new AsyncDataPool<TaskData>(100);
            _taskPool[(int)WE.TaskType.NORMAL].RegisterOnItemRemoved(OnTaskRemoved);
            _taskPool[(int)WE.TaskType.FIXED].RegisterOnItemRemoved(OnTaskRemoved);
			_taskMapping = new Dictionary<ITask, TaskData>();
			_taskActionQueue = new AsyncCommandBuffer<TaskCommand>[(int)WE.TaskType.MAX];
            _taskActionQueue[(int)WE.TaskType.NORMAL] = new AsyncCommandBuffer<TaskCommand>();
            _taskActionQueue[(int)WE.TaskType.FIXED] = new AsyncCommandBuffer<TaskCommand>();
			_taskDataPool = new Stack<TaskData>(100);

            _mapJobs = default;
        }

		private void Start()
		{
			/*---------- for debug -----------*/
			//Archive.Instance.CreateArchive("abc")
			UpgradeDatabase.Instance.InitUpgradeDatabase("Conf/UpgradeConf");
            if (Archive.Instance.EnterArchive("abc") == false)
            {
                GameLogger.LogWarning("Not has the abc archive , create one");
                Archive.Instance.CreateArchive("abc",5);
            }
			UpgradeDatabase.Instance.LoadUpgradeByArchive();
			/*---------- for debug -----------*/

			StartCoroutine(InitAllWarField());
		}

		private void OnDestroy()
		{
			//释放锁
			_taskPool[(int)WE.TaskType.NORMAL]?.Dispose();
			_taskPool[(int)WE.TaskType.FIXED]?.Dispose();
		}

		private void FixedUpdate()
        {
            SoldierCtrl.Instance.WaitForMoveJobFinish();
            int index = (int)WE.TaskType.FIXED;
            MergeQueue(index);
            float deltaTime = Time.fixedDeltaTime;
            var stateValue = (id: index, dt: Time.fixedDeltaTime);
			_taskPool[index].ForEachAndFlush(static (taskData, state) =>
			{
				if (!taskData.p_isActive[state.id])
					return;
                if (taskData.p_interval[state.id] > 0)
				{
					taskData.p_currentTimer[state.id] -= 1; // fixed task p_currentTimer is a count
					if (taskData.p_currentTimer[state.id] <= 0)
						taskData.p_currentTimer[state.id] += taskData.p_interval[state.id];
					else
						return; // not run task
				}
                if(taskData.p_interval[state.id] > 0)
				    taskData.p_task.RunFixTask(state.dt * taskData.p_interval[state.id]);
                else
                    taskData.p_task.RunFixTask(state.dt);
			}, stateValue);
            SoldierCtrl.Instance.AllSoldierMoveJob(_mapJobs);//确保地图重构完成
		}

		private void Update()
		{
            int index = (int)WE.TaskType.NORMAL;
            MergeQueue(index);

            var stateValue = (id: index, dt: Time.deltaTime);
            _taskPool[index].ForEachAndFlush(static (taskData, state) =>
            {
                if (!taskData.p_isActive[state.id])
                    return;
                if (taskData.p_interval[state.id] > 0)
                {
                    taskData.p_currentTimer[state.id] -= state.dt;
                    if (taskData.p_currentTimer[state.id] <= 0)
                    {
                        taskData.p_currentTimer[state.id] += taskData.p_interval[state.id];
                        taskData.p_task.RunNormalTask(taskData.p_interval[state.id]);
                    }
                    else
                        return; // not run task
                }
                else
                    taskData.p_task.RunNormalTask(state.dt);
            }, stateValue);
		}

        private void LateUpdate()
        {
            _mapJobs.Complete(); //SearchJobs是每0.1s执行一次,所以不能保证
            SearchManager.Instance.FinishSearchJobs();// 完成上一帧调度的查找 Job 并分发结果（读 NativeArray）
            // 士兵位置写入 spatial grid（写 NativeArray）
            SoldierCtrl.Instance.SoldiersLaterUpdate();

            //必须确保移动任务和查找任务完成才能进行地图的重构
            JobHandle flowJob = WarMapCtrl.Instance.FlushFlowFieldMaps(); //更新流场寻路地图
            SpatialGridManager.Instance.FlushGridCommands(); //增删entity
            JobHandle spatialGridJob = SpatialGridManager.Instance.RebuildGrids();
            _mapJobs = JobHandle.CombineDependencies(flowJob, spatialGridJob);

            // 实体数据已更新后再调度本帧查找 Job, 依赖地图的重建任务
            SearchManager.Instance.StartSearchJobs(_mapJobs);
        }

#endregion

#region public functions

		public bool RegisterTask(ITask task, WE.TaskType type)
		{
			_taskActionQueue[(int)type].Enqueue(new TaskCommand
			{
				p_commType = TaskCommandType.REGISTER,
				p_task = task,
			});

			return true;
		}

		public void UnregisterTask(ITask task, WE.TaskType type)
        {
            //not check existence , may register and unregister in the same time slot
            _taskActionQueue[(int)type].Enqueue(new TaskCommand
            {
                p_commType = TaskCommandType.UNREGISTER,
                p_task = task,
            });
		}

		//interval：对于normal task是时间（s），对于fix task是fix uptime的count
		public bool ActiveTask(ITask task, WE.TaskType type, float interval = -1)
		{
            //not check existence , may register and active in the same time slot
            _taskActionQueue[(int)type].Enqueue(new TaskCommand
            {
                p_commType = TaskCommandType.ACTIVE,
                p_task = task,
                p_interval = interval,
            });

			return false;
		}

		public bool SuspendTask(ITask task, WE.TaskType type)
		{
            _taskActionQueue[(int)type].Enqueue(new TaskCommand
            {
                p_commType = TaskCommandType.SUSPEND,
                p_task = task,
            });

			return false;
		}
#endregion

#region private functions
		private IEnumerator InitAllWarField()
		{
			//make sure all Awake/Start finish
			yield return new WaitForEndOfFrame(); // 等待这一帧渲染完

            SearchManager.Instance.InitSearchManager();
			SystemCfgMgr.Instance.InitSysCfg();
			Utils.CreateRandomPool();
			WarResCtrl.Instance.InitWarRes();
			WarBuildingCtrl.Instance.InitWarBuildingCtrl();
			SoldierCtrl.Instance.InitSoldierCtrl();
			WeaponCtrl.Instance.InitWeaponCtrl();

			int level = 1;
			if(WarMapCtrl.Instance.InitMapCtrl(level, WE.Difficulty.NORMAL) == false)
			{
				GameLogger.LogError("Load level_" + level + " fail !!!");
				yield break;
			}

            CardCtrl.Instance.InitCarCtrl();
            UpgradeCtrl.Instance.InitUpgradeCtrl();
			//init ui after map is created, because the main menu need to get the soldier info from buildings
			//the buildings are created during the map creation
			UICtrl.Instance.InitUiCtrl();
            CameraCtrl.Instance.InitCameraCtrl();

            yield return null; //等待下一帧
            SoldierCtrl.Instance.StartWork();  //soldier must call firstly
            WarBuildingCtrl.Instance.StartWork();

            WarResCtrl.Instance.AddOcularStoneAt(SoldierDefines.SoldierLevel.BASICLEVEL, Vector2.zero, WE.OnGroundMapIndex);
            WarResCtrl.Instance.AddOcularStoneAt(SoldierDefines.SoldierLevel.BASICLEVEL, new Vector2(0.1f, 0.1f), WE.OnGroundMapIndex);
            WarResCtrl.Instance.AddOcularStoneAt(SoldierDefines.SoldierLevel.BASICLEVEL, new Vector2(0.1f, -0.1f), WE.OnGroundMapIndex);
		}

        private void MergeQueue(int queueId)
        {
            var stateValue = (self: this, id: queueId);
            _taskActionQueue[queueId].Flush(static (cmd, state) =>
            {
                switch (cmd.p_commType)
                {
                    case TaskCommandType.REGISTER:
                    {
                        if (state.self._taskMapping.TryGetValue(cmd.p_task, out var data) == false)
                        {
                            data = state.self._taskDataPool.Count > 0 ? state.self._taskDataPool.Pop() : new TaskData();
                            state.self.InitTaskData(data);
                            data.p_task = cmd.p_task;
                            data.p_isActive[state.id] = false;

                            state.self._taskMapping[cmd.p_task] = data;
                            state.self._taskPool[state.id].AddItemAsync(data);
                        }
                        else //注册另一种调用方式
                        {
                            if (data.p_isActive[state.id] == true)
                                GameLogger.LogError("Can not duplicately add task !");
                            else
                                state.self._taskPool[state.id].AddItemAsync(data);
                        }
                    }
                        break;
                    case TaskCommandType.UNREGISTER:
                    {
                        if (state.self._taskMapping.TryGetValue(cmd.p_task, out TaskData data) == true)
                        {
                            if(data.p_isActive[(int)WE.TaskType.FIXED] == false && data.p_isActive[(int)WE.TaskType.NORMAL] == false)
                                state.self._taskPool[state.id].RemoveItemAsync(data);
                            else
                                GameLogger.LogError("Can not unregister a active task");
                        }
                        else
                        {
                            GameLogger.LogError("Can not find task to unregister !");
                        }
                    }
                        break;
                    case TaskCommandType.ACTIVE:
                        if (state.self._taskMapping.TryGetValue(cmd.p_task, out TaskData activeData))
                        {
                            if (cmd.p_interval > 0) // <0 means need to call task in every update or fixupdate
                            {
                                if (state.id == (int)WE.TaskType.NORMAL)
                                    activeData.p_interval[state.id] = cmd.p_interval;
                                else
                                    activeData.p_interval[state.id] = Utils.CountOfFixUpdate(cmd.p_interval);
                            }

                            activeData.p_currentTimer[state.id] = activeData.p_interval[state.id];
                            activeData.p_isActive[state.id] = true;
                        }
                        else
                        {
                            GameLogger.LogError("Can not find task to active, not register yet !");
                        }
                        break;
                    case TaskCommandType.SUSPEND:
                        if (state.self._taskMapping.TryGetValue(cmd.p_task, out TaskData suspengData))
                        {
                            suspengData.p_isActive[state.id] = false;
                        }
                        else
                        {
                            GameLogger.LogError("Can not find task to suspend, not register yet !");
                        }
                        break;
                    default:
                        break;
                }
            }, stateValue);
        }

        //从_taskPool中删除数据的回调
        private void OnTaskRemoved(TaskData taskData)
        {
            _taskMapping.Remove(taskData.p_task);

            taskData.p_task = null; // 清除强引用，防内存泄漏
            _taskDataPool.Push(taskData); // 空壳扔回栈池
        }

        private void InitTaskData(TaskData taskData)
        {
            taskData.p_task = null;
            taskData.p_isActive = new bool[(int)WE.TaskType.MAX];
            taskData.p_interval = new float[(int)WE.TaskType.MAX];
            taskData.p_currentTimer = new float[(int)WE.TaskType.MAX];
            for (int i = 1; i < 3; i++)
            {
                taskData.p_isActive[i] = false;
                taskData.p_currentTimer[i] = 0;
                taskData.p_interval[i] = -1;
            }
        }
#endregion
	}
}

