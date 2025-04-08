namespace Lua.Runtime;

public sealed class CsClosure(string name,LuaValue[] upValues,Func<LuaFunctionExecutionContext, CancellationToken, ValueTask<int>> func) : LuaFunction(name, func)
{
   public readonly LuaValue[] UpValues = upValues;
}