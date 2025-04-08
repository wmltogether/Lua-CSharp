using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Lua.CodeAnalysis;
using Lua.Runtime;

namespace Lua;

[StructLayout(LayoutKind.Auto)]
public readonly record struct LuaFunctionExecutionContext
{
    public required LuaState State { get; init; }
    public required LuaThread Thread { get; init; }
    public required int ArgumentCount { get; init; }
    public required int FrameBase { get; init; }
    public required int ReturnFrameBase { get; init; }
    public SourcePosition? SourcePosition { get; init; }
    public string? RootChunkName { get; init; }
    public string? ChunkName { get; init; }
    public int? CallerInstructionIndex { get; init; }
    public object? AdditionalContext { get; init; }

    public ReadOnlySpan<LuaValue> Arguments
    {
        get { return Thread.GetStackValues().Slice(FrameBase, ArgumentCount); }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasArgument(int index)
    {
        return ArgumentCount > index && Arguments[index].Type is not LuaValueType.Nil;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LuaValue GetArgument(int index)
    {
        ThrowIfArgumentNotExists(index);
        return Arguments[index];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal LuaValue GetArgumentOrDefault(int index, LuaValue defaultValue = default)
    {
        if (ArgumentCount <= index)
        {
            return defaultValue;
        }

        return Arguments[index];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetArgument<T>(int index)
    {
        ThrowIfArgumentNotExists(index);

        var arg = Arguments[index];
        if (!arg.TryRead<T>(out var argValue))
        {
            var t = typeof(T);
            if ((t == typeof(int) || t == typeof(long)) && arg.TryReadNumber(out _))
            {
                LuaRuntimeException.BadArgumentNumberIsNotInteger(State.GetTraceback(), index + 1, Thread.GetCurrentFrame().Function.Name);
            }
            else if (LuaValue.TryGetLuaValueType(t, out var type))
            {
                LuaRuntimeException.BadArgument(State.GetTraceback(), index + 1, Thread.GetCurrentFrame().Function.Name, type.ToString(), arg.Type.ToString());
            }
            else if (arg.Type is LuaValueType.UserData or LuaValueType.LightUserData)
            {
                LuaRuntimeException.BadArgument(State.GetTraceback(), index + 1, Thread.GetCurrentFrame().Function.Name, t.Name, arg.UnsafeRead<object>()?.GetType().ToString() ?? "userdata: 0");
            }
            else
            {
                LuaRuntimeException.BadArgument(State.GetTraceback(), index + 1, Thread.GetCurrentFrame().Function.Name, t.Name, arg.Type.ToString());
            }
        }

        return argValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal T GetArgumentOrDefault<T>(int index, T defaultValue = default!)
    {
        if (ArgumentCount <= index)
        {
            return defaultValue;
        }

        var arg = Arguments[index];

        if (arg.Type is LuaValueType.Nil)
        {
            return defaultValue;
        }

        if (!arg.TryRead<T>(out var argValue))
        {
            var t = typeof(T);
            if ((t == typeof(int) || t == typeof(long)) && arg.TryReadNumber(out _))
            {
                LuaRuntimeException.BadArgumentNumberIsNotInteger(State.GetTraceback(), index + 1, Thread.GetCurrentFrame().Function.Name);
            }
            else if (LuaValue.TryGetLuaValueType(t, out var type))
            {
                LuaRuntimeException.BadArgument(State.GetTraceback(), index + 1, Thread.GetCurrentFrame().Function.Name, type.ToString(), arg.Type.ToString());
            }
            else if (arg.Type is LuaValueType.UserData or LuaValueType.LightUserData)
            {
                LuaRuntimeException.BadArgument(State.GetTraceback(), index + 1, Thread.GetCurrentFrame().Function.Name, t.Name, arg.UnsafeRead<object>()?.GetType().ToString() ?? "userdata: 0");
            }
            else
            {
                LuaRuntimeException.BadArgument(State.GetTraceback(), index + 1, Thread.GetCurrentFrame().Function.Name, t.Name, arg.Type.ToString());
            }
        }

        return argValue;
    }

    public int Return()
    {
        Thread.Stack.PopUntil(ReturnFrameBase);
        return 0;
    }

    public int Return(LuaValue result)
    {
        var stack = Thread.Stack;
        stack.SetTop(ReturnFrameBase + 1);
        stack.FastGet(ReturnFrameBase) = result;
        return 1;
    }

    public int Return(LuaValue result0, LuaValue result1)
    {
        var stack = Thread.Stack;
        stack.SetTop(ReturnFrameBase + 2);
        stack.FastGet(ReturnFrameBase) = result0;
        stack.FastGet(ReturnFrameBase + 1) = result1;
        return 2;
    }

    public int Return(LuaValue result0, LuaValue result1, LuaValue result2)
    {
        var stack = Thread.Stack;
        stack.SetTop(ReturnFrameBase + 3);
        stack.FastGet(ReturnFrameBase) = result0;
        stack.FastGet(ReturnFrameBase + 1) = result1;
        stack.FastGet(ReturnFrameBase + 2) = result2;
        return 3;
    }

    public int Return(ReadOnlySpan<LuaValue> results)
    {
        var stack = Thread.Stack;
        stack.EnsureCapacity(ReturnFrameBase + results.Length);
        results.CopyTo(stack.GetBuffer()[ReturnFrameBase..(ReturnFrameBase + results.Length)]);
        stack.SetTop(ReturnFrameBase + results.Length);
        return results.Length;
    }

    internal int Return(LuaValue result0, ReadOnlySpan<LuaValue> results)
    {
        var stack = Thread.Stack;
        stack.EnsureCapacity(ReturnFrameBase + results.Length);
        stack.SetTop(ReturnFrameBase + results.Length + 1);
        var buffer = stack.GetBuffer();
        buffer[ReturnFrameBase] = result0;
        results.CopyTo(buffer[(ReturnFrameBase + 1)..(ReturnFrameBase + results.Length + 1)]);
        return results.Length + 1;
    }

    public Span<LuaValue> GetReturnBuffer(int count)
    {
        var stack = Thread.Stack;
        stack.SetTop(ReturnFrameBase + count);
        var buffer = stack.GetBuffer()[ReturnFrameBase..(ReturnFrameBase + count)];
        return buffer;
    }

    public CsClosure? GetCsClosure()
    {
        return Thread.GetCurrentFrame().Function as CsClosure;
    }

    internal void ThrowBadArgument(int index, string message)
    {
        LuaRuntimeException.BadArgument(State.GetTraceback(), index, Thread.GetCurrentFrame().Function.Name, message);
    }

    void ThrowIfArgumentNotExists(int index)
    {
        if (ArgumentCount <= index)
        {
            LuaRuntimeException.BadArgument(State.GetTraceback(), index + 1, Thread.GetCurrentFrame().Function.Name);
        }
    }
}