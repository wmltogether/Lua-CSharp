namespace Lua.Runtime;

public static class Metamethods
{
    public const string Metatable = "__metatable";
    public const string Index = "__index";
    public const string NewIndex = "__newindex";
    public const string Add = "__add";
    public const string Sub = "__sub";
    public const string Mul = "__mul";
    public const string Div = "__div";
    public const string Mod = "__mod";
    public const string Pow = "__pow";
    public const string Unm = "__unm";
    public const string Len = "__len";
    public const string Eq = "__eq";
    public const string Lt = "__lt";
    public const string Le = "__le";
    public const string Call = "__call";
    public const string Concat = "__concat";
    public const string Pairs = "__pairs";
    public const string IPairs = "__ipairs";
    public new const string ToString = "__tostring";

    private static readonly (string, string) IndexDsc = (Index, "index");
    private static readonly (string, string) NewIndexDsc = (NewIndex, "new index");
    private static readonly (string, string) AddDsc = (Add, "add");
    private static readonly (string, string) SubDsc = (Sub, "sub");
    private static readonly (string, string) MulDsc = (Mul, "mul");
    private static readonly (string, string) DivDsc = (Div, "div");
    private static readonly (string, string) ModDsc = (Mod, "mod");
    private static readonly (string, string) PowDsc = (Pow, "pow");
    private static readonly (string, string) UnmDsc = (Unm, "unm");
    private static readonly (string, string) LenDsc = (Len, "get length of");
    private static readonly (string, string) EqDsc = (Eq, "eq");
    private static readonly (string, string) LtDsc = (Lt, "lt");
    private static readonly (string, string) LeDsc = (Le, "le");
    private static readonly (string, string) CallDsc = (Call, "call");
    private static readonly (string, string) ConcatDsc = (Concat, "concat");
    
    internal static (string Name, string Description) GetNameAndDescription(this OpCode opCode)
    {
        return opCode switch
        {
            OpCode.GetTabUp or OpCode.GetTable or OpCode.Self => IndexDsc,
            OpCode.SetTabUp or OpCode.SetTable => NewIndexDsc,
            OpCode.Add => AddDsc,
            OpCode.Sub => SubDsc,
            OpCode.Mul => MulDsc,
            OpCode.Div => DivDsc,
            OpCode.Mod => ModDsc,
            OpCode.Pow => PowDsc,
            OpCode.Unm => UnmDsc,
            OpCode.Len => LenDsc,
            OpCode.Eq => EqDsc,
            OpCode.Lt => LtDsc,
            OpCode.Le => LeDsc,
            OpCode.Call => CallDsc,
            OpCode.Concat => ConcatDsc,
            _ => (opCode.ToString(), opCode.ToString())
        };
    }
}