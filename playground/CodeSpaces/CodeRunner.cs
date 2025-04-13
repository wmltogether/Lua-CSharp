using System.Diagnostics;
using System.Text;
using Lua;
using Lua.Standard;

namespace Playground.CodeSpaces;

public delegate void ConsoleOutputHandler(string text);

public class CodeRunner
{
    public enum ScriptType
    {
        Lua,
    }

    public ConsoleOutputHandler? ConsoleOutput { get; set; }

    public CodeRunner()
    {
    }

    private SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    public void Reset()
    {
        var c = _currentContext.ToArray();
        foreach (var context in c)
        {
            context.Reset();
        }

        _currentContext.Clear();
    }

    private readonly List<ICodeRunnerContext> _currentContext = new List<ICodeRunnerContext>();

    public async Task RunAsync(string text, string stdin, ScriptType scriptType,
        CancellationToken cancellationToken = default)
    {
        var swatch = new Stopwatch();
        try
        {
            Reset();
            await _semaphore.WaitAsync(cancellationToken);
            swatch.Reset();
            swatch.Start();
            switch (scriptType)
            {
                case ScriptType.Lua:
                    var luaRunner = new LuaRunner();
                    luaRunner.ConsoleOutput = OnConsoleOutput;
                    _currentContext.Add(luaRunner);
                    WriteInput(stdin);
                    await luaRunner.RunString(text, stdin, cancellationToken);
                    break;
            }

            swatch.Stop();
            var elapsedTime = TimeSpan.FromMilliseconds(swatch.ElapsedMilliseconds);
            if (elapsedTime < TimeSpan.FromMilliseconds(1000))
            {
                OnConsoleOutput($"Elapsed Time: {elapsedTime.TotalMilliseconds}ms");
            }
            else
            {
                OnConsoleOutput($"Elapsed Time: {elapsedTime.TotalSeconds}s");
            }
        }
        catch (LuaParseException e)
        {
            OnConsoleOutput(e.Message);
        }
        catch (LuaException e)
        {
            OnConsoleOutput(e.Message);
        }
        catch (OperationCanceledException)
        {
        }
        catch (PlatformNotSupportedException)
        {
            OnConsoleOutput("Operation not support on this platform");
        }
        catch (Exception e)
        {
            OnConsoleOutput("Execute Error!");
            Console.WriteLine(e);
        }
        finally
        {
            _semaphore.Release();
            swatch.Stop();
        }
    }

    private void WriteInput(string? stdin)
    {
        if (string.IsNullOrEmpty(stdin)) return;
        UTF8Encoding encoding = new UTF8Encoding();
        ConsoleHelper.OpenStandardInput().Write(stdin?.Length > 0 ? encoding.GetBytes(stdin) : EMPTY1);
    }

    private static readonly byte[] EMPTY1 = new byte[1];

    private void OnConsoleOutput(string? value)
    {
        if (value == null) return;
        ConsoleOutput?.Invoke(value);
    }
}

public interface ICodeRunnerContext
{
    public Task RunString(string script, string stdin, CancellationToken cancellationToken = default);
    public void Reset();
}