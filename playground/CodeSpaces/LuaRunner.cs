using System.Text;
using Lua;
using Lua.Standard;

namespace Playground.CodeSpaces;

public class LuaRunner : ICodeRunnerContext
{
    private LuaState _state;
    public LuaRunner()
    {
        _state = LuaState.Create();
        _state.OpenStandardLibraries();
        _state.OpenExtensionLibraries();
        _state.Environment["print"] = new LuaFunction("print", Print);
    }
    
    public ConsoleOutputHandler? ConsoleOutput { get; set; }
    
    private async ValueTask<int> Print(LuaFunctionExecutionContext context, CancellationToken cancellationToken)
    {
        StringBuilder sb = new();
        for (int i = 0; i < context.ArgumentCount; i++)
        {
            var useSep = context.ArgumentCount > 1 && i != context.ArgumentCount - 1;
            var arg = context.Arguments[i].ToString();
            sb.Append(arg);
            if (useSep) sb.Append('\t');
        }
        sb.Append('\n');
        ConsoleOutput?.Invoke(sb.ToString());
        return await Task.FromResult(context.Return());
    }

    public void Reset()
    {
        if (_scriptCancellationToken is not null && !_scriptCancellationToken.IsCancellationRequested)
        {
            _scriptCancellationToken.Cancel();
        }
        _state.Environment.Clear();
    }
    
    private CancellationTokenSource? _scriptCancellationToken;

    public async Task RunString(string script, string stdin, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(script)) return;
        _scriptCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var chunkName = "main.lua";
        await _state.DoStringAsync(script, chunkName, cancellationToken: cancellationToken);
    }
    
    
}