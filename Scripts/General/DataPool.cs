using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

//实现高效增删和遍历的数据结构
//不支持存储Null和重复元素
//使用了交换删除的方式，所以必须存储不关心存续顺序的数据 ！！！
// 支持可选线程安全的 DataPool
public class DataPool<Ttype>
{
#region public parameters

#endregion

#region private parameters

    private readonly Dictionary<Ttype, int> _map;
    private readonly List<Ttype> _list;

    // 同步锁对象，如果不需要线程安全，则保持为 null
    private readonly object _syncRoot;

#endregion

#region private parameters' get set

    // 访问 Count 时也需要保证线程安全，防止读时发生扩容/修改
    public int Count
    {
        get
        {
            bool lockTaken = false;
            try
            {
                if (_syncRoot != null)
                    Monitor.Enter(_syncRoot, ref lockTaken);
                return _list.Count;
            }
            finally
            {
                if (lockTaken)
                    Monitor.Exit(_syncRoot);
            }
        }
    }

#endregion

#region public functions

    // 构造函数：增加 threadSafe 参数
    public DataPool(bool threadSafe, int capacity = 16)
    {
        _map = new Dictionary<Ttype, int>(capacity);
        _list = new List<Ttype>(capacity);

        if (threadSafe == true)
            _syncRoot = new object();
    }

    // 每个元素单独处理
    // 常规遍历接口
    public void ForEach(Action<Ttype> action)
    {
        if (action == null) return;

        bool lockTaken = false;
        try
        {
            if (_syncRoot != null)
                Monitor.Enter(_syncRoot, ref lockTaken);

            int count = _list.Count;
            for (int i = 0; i < count; i++)
            {
                action(_list[i]);
            }
        }
        finally
        {
            if (lockTaken)
                Monitor.Exit(_syncRoot);
        }
    }

    // 每个元素单独处理
    // 带状态的遍历接口 (传入外部变量，避免闭包产生额外 GC)
    public void ForEach<TState>(Action<Ttype, TState> action, TState state)
    {
        if (action == null) return;

        bool lockTaken = false;
        try
        {
            if (_syncRoot != null)
                Monitor.Enter(_syncRoot, ref lockTaken);

            int count = _list.Count;
            for (int i = 0; i < count; i++)
            {
                action(_list[i], state);
            }
        }
        finally
        {
            if (lockTaken)
                Monitor.Exit(_syncRoot);
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

        bool lockTaken = false;
        try
        {
            if (_syncRoot != null)
                Monitor.Enter(_syncRoot, ref lockTaken);

            int count = _list.Count;
            for (int i = 0; i < count; i++)
            {
                // 如果条件成立，直接跳出循环
                if (match(_list[i]))
                {
                    result = _list[i];
                    return true;
                }
            }
            return false;
        }
        finally
        {
            if (lockTaken)
                Monitor.Exit(_syncRoot);
        }
    }

    //找到需要的数据，返回
    // 带状态的提前中断遍历接口 (0 GC)
    public bool FindFirst<TState>(Func<Ttype, TState, bool> match, TState state, out Ttype result)
    {
        result = default;
        if (match == null)
            return false;

        bool lockTaken = false;
        try
        {
            if (_syncRoot != null)
                Monitor.Enter(_syncRoot, ref lockTaken);

            int count = _list.Count;
            for (int i = 0; i < count; i++)
            {
                // 如果条件成立，直接跳出循环
                if (match(_list[i], state))
                {
                    result = _list[i];
                    return true;
                }
            }
            return false;
        }
        finally
        {
            if (lockTaken)
                Monitor.Exit(_syncRoot);
        }
    }

    //action是针对整个数据进行处理
    //有返回值的版本
    public TResult ForList<TState, TResult>(Func<IReadOnlyList<Ttype>, TState, TResult> func, TState state)
    {
        bool lockTaken = false;
        try
        {
            if (_syncRoot != null)
                Monitor.Enter(_syncRoot, ref lockTaken);
            return func(_list, state); // 返回处理结果
        }
        finally
        {
            if (lockTaken)
                Monitor.Exit(_syncRoot);
        }
    }

    //没有返回值的版本
    public void ForList<TState>(Action<IReadOnlyList<Ttype>, TState> action, TState state)
    {
        if (action == null)
            return;

        bool lockTaken = false;
        try
        {
            if (_syncRoot != null)
                Monitor.Enter(_syncRoot, ref lockTaken);

            action(_list, state);
        }
        finally
        {
            if (lockTaken)
                Monitor.Exit(_syncRoot);
        }
    }

    public bool AddItem(Ttype value)
    {
        bool lockTaken = false;
        try
        {
            if (_syncRoot != null)
                Monitor.Enter(_syncRoot, ref lockTaken);

            if (value is null)
                return false;

            if (_map.TryAdd(value, _list.Count))
            {
                _list.Add(value);
                return true;
            }

            return false;
        }
        finally
        {
            if (lockTaken)
                Monitor.Exit(_syncRoot);
        }
    }

    public void RemoveItem(Ttype value)
    {
        bool lockTaken = false;
        try
        {
            if (_syncRoot != null)
                Monitor.Enter(_syncRoot, ref lockTaken);

            if (_map.TryGetValue(value, out var index))
            {
                ExecuteSwapAndPop(index);
            }
        }
        finally
        {
            if (lockTaken)
                Monitor.Exit(_syncRoot);
        }
    }

    public void RemoveItemAt(int index)
    {
        bool lockTaken = false;
        try
        {
            if (_syncRoot != null)
                Monitor.Enter(_syncRoot, ref lockTaken);

            if (index < 0 || index >= _list.Count)
                return;

            ExecuteSwapAndPop(index);
        }
        finally
        {
            if (lockTaken)
                Monitor.Exit(_syncRoot);
        }
    }

    public Ttype PopOut()
    {
        bool lockTaken = false;
        try
        {
            if (_syncRoot != null)
                Monitor.Enter(_syncRoot, ref lockTaken);

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
            if (lockTaken)
                Monitor.Exit(_syncRoot);
        }
    }

    //不能发在遍历中使用，效率极差,除非没加锁
    public Ttype GetByIndex(int index)
    {
        bool lockTaken = false;
        try
        {
            if (_syncRoot != null)
                Monitor.Enter(_syncRoot, ref lockTaken);
            return _list[index];
        }
        finally
        {
            if (lockTaken)
                Monitor.Exit(_syncRoot);
        }
    }

    public bool Contains(Ttype value)
    {
        bool lockTaken = false;
        try
        {
            if (_syncRoot != null)
                Monitor.Enter(_syncRoot, ref lockTaken);
            return _map.ContainsKey(value);
        }
        finally
        {
            if (lockTaken)
                Monitor.Exit(_syncRoot);
        }
    }

    public void Clear()
    {
        bool lockTaken = false;
        try
        {
            if (_syncRoot != null)
                Monitor.Enter(_syncRoot, ref lockTaken);
            _map.Clear();
            _list.Clear();
        }
        finally
        {
            if (lockTaken)
                Monitor.Exit(_syncRoot);
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
