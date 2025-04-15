using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Lua.Internal;

/// <summary>
/// A list of minimal features. Note that it is NOT thread-safe and must NOT be marked readonly as it is a mutable struct.
/// </summary>
/// <typeparam name="T">Element type</typeparam>
[StructLayout(LayoutKind.Auto)]
public struct FastListCore<T>
{
    const int InitialCapacity = 8;

    public static readonly FastListCore<T> Empty = default;

    T[]? array;
    int tailIndex;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T element)
    {
        if (array == null)
        {
            array = new T[InitialCapacity];
        }
        else if (array.Length == tailIndex)
        {
            Array.Resize(ref array, MathEx.NewArrayCapacity(tailIndex));
        }

        array[tailIndex] = element;
        tailIndex++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RemoveAtSwapback(int index)
    {
        if (array == null) throw new IndexOutOfRangeException();
        CheckIndex(index);

        array![index] = array[tailIndex - 1];
        array[tailIndex - 1] = default!;
        tailIndex--;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Shrink(int newSize)
    {
        if (newSize >= tailIndex) return;

        array.AsSpan(newSize).Clear();
        tailIndex = newSize;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear(bool removeArray = false)
    {
        if (array == null) return;

        array.AsSpan().Clear();
        tailIndex = 0;
        if (removeArray) array = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureCapacity(int capacity)
    {
        if (array == null)
        {
            array = new T[InitialCapacity];
        }
        var newSize = capacity <= InitialCapacity ? InitialCapacity : MathEx.NewArrayCapacity(capacity);
        if (newSize != array.Length) Array.Resize(ref array, newSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(ref FastListCore<T> destination)
    {
        destination.EnsureCapacity(tailIndex);
        destination.tailIndex = tailIndex;
        AsSpan().CopyTo(destination.AsSpan());
    }

    public ref T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref array![index];
    }

    public readonly int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => tailIndex;
    }

    public readonly Span<T> AsSpan() => array == null ? Span<T>.Empty : array.AsSpan(0, tailIndex);
    public readonly T[]? AsArray() => array;

    readonly void CheckIndex(int index)
    {
        if (index < 0 || index > tailIndex) throw new IndexOutOfRangeException();
    }
}