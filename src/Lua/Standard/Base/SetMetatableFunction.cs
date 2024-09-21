
using Lua.Runtime;

namespace Lua.Standard.Base;

public sealed class SetMetatableFunction : LuaFunction
{
    public override string Name => "setmetatable";
    public static readonly SetMetatableFunction Instance = new();

    protected override ValueTask<int> InvokeAsyncCore(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken cancellationToken)
    {
        var arg0 = ReadArgument<LuaTable>(context, 0);

        var arg1 = context.Arguments[1];
        if (arg1.Type is not (LuaValueType.Nil or LuaValueType.Table))
        {
            LuaRuntimeException.BadArgument(context.State.GetTracebacks(), 2, Name, [LuaValueType.Nil, LuaValueType.Table]);
        }

        if (arg0.Metatable != null && arg0.Metatable.TryGetValue(Metamethods.Metatable, out _))
        {
            throw new LuaRuntimeException(context.State.GetTracebacks(), "cannot change a protected metatable");
        }
        else if (arg1.Type is LuaValueType.Nil)
        {
            arg0.Metatable = null;
        }
        else
        {
            arg0.Metatable = arg1.Read<LuaTable>();
        }

        buffer.Span[0] = arg0;
        return new(1);
    }
}