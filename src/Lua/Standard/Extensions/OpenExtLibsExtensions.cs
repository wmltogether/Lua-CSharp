namespace Lua.Standard;

public static class OpenExtLibsExtensions
{
    public static void OpenStringExLibrary(this LuaState state)
    {
        var stringex = new LuaTable(0, StringExLibrary.Instance.Functions.Length);
        foreach (var func in StringExLibrary.Instance.Functions)
        {
            stringex[func.Name] = func;
        }

        state.Environment["stringex"] = stringex;
        state.LoadedModules["stringex"] = stringex;
    }

    public static void OpenExtensionLibraries(this LuaState state)
    {
        state.OpenStringExLibrary();
    }
}