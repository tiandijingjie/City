using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
	using UD = UIDefines;
    using WE = WarFieldElements;

    public class UiIoTask : ITask
    {
#region public parameters

        public static UiIoTask Instance = null;

#endregion

#region private parameters
        private enum IoCommandType : byte
        {
            MIN = 0,
            AddListener,
            RemoveListener,
            MAX,
        }

        //command
        private struct IoCommand
        {
            public IoCommandType p_cmdType;
            public UIDefines.UIEventGroupType p_groupType;
            public IoObserverIntf p_owner;
            public KeyCode p_key;
            public string p_keyAlias;
            public UIDefines.UiIoEventType p_evtType;
            public UIDefines.IoEvtScheduleType p_scheType;
        }
        private ConcurrentQueue<IoCommand> _cmdQueue; //指令队列，实现无锁 0GC 事件处理

        private IoEvtGroup[] _groups;
        private List<KeyToGroup> _keyToGroups;
        private UD.UIEventGroupType _occupiedGroupType;
        private readonly object _occupyLock = new object();

#endregion

#region private parameters' get set
        public UD.UIEventGroupType gs_occupiedGroupType
        {
            get { return _occupiedGroupType; }
        }

#endregion

#region public functions

        public UiIoTask()
        {
            if (Instance != null)
            {
                throw new InvalidOperationException("Can not create second instance of UiIoTask");
            }

            Instance = this;
            _groups = new IoEvtGroup[(int)UD.UIEventGroupType.MAX];
            for (int i = 1; i < _groups.Length; i++)
                _groups[i] = new IoEvtGroup((UD.UIEventGroupType)i);
            _keyToGroups = new List<KeyToGroup>();

            // 初始化无锁队列
            _cmdQueue = new ConcurrentQueue<IoCommand>();

            WarFieldGameManager.Instance.RegisterTask(this, WE.TaskType.NORMAL);
            WarFieldGameManager.Instance.ActiveTask(this, WE.TaskType.NORMAL);
        }

        public bool OccupyExclusiveOwnerShip(UD.UIEventGroupType ownerShip)
        {
            lock (_occupyLock)
            {
                if (_occupiedGroupType == ownerShip)
                    return true;
                if (_occupiedGroupType != UD.UIEventGroupType.MIN)
                {
                    GameLogger.LogWarning($"{_occupiedGroupType} is occupied the IO, can not set a new one {ownerShip}");
                    return false;
                }

                for (UD.UIEventGroupType i = UD.UIEventGroupType.MIN + 1; i < UD.UIEventGroupType.MAX; i++)
                {
                    if (i == ownerShip)
                    {
                        _groups[(int)i].SetStatus(true);
                        _occupiedGroupType = i;
                    }
                    else
                        _groups[(int)i].SetStatus(false);
                }
            }

            return true;
        }

        //实际会在RunTask最后完成释放
        //因为有可能出现在一个run里面释放之后立刻被占用的情况，出现问题。所有当一个run遍历完之后再进行释放
        public void ReleaseExclusiveOwnerShip(UD.UIEventGroupType ownerShip)
        {
            lock (_occupyLock)
            {
                if (_occupiedGroupType != ownerShip)
                {
                    GameLogger.LogWarning($"{_occupiedGroupType} is occupied the IO, {ownerShip} can not release it");
                    return;
                }

                for (UD.UIEventGroupType i = UD.UIEventGroupType.MIN + 1; i < UD.UIEventGroupType.MAX; i++)
                {
                    _groups[(int)i].SetStatus(true);
                }

                _occupiedGroupType = UD.UIEventGroupType.MIN;
            }
        }

        public void AddIoEvtTask(UD.UIEventGroupType groupType, IoObserverIntf owner, KeyCode key, string keyAlias,
            UD.UiIoEventType evtType, UD.IoEvtScheduleType scheType = UD.IoEvtScheduleType.LIST)
        {
            _cmdQueue.Enqueue(new IoCommand
            {
                p_cmdType = IoCommandType.AddListener,
                p_groupType = groupType,
                p_owner = owner,
                p_key = key,
                p_keyAlias = keyAlias,
                p_evtType = evtType,
                p_scheType = scheType
            });
        }

        public void RemoveIoEvtTask(UD.UIEventGroupType groupType, IoObserverIntf owner, KeyCode key, string keyAlias,
            UD.UiIoEventType evtType, UD.IoEvtScheduleType scheType = UD.IoEvtScheduleType.LIST)
        {
            _cmdQueue.Enqueue(new IoCommand
            {
                p_cmdType = IoCommandType.RemoveListener,
                p_groupType = groupType,
                p_owner = owner,
                p_key = key,
                p_keyAlias = keyAlias,
                p_evtType = evtType,
                p_scheType = scheType
            });
        }

        public void RunNormalTask(float deltaTime)
        {
            //执行所有的修改指令
            while (_cmdQueue.TryDequeue(out IoCommand cmd))
                ExecuteCommand(ref cmd);

            int count = _keyToGroups.Count;
            lock (_occupyLock)
            {
                for (int i = 0; i < count; i++)
                {
                    KeyToGroup ktg = _keyToGroups[i];

                    if (ktg.p_groupListers[(int)UD.UiIoEventType.KEYDOWN].Count > 0 && Input.GetKeyDown(ktg.p_key))
                    {
                        var list = ktg.p_groupListers[(int)UD.UiIoEventType.KEYDOWN];
                        for (int j = 0; j < list.Count; j++)
                            _groups[(int)list[j]].NotifyListeners(ktg.p_key, UD.UiIoEventType.KEYDOWN);
                    }
                    else if (ktg.p_groupListers[(int)UD.UiIoEventType.KEYUP].Count > 0 && Input.GetKeyUp(ktg.p_key))
                    {
                        var list = ktg.p_groupListers[(int)UD.UiIoEventType.KEYUP];
                        for (int j = 0; j < list.Count; j++)
                            _groups[(int)list[j]].NotifyListeners(ktg.p_key, UD.UiIoEventType.KEYUP);
                    }
                }
            }
        }

        public void RunFixTask(float deltaTime)
        {
            throw new NotImplementedException();
        }

        public bool RegisterGroupStatusListener(UD.UIEventGroupType groupType, IoGroupChangeIntf groupStatusChangeListener)
        {
            if (_groups[(int)groupType] == null)
                return false;
            return _groups[(int)groupType].AddStatusNotifier(groupStatusChangeListener);
        }

#endregion

#region private function

        private KeyToGroup GetKeyToGroup(KeyCode key)
        {
            int cnt = _keyToGroups.Count;
            for (int i = cnt - 1; i >= 0; i--)
            {
                if (_keyToGroups[i].p_key == key)
                    return _keyToGroups[i];
            }

            return null;
        }

        //执行增删，修改独占的指令操作
        private void ExecuteCommand(ref IoCommand cmd)
        {
            switch (cmd.p_cmdType)
            {
                case IoCommandType.AddListener:
                    if (_groups[(int)cmd.p_groupType] == null) return;

                    KeyToGroup addKtg = GetKeyToGroup(cmd.p_key);
                    if (addKtg == null)
                    {
                        addKtg = new KeyToGroup(cmd.p_key);
                        addKtg.p_groupListers[(int)cmd.p_evtType].Add(cmd.p_groupType);
                        _keyToGroups.Add(addKtg);
                    }
                    else if (!addKtg.p_groupListers[(int)cmd.p_evtType].Contains(cmd.p_groupType))
                    {
                        addKtg.p_groupListers[(int)cmd.p_evtType].Add(cmd.p_groupType);
                    }
                    _groups[(int)cmd.p_groupType].AddListener(cmd.p_owner, cmd.p_key, cmd.p_keyAlias, cmd.p_evtType, cmd.p_scheType);
                    break;

                case IoCommandType.RemoveListener:
                    if (_groups[(int)cmd.p_groupType] == null)
                        return;

                    KeyToGroup rmKtg = GetKeyToGroup(cmd.p_key);
                    if (rmKtg != null && _groups[(int)cmd.p_groupType].RemoveListener(cmd.p_owner, cmd.p_key, cmd.p_keyAlias, cmd.p_evtType, cmd.p_scheType))
                    {
                        rmKtg.p_groupListers[(int)cmd.p_evtType].Remove(cmd.p_groupType);

                        bool allEmpty = true;
                        for (int i = 1; i < (int)UD.UiIoEventType.MAX; i++)
                        {
                            if (rmKtg.p_groupListers[i].Count > 0)
                            {
                                allEmpty = false;
                                break;
                            }
                        }
                        if (allEmpty) _keyToGroups.Remove(rmKtg);
                    }
                    break;
                default:
                    break;
            }
        }
#endregion

#region private typedefine

        private class IoEvtGroup
        {
            private bool _isEnabled;
            private List<IoGroupChangeIntf> _statusNotifier; //group _isEnabled 改变时通知
            private UD.UIEventGroupType _groupType;
            private Dictionary<KeyCode, List<string>> _keyAliasDic; //KeyCode -> List<key alias name>(key 别名)

            private Dictionary<string, List<IoObserverIntf>[,]>
                _listeners; //key alias name(key 别名) -> [UiIoEventType, IoEvtScheduleType]

            public IoEvtGroup(UD.UIEventGroupType groupType)
            {
                _groupType = groupType;
                _keyAliasDic = new Dictionary<KeyCode, List<string>>();
                _listeners = new Dictionary<string, List<IoObserverIntf>[,]>();
                _isEnabled = true;
            }

            public bool AddStatusNotifier(IoGroupChangeIntf status)
            {
                if (_statusNotifier == null)
                    _statusNotifier = new List<IoGroupChangeIntf>();
                if (_statusNotifier.Contains(status) == true)
                    return false;
                _statusNotifier.Add(status);
                return true;
            }

            public bool AddListener(IoObserverIntf owner, KeyCode key, string keyAlias, UD.UiIoEventType evtType,
                UD.IoEvtScheduleType scheType)
            {

                List<IoObserverIntf> list = null;
                if (_keyAliasDic.ContainsKey(key) == true)
                {
                    if (_keyAliasDic[key].Contains(keyAlias) == true)
                    {
                        list = _listeners[keyAlias][(int)evtType, (int)scheType];
                        if (list != null)
                        {
                            if (list.Contains(owner) == true)
                            {
                                GameLogger.LogWarning($"{_groupType} {keyAlias} already added, can not add again");
                                return false;
                            }
                            else
                                list.Add(owner);
                        }
                        else
                        {
                            _listeners[keyAlias][(int)evtType, (int)scheType] = new List<IoObserverIntf>();
                            _listeners[keyAlias][(int)evtType, (int)scheType].Add(owner);
                        }
                    }
                    else
                    {
                        _keyAliasDic[key].Add(keyAlias);
                        _listeners.Add(keyAlias,
                            new List<IoObserverIntf>[(int)UD.UiIoEventType.MAX, (int)UD.IoEvtScheduleType.MAX]);
                        _listeners[keyAlias][(int)evtType, (int)scheType] = new List<IoObserverIntf>();
                        _listeners[keyAlias][(int)evtType, (int)scheType].Add(owner);
                    }
                }
                else
                {
                    _keyAliasDic.Add(key, new List<string>());
                    _keyAliasDic[key].Add(keyAlias);
                    _listeners.Add(keyAlias,
                        new List<IoObserverIntf>[(int)UD.UiIoEventType.MAX, (int)UD.IoEvtScheduleType.MAX]);
                    _listeners[keyAlias][(int)evtType, (int)scheType] = new List<IoObserverIntf>();
                    _listeners[keyAlias][(int)evtType, (int)scheType].Add(owner);
                }

                GameLogger.LogTrace($"AddListener {key} {keyAlias} {evtType} {scheType}");
                return true;
            }

            //return true: means not lister listen on this key anymore
            public bool RemoveListener(IoObserverIntf owner, KeyCode key, string keyAlias, UD.UiIoEventType evtType,
                UD.IoEvtScheduleType scheType)
            {
                try
                {
                    _listeners[keyAlias][(int)evtType, (int)scheType].Remove(owner);
                    GameLogger.LogTrace($"RemoveListener {key} {keyAlias} {evtType} {scheType}");
                    var array = _listeners[keyAlias];
                    bool allEmpty = true;
                    for (int i = 0; i < array.GetLength(0); i++)
                    {
                        for (int j = 0; j < array.GetLength(1); j++)
                        {
                            if (array[i, j]?.Count > 0)
                            {
                                allEmpty = false;
                                break;
                            }
                        }

                        if (allEmpty == false)
                            break;
                    }

                    if (allEmpty == true) //keyAlias下没有监听者
                    {
                        _listeners.Remove(keyAlias);
                        _keyAliasDic[key].Remove(keyAlias); //将这个别名从keycode删除
                        if (_keyAliasDic[key].Count == 0) //the group not listen this key anymore
                            return true;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }

                return false;
            }

            public void NotifyListeners(KeyCode key, UD.UiIoEventType evtType)
            {
                if (_isEnabled == false)
                    return;

                List<string> keyAliasList = _keyAliasDic[key];
                int cnt = keyAliasList.Count;
                for (int i = 0; i < cnt; i++)
                {
                    string keyAlias = keyAliasList[i];
                    for (UD.IoEvtScheduleType j = UD.IoEvtScheduleType.LIST; j < UD.IoEvtScheduleType.MAX; j++)
                    {
                        var listeners = _listeners[keyAlias];
                        List<IoObserverIntf> list = listeners[(int)evtType, (int)j];
                        switch (j)
                        {
                            case UD.IoEvtScheduleType.LIST:
                                if (list != null)
                                {
                                    for (int k = 0; k < list.Count; k++)
                                        list[k].OnIoEvtNotification(keyAlias, evtType);
                                }
                                break;
                            case UD.IoEvtScheduleType.STACK:
                                if (list != null && list.Count > 0)
                                    list[list.Count - 1].OnIoEvtNotification(keyAlias, evtType);
                                break;
                            case UD.IoEvtScheduleType.FIFO:
                                if (list != null && list.Count > 0)
                                    list[0].OnIoEvtNotification(keyAlias, evtType);
                                break;
                            default:
                                break;
                        }
                    }
                }
            }

            public void SetStatus(bool status)
            {
                if (_isEnabled == status)
                    return;
                _isEnabled = status;
                if (_statusNotifier != null)
                {
                    int cnt = _statusNotifier.Count;
                    for (int i = 0; i < cnt; i++)
                    {
                        _statusNotifier[i].OnGroupStatusChange(_isEnabled);
                    }
                }
            }
        }

        //bind key to group
        private class KeyToGroup
        {
            public KeyCode p_key;
            public List<UD.UIEventGroupType>[] p_groupListers; //[UD.UiIoEventType.MAX]

            public KeyToGroup(KeyCode key)
            {
                p_key = key;
                p_groupListers = new List<UD.UIEventGroupType>[(int)UD.UiIoEventType.MAX];
                for (int i = 1; i < (int)UD.UiIoEventType.MAX; i++)
                    p_groupListers[i] = new List<UD.UIEventGroupType>();
            }
        }

#endregion
    }
}



