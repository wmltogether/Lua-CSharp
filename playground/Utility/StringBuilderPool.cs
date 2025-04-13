using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.ObjectPool;

namespace Playground.Utility;

internal sealed class StringBuilderPool
{
    private const int DefaultPoolCapacity = 1024;

    private static readonly Lazy<StringBuilderPool> _shared = new(() => new StringBuilderPool(), true);
    public static StringBuilderPool Shared => _shared.Value;

    private ObjectPool<StringBuilder> _objectPool;

    private StringBuilderPool()
    {
        _objectPool = CreatePool();
    }

    public StringBuilder Allocate()
    {
        return _objectPool.Get();
    }

    public void Release(StringBuilder obj)
    {
        obj.Clear();
        _objectPool.Return(obj);
    }

    /// <summary>
    /// If someone need to create a private pool
    /// </summary>
    private static ObjectPool<StringBuilder> CreatePool(int size = 100, int capacity = DefaultPoolCapacity)
    {
        ObjectPool<StringBuilder> pool = null;
        pool = new DefaultObjectPool<StringBuilder>(new StringBuilderPooledObjectPolicy()
        {
            InitialCapacity = size,
            MaximumRetainedCapacity = capacity,
        });
        return pool;
    }
}