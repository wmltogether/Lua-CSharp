using System.Text;
using Lua.Internal;

namespace Lua.Standard;

public sealed class StringExLibrary
{
    public static readonly StringExLibrary Instance = new();

    public StringExLibrary()
    {
        Functions = [
            new("trim", Trim),
            new("trimStart", TrimStart),
            new("trimEnd", TrimEnd),
            new("lowerInvariant", LowerInvariant),
            new("upperInvariant", UpperInvariant),
            new("contains", Contains),
            new("startsWith", StartsWith),
            new("endsWith", EndsWith),
            new("equalsIgnoreCase", EqualsIgnoreCase),
        ];
    }

    public readonly LuaFunction[] Functions;

    public ValueTask<int> Trim(LuaFunctionExecutionContext context, CancellationToken cancellationToken)
    {
        var s = context.GetArgument<string>(0);
        var result  = s.Trim();
        return new(context.Return(result));
    }

    public ValueTask<int> TrimStart(LuaFunctionExecutionContext context, CancellationToken cancellationToken)
    {
        var s = context.GetArgument<string>(0);
        var result = s.TrimStart();
        return new(context.Return(result));
    }

    public ValueTask<int> TrimEnd(LuaFunctionExecutionContext context, CancellationToken cancellationToken)
    {
        var s = context.GetArgument<string>(0);
        var result = s.TrimEnd();
        return new(context.Return(result));
    }

    public ValueTask<int> LowerInvariant(LuaFunctionExecutionContext context, CancellationToken cancellationToken)
    {
        var s = context.GetArgument<string>(0);
        var result = s.ToLowerInvariant();
        return new(context.Return(result));
    }

    public ValueTask<int> UpperInvariant(LuaFunctionExecutionContext context, CancellationToken cancellationToken)
    {
        var s = context.GetArgument<string>(0);
        var result = s.ToUpperInvariant();
        return new(context.Return(result));
    }
    
    public ValueTask<int> Contains(LuaFunctionExecutionContext context, CancellationToken cancellationToken)
    {
        var s = context.GetArgument<string>(0);
        var s2 = context.GetArgument<string>(1);
        var result = s.Contains(s2);
        return new(context.Return(result));
    }
    
    public ValueTask<int> StartsWith(LuaFunctionExecutionContext context, CancellationToken cancellationToken)
    {
        var s = context.GetArgument<string>(0);
        var s2 = context.GetArgument<string>(1);
        var result = s.StartsWith(s2);
        return new(context.Return(result));
    }
    
    public ValueTask<int> EndsWith(LuaFunctionExecutionContext context, CancellationToken cancellationToken)
    {
        var s = context.GetArgument<string>(0);
        var s2 = context.GetArgument<string>(1);
        var result = s.EndsWith(s2);
        return new(context.Return(result));
    }

    public ValueTask<int> EqualsIgnoreCase(LuaFunctionExecutionContext context, CancellationToken cancellationToken)
    {
        var s = context.GetArgument<string>(0);
        var s2 = context.GetArgument<string>(1);
        var result = string.Equals(s, s2, StringComparison.OrdinalIgnoreCase);
        return new(context.Return(result));
    }
}