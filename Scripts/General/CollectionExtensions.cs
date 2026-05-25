using System.Collections.Generic;

public static class CollectionExtensions
{
    /// <summary>
    /// 判断 List 是否为 null 或者没有元素
    /// </summary>
    public static bool IsNullOrEmpty<T>(this List<T> list)
    {
        return list == null || list.Count == 0;
    }

    /// <summary>
    /// 判断 DataPool 是否为 null 或者没有元素
    /// </summary>
    public static bool IsNullOrEmpty<T>(this DataPool<T> pool)
    {
        return pool == null || pool.Count == 0;
    }

    /// <summary>
    /// 判断 AsyncDataPool 是否为 null 或者没有元素
    /// </summary>
    public static bool IsNullOrEmpty<T>(this AsyncDataPool<T> pool)
    {
        return pool == null || pool.Count == 0;
    }

    /// <summary>
    /// 判断 AsyncDataList 是否为 null 或者没有元素
    /// </summary>
    public static bool IsNullOrEmpty<T>(this AsyncCommandBuffer<T> list)
    {
        return list == null || list.Count == 0;
    }
}
