namespace Lua.Standard;

public class LuaConsole
{
    public static bool UseVirtualConsole => LuaPlatformUtility.IsSandBox;

    private static Stream _inputStream = new MemoryStream();
    private static Stream _outputStream = new MemoryStream();
    private static Stream _errorStream = new MemoryStream();

    public static Stream OpenStandardInput()
    {
        return UseVirtualConsole ? _inputStream : Console.OpenStandardInput();
    }
    
    public static Stream OpenStandardOutput()
    {
        return UseVirtualConsole ? _outputStream : Console.OpenStandardOutput();
    }
    
    public static Stream OpenStandardError()
    {
        return UseVirtualConsole ? _errorStream : Console.OpenStandardError();
    }
}