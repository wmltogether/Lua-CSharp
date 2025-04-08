namespace Lua.Standard;

public sealed class MathematicsLibrary
{
    public static readonly MathematicsLibrary Instance = new();
    public const string RandomInstanceKey = "__lua_mathematics_library_random_instance";

    public MathematicsLibrary()
    {
        Functions =
        [
            new("abs", Abs),
            new("acos", Acos),
            new("asin", Asin),
            new("atan2", Atan2),
            new("atan", Atan),
            new("ceil", Ceil),
            new("cos", Cos),
            new("cosh", Cosh),
            new("deg", Deg),
            new("exp", Exp),
            new("floor", Floor),
            new("fmod", Fmod),
            new("frexp", Frexp),
            new("ldexp", Ldexp),
            new("log", Log),
            new("max", Max),
            new("min", Min),
            new("modf", Modf),
            new("pow", Pow),
            new("rad", Rad),
            new("random", Random),
            new("randomseed", RandomSeed),
            new("sin", Sin),
            new("sinh", Sinh),
            new("sqrt", Sqrt),
            new("tan", Tan),
            new("tanh", Tanh),
        ];
    }

    public readonly LuaFunction[] Functions;

    public sealed class RandomUserData(Random random) : ILuaUserData
    {
        LuaTable? SharedMetatable;

        public LuaTable? Metatable
        {
            get => SharedMetatable;
            set => SharedMetatable = value;
        }

        public Random Random { get; } = random;
    }

    public ValueTask<int> Abs(LuaFunctionExecutionContext context,  CancellationToken cancellationToken)
    {
        var arg0 = context.GetArgument<double>(0);
        return new (context.Return(Math.Abs(arg0)));
    }

    public ValueTask<int> Acos(LuaFunctionExecutionContext context,  CancellationToken cancellationToken)
    {
        var arg0 = context.GetArgument<double>(0);
        return new (context.Return(Math.Acos(arg0)));
    }

    public ValueTask<int> Asin(LuaFunctionExecutionContext context,  CancellationToken cancellationToken)
    {
        var arg0 = context.GetArgument<double>(0);
        return new (context.Return(Math.Asin(arg0)));
    }

    public ValueTask<int> Atan2(LuaFunctionExecutionContext context,  CancellationToken cancellationToken)
    {
        var arg0 = context.GetArgument<double>(0);
        var arg1 = context.GetArgument<double>(1);

        return new (context.Return(Math.Atan2(arg0, arg1)));
    }

    public ValueTask<int> Atan(LuaFunctionExecutionContext context,  CancellationToken cancellationToken)
    {
        var arg0 = context.GetArgument<double>(0);
        return new (context.Return(Math.Atan(arg0)));
    }

    public ValueTask<int> Ceil(LuaFunctionExecutionContext context,  CancellationToken cancellationToken)
    {
        var arg0 = context.GetArgument<double>(0);
        return new (context.Return(Math.Ceiling(arg0)));
    }

    public ValueTask<int> Cos(LuaFunctionExecutionContext context,  CancellationToken cancellationToken)
    {
        var arg0 = context.GetArgument<double>(0);
        return new (context.Return(Math.Cos(arg0)));
    }

    public ValueTask<int> Cosh(LuaFunctionExecutionContext context,  CancellationToken cancellationToken)
    {
        var arg0 = context.GetArgument<double>(0);
        return new (context.Return(Math.Cosh(arg0)));
    }

    public ValueTask<int> Deg(LuaFunctionExecutionContext context,  CancellationToken cancellationToken)
    {
        var arg0 = context.GetArgument<double>(0);
        return new (context.Return(arg0 * (180.0 / Math.PI)));
    }

    public ValueTask<int> Exp(LuaFunctionExecutionContext context,  CancellationToken cancellationToken)
    {
        var arg0 = context.GetArgument<double>(0);
        return new (context.Return(Math.Exp(arg0)));
    }

    public ValueTask<int> Floor(LuaFunctionExecutionContext context,  CancellationToken cancellationToken)
    {
        var arg0 = context.GetArgument<double>(0);
        return new (context.Return(Math.Floor(arg0)));
    }

    public ValueTask<int> Fmod(LuaFunctionExecutionContext context,  CancellationToken cancellationToken)
    {
        var arg0 = context.GetArgument<double>(0);
        var arg1 = context.GetArgument<double>(1);
        return new (context.Return(arg0 % arg1));
    }

    public ValueTask<int> Frexp(LuaFunctionExecutionContext context,  CancellationToken cancellationToken)
    {
        var arg0 = context.GetArgument<double>(0);

        var (m, e) = MathEx.Frexp(arg0);
        return new (context.Return(m,e));
    }

    public ValueTask<int> Ldexp(LuaFunctionExecutionContext context,  CancellationToken cancellationToken)
    {
        var arg0 = context.GetArgument<double>(0);
        var arg1 = context.GetArgument<double>(1);

        return new (context.Return(arg0 * Math.Pow(2, arg1)));
    }

    public ValueTask<int> Log(LuaFunctionExecutionContext context,  CancellationToken cancellationToken)
    {
        var arg0 = context.GetArgument<double>(0);

        if (context.ArgumentCount == 1)
        {
            return new (context.Return(Math.Log(arg0)));
        }
        else
        {
            var arg1 = context.GetArgument<double>(1);
            return new (context.Return(Math.Log(arg0, arg1)));
        }
    }

    public ValueTask<int> Max(LuaFunctionExecutionContext context,  CancellationToken cancellationToken)
    {
        var x = context.GetArgument<double>(0);
        for (int i = 1; i < context.ArgumentCount; i++)
        {
            x = Math.Max(x, context.GetArgument<double>(i));
        }

        return new (context.Return(x));
    }

    public ValueTask<int> Min(LuaFunctionExecutionContext context,  CancellationToken cancellationToken)
    {
        var x = context.GetArgument<double>(0);
        for (int i = 1; i < context.ArgumentCount; i++)
        {
            x = Math.Min(x, context.GetArgument<double>(i));
        }

        return new (context.Return(x));
    }

    public ValueTask<int> Modf(LuaFunctionExecutionContext context,  CancellationToken cancellationToken)
    {
        var arg0 = context.GetArgument<double>(0);
        var (i, f) = MathEx.Modf(arg0);
        return new (context.Return(i,f));
    }

    public ValueTask<int> Pow(LuaFunctionExecutionContext context,  CancellationToken cancellationToken)
    {
        var arg0 = context.GetArgument<double>(0);
        var arg1 = context.GetArgument<double>(1);

        return new (context.Return(Math.Pow(arg0, arg1)));
    }

    public ValueTask<int> Rad(LuaFunctionExecutionContext context,  CancellationToken cancellationToken)
    {
        var arg0 = context.GetArgument<double>(0);
        return new (context.Return(arg0 * (Math.PI / 180.0)));
    }

    public ValueTask<int> Random(LuaFunctionExecutionContext context,  CancellationToken cancellationToken)
    {
        var rand = context.State.Environment[RandomInstanceKey].Read<RandomUserData>().Random;

        // When we call it without arguments, it returns a pseudo-random real number with uniform distribution in the interval [0,1
        if (context.ArgumentCount == 0)
        {
            return new (context.Return(rand.NextDouble()));
        }
        // When we call it with only one argument, an integer n, it returns an integer pseudo-random number such that 1 <= x <= n.
        // This is different from the C# random functions.
        // See: https://www.lua.org/pil/18.html
        else if (context.ArgumentCount == 1)
        {
            var arg0 = context.GetArgument<int>(0);
            if (arg0 < 0)
            {
                LuaRuntimeException.BadArgument(context.State.GetTraceback(), 0, "random");
            }
            return new (context.Return(rand.Next(1, arg0 + 1)));
        }
        else
        {
            var arg0 = context.GetArgument<int>(0);
            var arg1 = context.GetArgument<int>(1);
            if (arg0 < 1 || arg1 <= arg0)
            {
                LuaRuntimeException.BadArgument(context.State.GetTraceback(), 1, "random");
            }
            return new (context.Return(rand.Next(arg0, arg1 + 1)));
        }

    }

    public ValueTask<int> RandomSeed(LuaFunctionExecutionContext context,  CancellationToken cancellationToken)
    {
        var arg0 = context.GetArgument<double>(0);
        context.State.Environment[RandomInstanceKey] = new(new RandomUserData(new Random((int)BitConverter.DoubleToInt64Bits(arg0))));
        return new (context.Return());
    }

    public ValueTask<int> Sin(LuaFunctionExecutionContext context,  CancellationToken cancellationToken)
    {
        var arg0 = context.GetArgument<double>(0);
        return new (context.Return(Math.Sin(arg0)));
    }

    public ValueTask<int> Sinh(LuaFunctionExecutionContext context,  CancellationToken cancellationToken)
    {
        var arg0 = context.GetArgument<double>(0);
        return new (context.Return(Math.Sinh(arg0)));
    }

    public ValueTask<int> Sqrt(LuaFunctionExecutionContext context,  CancellationToken cancellationToken)
    {
        var arg0 = context.GetArgument<double>(0);
        return new (context.Return(Math.Sqrt(arg0)));
    }

    public ValueTask<int> Tan(LuaFunctionExecutionContext context, CancellationToken cancellationToken)
    {
        var arg0 = context.GetArgument<double>(0);
        return new (context.Return(Math.Tan(arg0)));
    }

    public ValueTask<int> Tanh(LuaFunctionExecutionContext context,  CancellationToken cancellationToken)
    {
        var arg0 = context.GetArgument<double>(0);
        return new (context.Return(Math.Tanh(arg0)));
    }
}