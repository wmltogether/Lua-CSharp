namespace Lua;

public class LuaPlatformUtility
{
    public static bool IsSandBox => _supportFileModuleTryLazy.Value;
    
    private static Lazy<bool> _supportFileModuleTryLazy = new Lazy<bool>(() =>
    {
        try
        {
            _ = Console.OpenStandardOutput();
            return true;
        }

        catch (Exception)
        {
            return false;
        }
    });

}