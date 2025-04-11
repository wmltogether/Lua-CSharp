using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Lua.Runtime;

[StructLayout(LayoutKind.Explicit, Pack = 2)]
public struct Instruction : IEquatable<Instruction>
{
    [FieldOffset(0)] ulong _value;
    [FieldOffset(0)] OpCode opCode;
    [FieldOffset(1)] ushort a;
    [FieldOffset(3)] ushort b;
    [FieldOffset(5)] ushort c;
    [FieldOffset(3)] uint bx;

    public readonly ulong Value => _value;

    public OpCode OpCode
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get => opCode;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => opCode = value;
    }

    public ushort A
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get => a;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => a = value;
    }

    public ushort B
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get => b;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => b = value;
    }

    internal readonly uint UIntB
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => b;
    }

    public ushort C
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get => c;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => c = value;
    }

    internal readonly uint UIntC
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => c; //  
    }

    public uint Bx
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get => bx;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => bx = value;
    }

    public int SBx
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get => (int)(Bx - 131071); // signed 18 bits
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Bx = (uint)(value + 131071);
    }

    public ulong Ax
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get => (_value >> 8) & 0x_FFFF_FFFF_FFFF;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => this._value = (this._value & 0xFF) | ((value & 0x_FFFF_FFFF_FFFF) << 8);
    }

    public bool Equals(Instruction other)
    {
        return _value == other._value;
    }

    public override bool Equals(object? obj)
    {
        return (obj is Instruction instruction) && Equals(instruction);
    }

    public override int GetHashCode()
    {
        return _value.GetHashCode();
    }

    public override string ToString()
    {
        return OpCode switch
        {
            OpCode.Move => $"MOVE      {A} {B}",
            OpCode.LoadK => $"LOADK     {A} {Bx}",
            OpCode.LoadKX => $"LOADKX    {A}",
            OpCode.LoadBool => $"LOADBOOL  {A} {B} {C}",
            OpCode.LoadNil => $"LOADNIL   {A} {B}",
            OpCode.GetUpVal => $"GETUPVAL  {A} {B}",
            OpCode.GetTabUp => $"GETTABUP  {A} {B} {C}",
            OpCode.GetTable => $"GETTABLE  {A} {B} {C}",
            OpCode.SetTabUp => $"SETTABUP  {A} {B} {C}",
            OpCode.SetUpVal => $"SETUPVAL  {A} {B}",
            OpCode.SetTable => $"SETTABLE  {A} {B} {C}",
            OpCode.NewTable => $"NEWTABLE  {A} {B} {C}",
            OpCode.Self => $"SELF      {A} {B} {C}",
            OpCode.Add => $"ADD       {A} {B} {C}",
            OpCode.Sub => $"SUB       {A} {B} {C}",
            OpCode.Mul => $"MUL       {A} {B} {C}",
            OpCode.Div => $"DIV       {A} {B} {C}",
            OpCode.Mod => $"MOD       {A} {B} {C}",
            OpCode.Pow => $"POW       {A} {B} {C}",
            OpCode.Unm => $"UNM       {A} {B}",
            OpCode.Not => $"NOT       {A} {B}",
            OpCode.Len => $"LEN       {A} {B}",
            OpCode.Concat => $"CONCAT    {A} {B} {C}",
            OpCode.Jmp => $"JMP       {A} {SBx}",
            OpCode.Eq => $"EQ        {A} {B} {C}",
            OpCode.Lt => $"LT        {A} {B} {C}",
            OpCode.Le => $"LE        {A} {B} {C}",
            OpCode.Test => $"TEST      {A} {C}",
            OpCode.TestSet => $"TESTSET   {A} {B} {C}",
            OpCode.Call => $"CALL      {A} {B} {C}",
            OpCode.TailCall => $"TAILCALL  {A} {B} {C}",
            OpCode.Return => $"RETURN    {A} {B}",
            OpCode.ForLoop => $"FORLOOP   {A} {SBx}",
            OpCode.ForPrep => $"FORPREP   {A} {SBx}",
            OpCode.TForCall => $"TFORCALL  {A} {C}",
            OpCode.TForLoop => $"TFORLOOP  {A} {SBx}",
            OpCode.SetList => $"SETLIST   {A} {B} {C}",
            OpCode.Closure => $"CLOSURE   {A} {SBx}",
            OpCode.VarArg => $"VARARG    {A} {B}",
            OpCode.ExtraArg => $"EXTRAARG  {Ax}",
            _ => "",
        };
    }

    public static bool operator ==(Instruction left, Instruction right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Instruction left, Instruction right)
    {
        return !(left == right);
    }

    /// <summary>
    /// R(A) := R(B)
    /// </summary>
    public static Instruction Move(ushort a, ushort b)
    {
        return new()
        {
            OpCode = OpCode.Move,
            A = a,
            B = b,
        };
    }

    /// <summary>
    /// R(A) := Kst(Bx)
    /// </summary>
    public static Instruction LoadK(ushort a, uint bx)
    {
        return new()
        {
            OpCode = OpCode.LoadK,
            A = a,
            Bx = bx,
        };
    }

    /// <summary>
    /// R(A) := Kst(extra arg)
    /// </summary>
    public static Instruction LoadKX(ushort a)
    {
        return new()
        {
            OpCode = OpCode.LoadKX,
            A = a,
        };
    }

    /// <summary>
    /// <para>R(A) := (Bool)B</para>
    /// <para>if (C) pc++</para>
    /// </summary>
    public static Instruction LoadBool(ushort a, ushort b, ushort c)
    {
        return new()
        {
            OpCode = OpCode.LoadBool,
            A = a,
            B = b,
            C = c,
        };
    }

    /// <summary>
    /// R(A), R(A+1), ..., R(A+B) := nil
    /// </summary>
    public static Instruction LoadNil(ushort a, ushort b)
    {
        return new()
        {
            OpCode = OpCode.LoadNil,
            A = a,
            B = b,
        };
    }

    /// <summary>
    /// R(A) := UpValue[B]
    /// </summary>
    public static Instruction GetUpVal(ushort a, ushort b)
    {
        return new()
        {
            OpCode = OpCode.GetUpVal,
            A = a,
            B = b,
        };
    }

    /// <summary>
    /// R(A) := UpValue[B][RK(C)]
    /// </summary>
    public static Instruction GetTabUp(ushort a, ushort b, ushort c)
    {
        return new()
        {
            OpCode = OpCode.GetTabUp,
            A = a,
            B = b,
            C = c,
        };
    }

    /// <summary>
    /// R(A) := R(B)[RK(C)]
    /// </summary>
    public static Instruction GetTable(ushort a, ushort b, ushort c)
    {
        return new()
        {
            OpCode = OpCode.GetTable,
            A = a,
            B = b,
            C = c,
        };
    }

    /// <summary>
    /// UpValue[B] := R(A)
    /// </summary>
    public static Instruction SetUpVal(ushort a, ushort b)
    {
        return new()
        {
            OpCode = OpCode.SetUpVal,
            A = a,
            B = b,
        };
    }

    /// <summary>
    /// UpValue[A][RK(B)] := RK(C)
    /// </summary>
    public static Instruction SetTabUp(ushort a, ushort b, ushort c)
    {
        return new()
        {
            OpCode = OpCode.SetTabUp,
            A = a,
            B = b,
            C = c,
        };
    }

    /// <summary>
    /// R(A)[RK(B)] := RK(C)
    /// </summary>
    public static Instruction SetTable(ushort a, ushort b, ushort c)
    {
        return new()
        {
            OpCode = OpCode.SetTable,
            A = a,
            B = b,
            C = c,
        };
    }

    /// <summary>
    /// R(A) := {} (size = B,C)
    /// </summary>
    public static Instruction NewTable(ushort a, ushort b, ushort c)
    {
        return new()
        {
            OpCode = OpCode.NewTable,
            A = a,
            B = b,
            C = c,
        };
    }

    /// <summary>
    /// R(A+1) := R(B); R(A) := R(B)[RK(C)]
    /// </summary>
    public static Instruction Self(ushort a, ushort b, ushort c)
    {
        return new()
        {
            OpCode = OpCode.Self,
            A = a,
            B = b,
            C = c,
        };
    }

    /// <summary>
    /// R(A) := RK(B) + RK(C)
    /// </summary>
    public static Instruction Add(ushort a, ushort b, ushort c)
    {
        return new()
        {
            OpCode = OpCode.Add,
            A = a,
            B = b,
            C = c,
        };
    }

    /// <summary>
    /// R(A) := RK(B) - RK(C)
    /// </summary>
    public static Instruction Sub(ushort a, ushort b, ushort c)
    {
        return new()
        {
            OpCode = OpCode.Sub,
            A = a,
            B = b,
            C = c,
        };
    }

    /// <summary>
    /// R(A) := RK(B) * RK(C)
    /// </summary>
    public static Instruction Mul(ushort a, ushort b, ushort c)
    {
        return new()
        {
            OpCode = OpCode.Mul,
            A = a,
            B = b,
            C = c,
        };
    }

    /// <summary>
    /// R(A) := RK(B) / RK(C)
    /// </summary>
    public static Instruction Div(ushort a, ushort b, ushort c)
    {
        return new()
        {
            OpCode = OpCode.Div,
            A = a,
            B = b,
            C = c,
        };
    }

    /// <summary>
    /// R(A) := RK(B) % RK(C)
    /// </summary>
    public static Instruction Mod(ushort a, ushort b, ushort c)
    {
        return new()
        {
            OpCode = OpCode.Mod,
            A = a,
            B = b,
            C = c,
        };
    }

    /// <summary>
    /// R(A) := RK(B) ^ RK(C)
    /// </summary>
    public static Instruction Pow(ushort a, ushort b, ushort c)
    {
        return new()
        {
            OpCode = OpCode.Pow,
            A = a,
            B = b,
            C = c,
        };
    }

    /// <summary>
    /// R(A) := -R(B)
    /// </summary>
    public static Instruction Unm(ushort a, ushort b)
    {
        return new()
        {
            OpCode = OpCode.Unm,
            A = a,
            B = b,
        };
    }

    /// <summary>
    /// R(A) := not R(B)
    /// </summary>
    public static Instruction Not(ushort a, ushort b)
    {
        return new()
        {
            OpCode = OpCode.Not,
            A = a,
            B = b,
        };
    }

    /// <summary>
    /// R(A) := length of R(B)
    /// </summary>
    public static Instruction Len(ushort a, ushort b)
    {
        return new()
        {
            OpCode = OpCode.Len,
            A = a,
            B = b,
        };
    }

    /// <summary>
    /// R(A) := R(B).. ... ..R(C)
    /// </summary>
    public static Instruction Concat(ushort a, ushort b, ushort c)
    {
        return new()
        {
            OpCode = OpCode.Concat,
            A = a,
            B = b,
            C = c,
        };
    }

    /// <summary>
    /// <para>pc += sBx</para>
    /// <para>if (A) close all upvalues >= R(A - 1)</para>
    /// </summary>
    public static Instruction Jmp(ushort a, int sBx)
    {
        return new()
        {
            OpCode = OpCode.Jmp,
            A = a,
            SBx = sBx,
        };
    }

    /// <summary>
    /// if ((RK(B) == RK(C)) ~= A) then pc++
    /// </summary>
    public static Instruction Eq(ushort a, ushort b, ushort c)
    {
        return new()
        {
            OpCode = OpCode.Eq,
            A = a,
            B = b,
            C = c,
        };
    }

    /// <summary>
    /// if ((RK(B) &lt; RK(C)) ~= A) then pc++
    /// </summary>
    public static Instruction Lt(ushort a, ushort b, ushort c)
    {
        return new()
        {
            OpCode = OpCode.Lt,
            A = a,
            B = b,
            C = c,
        };
    }

    /// <summary>
    /// if ((RK(B) &lt;= RK(C)) ~= A) then pc++
    /// </summary>
    public static Instruction Le(ushort a, ushort b, ushort c)
    {
        return new()
        {
            OpCode = OpCode.Le,
            A = a,
            B = b,
            C = c,
        };
    }

    /// <summary>
    /// if not (R(A) &lt;=&gt; C) then pc++
    /// </summary>
    public static Instruction Test(ushort a, ushort c)
    {
        return new()
        {
            OpCode = OpCode.Test,
            A = a,
            C = c,
        };
    }

    /// <summary>
    /// if (R(B) &lt;=&gt; C) then R(A) := R(B) else pc++
    /// </summary>
    public static Instruction TestSet(ushort a, ushort b, ushort c)
    {
        return new()
        {
            OpCode = OpCode.TestSet,
            A = a,
            B = b,
            C = c,
        };
    }

    /// <summary>
    /// R(A), ... ,R(A+C-2) := R(A)(R(A+1), ... ,R(A+B-1))
    /// </summary>
    public static Instruction Call(ushort a, ushort b, ushort c)
    {
        return new()
        {
            OpCode = OpCode.Call,
            A = a,
            B = b,
            C = c,
        };
    }

    /// <summary>
    /// return R(A)(R(A+1), ... ,R(A+B-1))
    /// </summary>
    public static Instruction TailCall(ushort a, ushort b, ushort c)
    {
        return new()
        {
            OpCode = OpCode.TailCall,
            A = a,
            B = b,
            C = c,
        };
    }

    /// <summary>
    /// return R(A), ... ,R(A+B-2)
    /// </summary>
    public static Instruction Return(ushort a, ushort b)
    {
        return new()
        {
            OpCode = OpCode.Return,
            A = a,
            B = b,
        };
    }

    /// <summary>
    /// <para>R(A) += R(A+2);</para>
    /// <para>if R(A) &lt;?= R(A+1) then { pc += sBx; R(A+3) = R(A) }</para>
    /// </summary>
    public static Instruction ForLoop(ushort a, int sBx)
    {
        return new()
        {
            OpCode = OpCode.ForLoop,
            A = a,
            SBx = sBx,
        };
    }

    /// <summary>
    /// <para>R(A) -= R(A+2)</para>
    /// <para>pc += sBx</para>
    /// </summary>
    public static Instruction ForPrep(ushort a, int sBx)
    {
        return new()
        {
            OpCode = OpCode.ForPrep,
            A = a,
            SBx = sBx,
        };
    }

    /// <summary>
    /// R(A+3), ... ,R(A+2+C) := R(A)(R(A+1), R(A+2));
    /// </summary>
    public static Instruction TForCall(ushort a, ushort c)
    {
        return new()
        {
            OpCode = OpCode.TForCall,
            A = a,
            C = c,
        };
    }

    /// <summary>
    /// if R(A+1) ~= nil then { R(A) = R(A+1); pc += sBx }
    /// </summary>
    public static Instruction TForLoop(ushort a, int sBx)
    {
        return new()
        {
            OpCode = OpCode.TForLoop,
            A = a,
            SBx = sBx,
        };
    }

    /// <summary>
    /// R(A)[(C-1) * FPF + i] := R(A+i), 1 &lt;= i &lt;= B
    /// </summary>
    public static Instruction SetList(ushort a, ushort b, ushort c)
    {
        return new()
        {
            OpCode = OpCode.SetList,
            A = a,
            B = b,
            C = c,
        };
    }

    /// <summary>
    /// R(A) := closure(KPROTO[Bx])
    /// </summary>
    public static Instruction Closure(ushort a, int sBx)
    {
        return new()
        {
            OpCode = OpCode.Closure,
            A = a,
            SBx = sBx,
        };
    }

    /// <summary>
    /// R(A), R(A+1), ..., R(A+B-2) = vararg
    /// </summary>
    public static Instruction VarArg(ushort a, ushort b)
    {
        return new()
        {
            OpCode = OpCode.VarArg,
            A = a,
            B = b,
        };
    }

    /// <summary>
    /// extra (larger) argument for previous opcode
    /// </summary>
    public static Instruction ExtraArg(ulong ax)
    {
        return new()
        {
            OpCode = OpCode.ExtraArg,
            Ax = ax,
        };
    }
}