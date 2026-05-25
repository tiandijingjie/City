using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Jobs;

//基本与AsyncDataPool一致,为了burst的IJobParallelForTransform计算transform,特别处理了transform
public class AsyncDataPoolForTransform<Ttype> : IDisposable
{
#region public parameters

#endregion

#region private parameters

    // 主数据区（受读写锁保护，允许多线程并发读取，单线程排他修改）
    private readonly Dictionary<Ttype, int> _mainMap;
    private readonly List<Ttype> _mainList;

    // 异步缓冲队列（双缓冲设计，实现 0 GC）
    private List<Ttype> _pendingAdds;
    private List<Ttype> _processingAdds; // 用于后台合并，避免占用 _addLock

    private List<Ttype> _pendingRemoves;
    private List<Ttype> _processingRemoves;

    // 细粒度锁（增删分离，避免生产者之间的竞争）
    private readonly object _addLock = new object();
    private readonly object _removeLock = new object();

    // 读写锁，用于保护主数据区 (_mainList 和 _mainMap)
    private readonly ReaderWriterLockSlim _mainLock;

    //回调
    private Action<Ttype> _onItemAdded;
    private Action<Ttype> _onItemRemoved;

    //transform
    private TransformAccessArray _transformArray;
    private Func<Ttype, Transform> _getTransformFunc; //获取transform的回调
    private bool _useTransformSync = false;
    private int _transformCapacity;
#endregion

#region private parameters' get set

    public int Count
    {
        get
        {
            _mainLock.EnterReadLock();
            try
            {
                return _mainList.Count;
            }
            finally
            {
                _mainLock.ExitReadLock();
            }
        }
    }

    public TransformAccessArray gs_transformArray
    {
        get { return _transformArray; }
    }

    //禁止这样访问，性能极差
    // public Ttype this[int index]
    // {
    //     get
    //     {
    //         _mainLock.EnterReadLock();
    //         try
    //         {
    //             return _mainList[index];
    //         }
    //         finally
    //         {
    //             _mainLock.ExitReadLock();
    //         }
    //     }
    // }

#endregion

#region public functions

    public AsyncDataPoolForTransform(int capacity = 16)
    {
        _mainMap = new Dictionary<Ttype, int>(capacity);
        _mainList = new List<Ttype>(capacity);

        _pendingAdds = new List<Ttype>(capacity);
        _processingAdds = new List<Ttype>(capacity);

        _pendingRemoves = new List<Ttype>(capacity);
        _processingRemoves = new List<Ttype>(capacity);

        //允许同一个线程在持有写锁时，向下兼容获取读锁
        _mainLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
    }

    public void EnableTransformSync(int capacity, Func<Ttype, Transform> getTransformFunc)
    {
        _transformCapacity = capacity;
        _transformArray = new TransformAccessArray(capacity);
        _getTransformFunc = getTransformFunc;
        _useTransformSync = true;
    }

    // ==========================================
    // 异步投递接口 (任何线程可随时调用)
    // ==========================================

    /// <summary>
    /// 异步添加：将对象压入添加缓冲队列
    /// </summary>
    public void AddItemAsync(Ttype item)
    {
        if (item == null)
            return;

        var id = Utils.GetClassAddrId(this);
        lock (_addLock)
        {
            _pendingAdds.Add(item);
        }
    }

    // 异步删除：将对象压入删除缓冲队列
    public void RemoveItemAsync(Ttype item)
    {
        if (item == null)
            return;

        var id = Utils.GetClassAddrId(this);
        lock (_removeLock)
        {
            _pendingRemoves.Add(item);
        }
    }

    // 只读遍历。
    // 绝对安全，不会触发实际的新增和删除。
    public void ForEachReadOnly(Action<Ttype> action)
    {
        if (action == null)
            return;

        _mainLock.EnterReadLock(); // 开启读锁
        try
        {
            int count = _mainList.Count;
            for (int i = 0; i < count; i++)
            {
                action(_mainList[i]);
            }
        }
        finally
        {
            _mainLock.ExitReadLock();
        }
    }

    // 提供带状态的版本以支持 0 GC
    public void ForEachReadOnly<TState>(Action<Ttype, TState> action, TState state)
    {
        if (action == null)
            return;
        _mainLock.EnterReadLock();
        try
        {
            int count = _mainList.Count;
            for (int i = 0; i < count; i++)
                action(_mainList[i], state);
        }
        finally
        {
            _mainLock.ExitReadLock();
        }
    }

    // 遍历主队列，然后处理所有的异步增删请求。
    // 如果 action 为空，则直接跳过遍历，仅执行 Flush（合并数据）。
    //flushAtLast: true 先遍历再flush
    //             flase 先flush再遍历
    public void ForEachAndFlush(Action<Ttype> action, bool flushAtLast = true)
    {
        if (flushAtLast == false)
        {
            _mainLock.EnterWriteLock();
            try
            {
                FlushAdds();
                FlushRemoves();
            }
            finally
            {
                _mainLock.ExitWriteLock();
            }
        }
        //如果有传入 action，先执行安全的只读遍历
        if (action != null)
        {
            _mainLock.EnterReadLock();
            try
            {
                int count = _mainList.Count;
                for (int i = 0; i < count; i++)
                {
                    action(_mainList[i]);
                }
            }
            finally
            {
                _mainLock.ExitReadLock();
            }
        }

        if (flushAtLast == true)
        {
            _mainLock.EnterWriteLock();
            try
            {
                FlushAdds();
                FlushRemoves();
            }
            finally
            {
                _mainLock.ExitWriteLock();
            }
        }
    }

    // 带状态
    public void ForEachAndFlush<TState>(Action<Ttype, TState> action, TState state, bool flushAtLast = true)
    {
        if (flushAtLast == false)
        {
            _mainLock.EnterWriteLock();
            try
            {
                FlushRemoves();
                FlushAdds();
            }
            finally
            {
                _mainLock.ExitWriteLock();
            }
        }

        if (action != null)
        {
            _mainLock.EnterReadLock();
            try
            {
                int count = _mainList.Count;
                for (int i = 0; i < count; i++)
                {
                    action(_mainList[i], state);
                }
            }
            finally
            {
                _mainLock.ExitReadLock();
            }
        }

        if (flushAtLast == true)
        {
            _mainLock.EnterWriteLock();
            try
            {
                FlushRemoves();
                FlushAdds();
            }
            finally
            {
                _mainLock.ExitWriteLock();
            }
        }
    }

    //找到需要的数据，返回
    // 支持提前中断的遍历接口。
    // 传入的 action 返回 true 时，立即中断循环并返回 true；如果遍历完没找到，返回 false。
    public bool FindFirst(Func<Ttype, bool> match, out Ttype result)
    {
        result = default;
        if (match == null)
            return false;

        _mainLock.EnterReadLock();
        try
        {
            int count = _mainList.Count;
            for (int i = 0; i < count; i++)
            {
                // 如果条件成立，直接跳出循环
                if (match(_mainList[i]))
                {
                    result = _mainList[i];
                    return true;
                }
            }
            return false;
        }
        finally
        {
            _mainLock.ExitReadLock();
        }
    }

    //找到需要的数据，返回
    // 带状态的提前中断遍历接口 (0 GC)
    public bool FindFirst<TState>(Func<Ttype, TState, bool> match, TState state, out Ttype result)
    {
        result = default;
        if (match == null)
            return false;

        _mainLock.EnterReadLock();
        try
        {
            int count = _mainList.Count;
            for (int i = 0; i < count; i++)
            {
                // 如果条件成立，直接跳出循环
                if (match(_mainList[i], state))
                {
                    result = _mainList[i];
                    return true;
                }
            }
            return false;
        }
        finally
        {
            _mainLock.ExitReadLock();
        }
    }

    //action是针对整个数据进行处理
    //有返回值的版本
    public TResult ForList<TState, TResult>(Func<IReadOnlyList<Ttype>, TState, TResult> func, TState state)
    {
        _mainLock.EnterReadLock();
        try
        {
            return func(_mainList, state); // 返回处理结果
        }
        finally
        {
            _mainLock.ExitReadLock();
        }
    }

    //没有返回值的版本
    public void ForList<TState>(Action<IReadOnlyList<Ttype>, TState> action, TState state)
    {
        if (action == null)
            return;

        _mainLock.EnterReadLock();
        try
        {
            action(_mainList, state);
        }
        finally
        {
            _mainLock.ExitReadLock();
        }
    }

    // 同步弹出一个元素并将其从池中移除。
    // 默认弹出队尾元素，无需 Swap，性能最高 (O(1))。
    // 注意：此操作会申请写锁，短暂阻塞只读遍历。
    // <param name="item">弹出的元素</param>
    // <returns>是否成功弹出</returns>
    public bool PopOutSync(out Ttype item)
    {
        item = default;

        _mainLock.EnterWriteLock();
        try
        {
            if (_mainList.Count == 0)
                return false;

            // 直接获取最后一个元素
            int lastIndex = _mainList.Count - 1;
            item = _mainList[lastIndex];

            // 从列表和字典中移除 (不需要执行 SwapAndPop，因为本身就是最后一个)
            _mainList.RemoveAt(lastIndex);
            _mainMap.Remove(item);

            // 根据你的业务需求，决定是否在 Pop 时触发 Remove 回调
            _onItemRemoved?.Invoke(item);

            return true;
        }
        finally
        {
            _mainLock.ExitWriteLock();
        }
    }

    public void ClearAll()
    {
        _mainLock.EnterWriteLock();
        try
        {
            _mainList.Clear();
            _mainMap.Clear();
            lock (_addLock)
            {
                _pendingAdds.Clear();
                _processingAdds.Clear();
            }
            lock (_removeLock)
            {
                _pendingRemoves.Clear();
                _processingRemoves.Clear();
            }

            if (_useTransformSync && _transformArray.isCreated)
            {
                _transformArray.Dispose();
                _transformArray = new TransformAccessArray(_transformCapacity);
            }
        }
        finally
        {
            _mainLock.ExitWriteLock();
        }
    }

    public void RegisterOnItemAdded(Action<Ttype> callback)
    {
        if (callback != null)
            _onItemAdded += callback;
    }

    public void UnregisterOnItemAdded(Action<Ttype> callback)
    {
        if (callback != null)
            _onItemAdded -= callback;
    }

    public void RegisterOnItemRemoved(Action<Ttype> callback)
    {
        if (callback != null)
            _onItemRemoved += callback;
    }

    public void UnregisterOnItemRemoved(Action<Ttype> callback)
    {
        if (callback != null)
            _onItemRemoved -= callback;
    }

    //不能发在遍历中使用，效率极差
    public Ttype GetByIndex(int index)
    {
        _mainLock.EnterReadLock();
        try
        {
            return _mainList[index];
        }
        finally
        {
            _mainLock.ExitReadLock();
        }
    }

    //不能发在遍历中使用，效率极差
    public int GetIndex(Ttype item)
    {
        _mainLock.EnterReadLock();
        try
        {
            return _mainMap[item];
        }
        finally
        {
            _mainLock.ExitReadLock();
        }
    }

    //交换两个元素，基本上用不上，除非队列不删除数据
    public void SwapElements(int indexA, int indexB)
    {
        if (indexA == indexB)
            return;

        _mainLock.EnterWriteLock();
        try
        {
            if (indexA < 0 || indexA >= _mainList.Count || indexB < 0 || indexB >= _mainList.Count)
            {
                return;
            }

            Ttype elementA = _mainList[indexA];
            Ttype elementB = _mainList[indexB];

            _mainList[indexA] = elementB;
            _mainList[indexB] = elementA;

            _mainMap[elementA] = indexB;
            _mainMap[elementB] = indexA;
        }
        finally
        {
            _mainLock.ExitWriteLock();
        }
    }

    //IDisposable, 每个使用AsyncDataPool的对象需要主动调用这个函数来释放非托管锁
    public void Dispose()
    {
        _mainLock?.Dispose();
        if (_useTransformSync)
            _transformArray.Dispose();
    }
#endregion

#region private functions

    private void FlushRemoves()
    {
        // 极其短暂的锁：只为了交换 List 指针（耗时约 0.0001 毫秒）
        // 这样其他的异步线程立刻就能继续往 _pendingRemoves 里投递数据
        lock (_removeLock)
        {
            if (_pendingRemoves.Count == 0)
                return;

            List<Ttype> temp = _pendingRemoves;
            _pendingRemoves = _processingRemoves;
            _processingRemoves = temp;
        }

        // 无锁状态下从容处理删除，使用 O(1) 的 Swap-And-Pop
        int count = _processingRemoves.Count;
        for (int i = 0; i < count; i++)
        {
            Ttype item = _processingRemoves[i];
            if (_mainMap.TryGetValue(item, out int index))
            {
                _onItemRemoved?.Invoke(item);
                ExecuteSwapAndPop(index);
            }
        }

        // 处理完后清空处理队列，供下一次交换使用
        _processingRemoves.Clear();
    }

    private void FlushAdds()
    {
        // 同理，极速交换指针
        lock (_addLock)
        {
            if (_pendingAdds.Count == 0)
                return;

            List<Ttype> temp = _pendingAdds;
            _pendingAdds = _processingAdds;
            _processingAdds = temp;
        }

        // 无锁状态下处理新增
        int count = _processingAdds.Count;
        for (int i = 0; i < count; i++)
        {
            Ttype item = _processingAdds[i];
            if (_mainMap.TryAdd(item, _mainList.Count) == true)
            {
                _mainList.Add(item);
                if (_useTransformSync)
                {
                    if (_transformArray.length + 1 > _transformArray.capacity) //扩容, 效率极低
                    {
                        int newCapacity = Mathf.Max(16, _transformArray.capacity * 2);
                        TransformAccessArray oldArr = _transformArray;
                        TransformAccessArray tmpArr = new TransformAccessArray(newCapacity);
                        int cnt = _transformArray.length;
                        for (int j = 0; j < cnt; j++)
                            tmpArr.Add(oldArr[j]);
                        _transformArray = tmpArr;
                        oldArr.Dispose();  //必须要手动释放原数组
                    }
                    _transformArray.Add(_getTransformFunc(item)); //获取transform
                }

                _onItemAdded?.Invoke(item);
            }
            else
                GameLogger.LogError("Fail to add item in pool !!!");
        }

        _processingAdds.Clear();
    }

    private void ExecuteSwapAndPop(int index)
    {
        int lastIndex = _mainList.Count - 1;
        Ttype valueToRemove = _mainList[index];

        if (index < lastIndex)
        {
            Ttype lastElement = _mainList[lastIndex];
            _mainList[index] = lastElement;
            _mainMap[lastElement] = index;
        }

        _mainList.RemoveAt(lastIndex);
        _mainMap.Remove(valueToRemove);
        if (_useTransformSync)
            _transformArray.RemoveAtSwapBack(index);
    }

#endregion
}

