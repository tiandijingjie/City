using System;
using System.Collections.Generic;
using System.Threading;

// 高效增删和遍历的数据结构 (基于 Swap-And-Pop 算法)
// 不支持存储 Null 和重复元素。
// 使用了交换删除的方式，必须存储不关心存续顺序的数据！
// 支持可选的读写锁 (ReaderWriterLockSlim) 线程安全机制。
// 与DataPool区别是使用了读写锁，可以防止同线程的重入， 不加锁的时候与DataPool一样
public class DataPoolRw<Ttype> : IDisposable
{
#region private parameters

    private readonly Dictionary<Ttype, int> _map;
    private readonly List<Ttype> _list;

    // 是否启用线程安全
    private readonly bool _threadSafe;

    // 读写锁，替代原本繁琐的 Monitor.Enter
    private readonly ReaderWriterLockSlim _rwLock;

#endregion

#region Properties

    public int Count
    {
        get
        {
            if (_threadSafe)
                _rwLock.EnterReadLock();
            try
            {
                return _list.Count;
            }
            finally
            {
                if (_threadSafe)
                    _rwLock.ExitReadLock();
            }
        }
    }

    //禁止这样访问，性能极差
    // public Ttype this[int index]
    // {
    //     get
    //     {
    //         if (_threadSafe)
    //             _rwLock.EnterReadLock();
    //         try
    //         {
    //             return _list[index];
    //         }
    //         finally
    //         {
    //             if (_threadSafe)
    //                 _rwLock.ExitReadLock();
    //         }
    //     }
    // }

#endregion

#region public functions

    public DataPoolRw(bool threadSafe, int capacity = 16)
    {
        _map = new Dictionary<Ttype, int>(capacity);
        _list = new List<Ttype>(capacity);

        _threadSafe = threadSafe;
        if (_threadSafe)
        {
            _rwLock = new ReaderWriterLockSlim();
        }
    }

    public void Dispose()
    {
        _rwLock?.Dispose();
    }

    // 常规遍历接口
    public void ForEach(Action<Ttype> action)
    {
        if (action == null)
            return;

        if (_threadSafe)
            _rwLock.EnterReadLock();
        try
        {
            int count = _list.Count;
            for (int i = 0; i < count; i++)
            {
                action(_list[i]);
            }
        }
        finally
        {
            if (_threadSafe)
                _rwLock.ExitReadLock();
        }
    }

    // 带状态的遍历接口 (避免闭包产生 GC)
    public void ForEach<TState>(Action<Ttype, TState> action, TState state)
    {
        if (action == null)
            return;

        if (_threadSafe)
            _rwLock.EnterReadLock();
        try
        {
            int count = _list.Count;
            for (int i = 0; i < count; i++)
            {
                action(_list[i], state);
            }
        }
        finally
        {
            if (_threadSafe)
                _rwLock.ExitReadLock();
        }
    }

    public bool AddItem(Ttype value)
    {
        if (value == null)
            return false;

        if (_threadSafe)
            _rwLock.EnterWriteLock();
        try
        {
            if (_map.TryAdd(value, _list.Count))
            {
                _list.Add(value);
                return true;
            }

            return false;
        }
        finally
        {
            if (_threadSafe)
                _rwLock.ExitWriteLock();
        }
    }

    public void RemoveItem(Ttype value)
    {
        if (value == null)
            return;

        if (_threadSafe)
            _rwLock.EnterWriteLock();
        try
        {
            if (_map.TryGetValue(value, out var index))
            {
                ExecuteSwapAndPop(index);
            }
        }
        finally
        {
            if (_threadSafe)
                _rwLock.ExitWriteLock();
        }
    }

    public void RemoveItemAt(int index)
    {
        if (_threadSafe)
            _rwLock.EnterWriteLock();
        try
        {
            if (index < 0 || index >= _list.Count)
                return;
            ExecuteSwapAndPop(index);
        }
        finally
        {
            if (_threadSafe)
                _rwLock.ExitWriteLock();
        }
    }

    public Ttype PopOut()
    {
        if (_threadSafe)
            _rwLock.EnterWriteLock();
        try
        {
            if (_list.Count == 0)
                return default;

            int lastIndex = _list.Count - 1;
            Ttype ret = _list[lastIndex];

            _list.RemoveAt(lastIndex);
            _map.Remove(ret);

            return ret;
        }
        finally
        {
            if (_threadSafe)
                _rwLock.ExitWriteLock();
        }
    }

    public bool Contains(Ttype value)
    {
        if (value == null)
            return false;

        if (_threadSafe)
            _rwLock.EnterReadLock();
        try
        {
            return _map.ContainsKey(value);
        }
        finally
        {
            if (_threadSafe)
                _rwLock.ExitReadLock();
        }
    }

    public void Clear()
    {
        if (_threadSafe)
            _rwLock.EnterWriteLock();
        try
        {
            _map.Clear();
            _list.Clear();
        }
        finally
        {
            if (_threadSafe)
                _rwLock.ExitWriteLock();
        }
    }

#endregion

#region private functions

    // 内部方法不加锁，由调用它的公有方法负责加锁
    private void ExecuteSwapAndPop(int index)
    {
        int lastIndex = _list.Count - 1;
        Ttype valueToRemove = _list[index];

        if (index < lastIndex)
        {
            Ttype lastElement = _list[lastIndex];
            _list[index] = lastElement;
            _map[lastElement] = index;
        }

        _list.RemoveAt(lastIndex);
        _map.Remove(valueToRemove);
    }

#endregion
}
