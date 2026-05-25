using System;
using System.Collections.Generic;

// 极简双缓冲指令队列 
// 专为高频事件和跨线程指令投递设计，性能极致，0 GC。
// Flush 只能主线程调用
public class AsyncCommandBuffer<Ttype>
{
#region public parameters

#endregion

#region private parameters
    // 写缓冲区：接收所有子线程的投递
    private List<Ttype> _writeBuffer;

    // 读缓冲区：主线程专享的处理区
    private List<Ttype> _readBuffer;

    private readonly object _swapLock = new object();
#endregion

#region private parameters' get set
    public int Count
    {
        get
        {
            lock (_swapLock)
                return _readBuffer.Count;
        }
    }
#endregion

#region public functions
    public AsyncCommandBuffer(int capacity = 128)
    {
        _writeBuffer = new List<Ttype>(capacity);
        _readBuffer = new List<Ttype>(capacity);
    }

    // 任何线程均可调用：将指令压入队列
    public void Enqueue(Ttype command)
    {
        // 极短的加锁：仅为了 Add 操作的线程安全
        lock (_swapLock)
        {
            _writeBuffer.Add(command);
        }
    }

    // 仅限主线程调用 (如 LateUpdate)：批量处理并清空指令
    // 只能主线程调用 !!!!
    public void Flush(Action<Ttype> onProcess)
    {
        // 第一步：极速交换读写指针
        lock (_swapLock)
        {
            if (_writeBuffer.Count == 0)
                return;

            // 交换引用
            // 交换完成后，立马释放锁，子线程又可以继续往新的 _writeBuffer 里写数据了！
            List<Ttype> temp = _readBuffer;
            _readBuffer = _writeBuffer;
            _writeBuffer = temp;
        }

        // 无锁状态下执行指令
        int count = _readBuffer.Count;
        for (int i = 0; i < count; i++)
        {
            onProcess(_readBuffer[i]);
        }

        // 清空读缓冲区
        _readBuffer.Clear();
    }

    // 仅限主线程调用 (如 LateUpdate)：批量处理并清空指令
    // 只能主线程调用 !!!!
    public void Flush<TState>(Action<Ttype, TState> onProcess, TState state)
    {
        // 第一步：极速交换读写指针
        lock (_swapLock)
        {
            if (_writeBuffer.Count == 0)
                return;

            // 交换引用
            // 交换完成后，立马释放锁，子线程又可以继续往新的 _writeBuffer 里写数据了！
            List<Ttype> temp = _readBuffer;
            _readBuffer = _writeBuffer;
            _writeBuffer = temp;
        }

        // 无锁状态下执行指令
        int count = _readBuffer.Count;
        for (int i = 0; i < count; i++)
        {
            onProcess(_readBuffer[i], state);
        }

        // 清空读缓冲区
        _readBuffer.Clear();
    }
#endregion

#region private functions

#endregion
}
