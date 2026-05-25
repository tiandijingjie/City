using System;
using System.Collections.Generic;
using System.Threading;

// 实现增删和遍历的数据结构（底层仅基于 List）
// 不支持存储 Null
// 支持可选是否允许重复元素
// 使用了交换删除的方式，所以必须存储不关心存续顺序的数据 ！！！
// 支持可选线程安全的 DataList
public class DataList<Ttype>
{
#region private parameters

    private readonly List<Ttype> _list;
    private readonly bool _allowDuplicate;

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

    // 构造函数：增加 threadSafe 参数和 allowDuplicate 参数
    public DataList(bool threadSafe, bool allowDuplicate, int capacity = 16)
    {
        _list = new List<Ttype>(capacity);
        _allowDuplicate = allowDuplicate;

        if (threadSafe == true)
            _syncRoot = new object();
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

            // 如果不允许重复，则需要遍历检查是否存在
            if (!_allowDuplicate && _list.Contains(value))
                return false;

            _list.Add(value);
            return true;
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

            if (value is null)
                return;

            // 找到第一个匹配元素的索引
            int index = _list.IndexOf(value);
            if (index >= 0)
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

            return ret;
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

            if (value is null)
                return false;

            return _list.Contains(value);
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

            _list.Clear();
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

    public int GetIndex(Ttype value)
    {
        bool lockTaken = false;
        try
        {
            if (_syncRoot != null)
                Monitor.Enter(_syncRoot, ref lockTaken);
            return _list.IndexOf(value);
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

        // 如果要删除的不是最后一个元素，则将最后一个元素覆盖到当前位置
        if (index < lastIndex)
        {
            _list[index] = _list[lastIndex];
        }

        // 移除末尾元素
        _list.RemoveAt(lastIndex);
    }

#endregion
}
