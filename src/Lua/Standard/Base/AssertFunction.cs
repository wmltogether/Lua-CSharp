namespace Lua.Standard.Base;

public sealed class AssertFunction : LuaFunction
{
    public override string Name => "assert";
    public static readonly AssertFunction Instance = new();

    protected override ValueTask<int> InvokeAsyncCore(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken cancellationToken)
    {
        var arg0 = ReadArgument(context, 0);

        if (!arg0.ToBoolean())
        {
            var message = "assertion failed!";
            if (context.ArgumentCount >= 2)
            {
                message = ReadArgument<string>(context, 1);
            }

            throw new LuaAssertionException(context.State.GetTracebacks(), message);
        }

        return new(0);
    }
}
