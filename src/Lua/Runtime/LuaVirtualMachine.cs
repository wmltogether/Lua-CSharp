using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Lua.Internal;

namespace Lua.Runtime;

[SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly")]
public static partial class LuaVirtualMachine
{
    [StructLayout(LayoutKind.Auto)]
    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    struct VirtualMachineExecutionContext(
        LuaState state,
        LuaStack stack,
        LuaThread thread,
        in CallStackFrame frame,
        CancellationToken cancellationToken)
    {
        public readonly LuaState State = state;
        public readonly LuaStack Stack = stack;
        public Closure Closure = (Closure)frame.Function;
        public readonly LuaThread Thread = thread;
        public Chunk Chunk => Closure.Proto;
        public int FrameBase = frame.Base;
        public int VariableArgumentCount = frame.VariableArgumentCount;
        public readonly CancellationToken CancellationToken = cancellationToken;
        public int Pc = -1;
        public Instruction Instruction;
        public int CurrentReturnFrameBase = frame.ReturnBase;
        public ValueTask<int> Task;
        public int LastHookPc = -1;
        public bool IsTopLevel => BaseCallStackCount == Thread.CallStack.Count;

        public readonly int BaseCallStackCount = thread.CallStack.Count;

        public PostOperationType PostOperation;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Pop(Instruction instruction, int frameBase)
        {
            var count = instruction.B - 1;
            var src = instruction.A + frameBase;
            if (count == -1) count = Stack.Count - src;
            return PopFromBuffer(src, count);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool PopFromBuffer(int src, int srcCount)
        {
            var result = Stack.GetBuffer().Slice(src, srcCount);
            ref var callStack = ref Thread.CallStack;
        Re:
            var frames = callStack.AsSpan();
            if (frames.Length == BaseCallStackCount)
            {
                var returnBase = frames[^1].ReturnBase;
                if (src != returnBase)
                {
                    result.CopyTo(Stack.GetBuffer()[returnBase..]);
                }

                Stack.PopUntil(returnBase + srcCount);
                return false;
            }

            ref readonly var frame = ref frames[^1];
            Pc = frame.CallerInstructionIndex;
            Thread.LastPc = Pc;
            ref readonly var lastFrame = ref frames[^2];
            Closure = Unsafe.As<Closure>(lastFrame.Function);
            CurrentReturnFrameBase = frame.ReturnBase;
            var callInstruction = Chunk.Instructions[Pc];
            if (callInstruction.OpCode == OpCode.TailCall)
            {
                Thread.PopCallStackFrame();
                goto Re;
            }


            FrameBase = lastFrame.Base;
            VariableArgumentCount = lastFrame.VariableArgumentCount;

            var opCode = callInstruction.OpCode;
            if (opCode is OpCode.Eq or OpCode.Lt or OpCode.Le)
            {
                var compareResult = srcCount > 0 && result[0].ToBoolean();
                if ((frame.Flags & CallStackFrameFlags.ReversedLe) != 0)
                {
                    compareResult = !compareResult;
                }

                if (compareResult != (callInstruction.A == 1))
                {
                    Pc++;
                }

                Thread.PopCallStackFrameWithStackPop();
                return true;
            }

            var target = callInstruction.A + FrameBase;
            var targetCount = result.Length;
            switch (opCode)
            {
                case OpCode.Call:
                    {
                        var c = callInstruction.C;
                        if (c != 0)
                        {
                            targetCount = c - 1;
                        }

                        break;
                    }
                case OpCode.TForCall:
                    target += 3;
                    targetCount = callInstruction.C;
                    break;
                case OpCode.Self:
                    Stack.Get(target) = result.Length == 0 ? LuaValue.Nil : result[0];
                    Thread.PopCallStackFrameWithStackPop(target + 2);
                    return true;
                case OpCode.SetTable or OpCode.SetTabUp:
                    targetCount = 0;
                    break;
                // Other opcodes has one result
                default:
                    Stack.Get(target) = result.Length == 0 ? LuaValue.Nil : result[0];
                    Thread.PopCallStackFrameWithStackPop(target + 1);
                    return true;
            }

            Stack.EnsureCapacity(target + targetCount);
            if (0 < targetCount && src != target)
            {
                if (targetCount < result.Length)
                {
                    result = result.Slice(0, targetCount);
                }

                result.CopyTo(Stack.GetBuffer().Slice(target, targetCount));
            }

            Stack.PopUntil(target + Math.Min(targetCount, srcCount));
            Stack.NotifyTop(target + targetCount);
            Thread.PopCallStackFrame();
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(in CallStackFrame frame)
        {
            Pc = -1;
            Closure = (Closure)(frame.Function);
            FrameBase = frame.Base;
            CurrentReturnFrameBase = frame.ReturnBase;
            VariableArgumentCount = frame.VariableArgumentCount;
        }

        public void PopOnTopCallStackFrames()
        {
            ref var callStack = ref Thread.CallStack;
            var count = callStack.Count;
            if (count == BaseCallStackCount) return;
            while (callStack.Count > BaseCallStackCount + 1)
            {
                callStack.TryPop();
            }

            Thread.PopCallStackFrame();
        }

        bool ExecutePostOperation(PostOperationType postOperation)
        {
            var stackCount = Stack.Count;
            var resultsSpan = Stack.GetBuffer()[CurrentReturnFrameBase..];
            switch (postOperation)
            {
                case PostOperationType.Nop: break;
                case PostOperationType.SetResult:
                    var RA = Instruction.A + FrameBase;
                    Stack.Get(RA) = stackCount > CurrentReturnFrameBase ? Stack.Get(CurrentReturnFrameBase) : LuaValue.Nil;
                    Stack.NotifyTop(RA + 1);
                    Stack.PopUntil(RA + 1);
                    break;
                case PostOperationType.TForCall:
                    TForCallPostOperation(ref this);
                    break;
                case PostOperationType.Call:
                    CallPostOperation(ref this);
                    break;
                case PostOperationType.TailCall:
                    if (!PopFromBuffer(CurrentReturnFrameBase, Stack.Count - CurrentReturnFrameBase))
                    {
                        return false;
                    }

                    break;
                case PostOperationType.Self:
                    SelfPostOperation(ref this, resultsSpan);
                    break;
                case PostOperationType.Compare:
                    ComparePostOperation(ref this, resultsSpan);
                    break;
            }

            return true;
        }

        public async ValueTask<int> ExecuteClosureAsyncImpl()
        {
            var returnFrameBase = CurrentReturnFrameBase;

            while (MoveNext(ref this))
            {
                await Task;
                Task = default;
                if (PostOperation != PostOperationType.TailCall)
                {
                    Thread.PopCallStackFrame();
                }

                if (!ExecutePostOperation(PostOperation))
                {
                    break;
                }
            }

            return Thread.Stack.Count - returnFrameBase;
        }
    }

    enum PostOperationType
    {
        None,
        Nop,
        SetResult,
        TForCall,
        Call,
        TailCall,
        Self,
        Compare,
    }

    internal static ValueTask<int> ExecuteClosureAsync(LuaState luaState, CancellationToken cancellationToken)
    {
        var thread = luaState.CurrentThread;
        ref readonly var frame = ref thread.GetCurrentFrame();

        var context = new VirtualMachineExecutionContext(luaState, thread.Stack, thread, in frame,
            cancellationToken);

        return context.ExecuteClosureAsyncImpl();
    }

    static bool MoveNext(ref VirtualMachineExecutionContext context)
    {
        try
        {
            // This is a label to restart the execution when new function is called or restarted
        Restart:
            ref var instructionsHead = ref context.Chunk.Instructions[0];
            var frameBase = context.FrameBase;
            var stack = context.Stack;
            stack.EnsureCapacity(frameBase + context.Chunk.MaxStackPosition);
            ref var constHead = ref MemoryMarshalEx.UnsafeElementAt(context.Chunk.Constants, 0);
            ref var lineAndCountHookMask = ref context.Thread.LineAndCountHookMask;
            goto Loop;
        LineHook:

            {
                context.LastHookPc = context.Pc;
                if (!context.Thread.IsInHook && ExecutePerInstructionHook(ref context))
                {
                    {
                        context.PostOperation = PostOperationType.Nop;
                        return true;
                    }
                }

                --context.Pc;
            }

        Loop:
            while (true)
            {
                var instruction = Unsafe.Add(ref instructionsHead, ++context.Pc);
                context.Instruction = instruction;
                if (lineAndCountHookMask.Value != 0 && (context.Pc != context.LastHookPc))
                {
                    goto LineHook;
                }

                context.LastHookPc = -1;
                var iA = instruction.A;
                var opCode = instruction.OpCode;
                switch (opCode)
                {
                    case OpCode.Move:
                        ref var stackHead = ref stack.FastGet(frameBase);
                        Unsafe.Add(ref stackHead, iA) = Unsafe.Add(ref stackHead, instruction.UIntB);
                        stack.NotifyTop(iA + frameBase + 1);
                        continue;
                    case OpCode.LoadK:
                        stack.GetWithNotifyTop(iA + frameBase) = Unsafe.Add(ref constHead, instruction.Bx);
                        continue;
                    case OpCode.LoadBool:
                        stack.GetWithNotifyTop(iA + frameBase) = instruction.B != 0;
                        if (instruction.C != 0) context.Pc++;
                        continue;
                    case OpCode.LoadNil:
                        var ra1 = iA + frameBase + 1;
                        var iB = instruction.B;
                        stack.GetBuffer().Slice(ra1 - 1, iB + 1).Clear();
                        stack.NotifyTop(ra1 + iB);
                        continue;
                    case OpCode.GetUpVal:
                        stack.GetWithNotifyTop(iA + frameBase) = context.Closure.GetUpValue(instruction.B);
                        continue;
                    case OpCode.GetTabUp:
                    case OpCode.GetTable:
                        stackHead = ref stack.FastGet(frameBase);
                        ref readonly var vc = ref RKC(ref stackHead, ref constHead, instruction);
                        ref readonly var vb = ref (instruction.OpCode == OpCode.GetTable ? ref Unsafe.Add(ref stackHead, instruction.UIntB) : ref context.Closure.GetUpValueRef(instruction.B));
                        var doRestart = false;
                        if (vb.TryReadTable(out var luaTable) && luaTable.TryGetValue(vc, out var resultValue) || GetTableValueSlowPath(vb, vc, ref context, out resultValue, out doRestart))
                        {
                            if (doRestart) goto Restart;
                            stack.GetWithNotifyTop(instruction.A + frameBase) = resultValue;
                            continue;
                        }

                        return true;
                    case OpCode.SetTabUp:
                    case OpCode.SetTable:
                        stackHead = ref stack.FastGet(frameBase);
                        vb = ref RKB(ref stackHead, ref constHead, instruction);
                        if (vb.TryReadNumber(out var numB))
                        {
                            if (double.IsNaN(numB))
                            {
                                ThrowLuaRuntimeException(ref context, "table index is NaN");
                                return true;
                            }
                        }

                        var table = opCode == OpCode.SetTabUp ? context.Closure.GetUpValue(iA) : Unsafe.Add(ref stackHead, iA);

                        if (table.TryReadTable(out luaTable))
                        {
                            ref var valueRef = ref luaTable.FindValue(vb);
                            if (!Unsafe.IsNullRef(ref valueRef) && valueRef.Type != LuaValueType.Nil)
                            {
                                valueRef = RKC(ref stackHead, ref constHead, instruction);
                                continue;
                            }
                        }

                        vc = ref RKC(ref stackHead, ref constHead, instruction);
                        if (SetTableValueSlowPath(table, vb, vc, ref context, out doRestart))
                        {
                            if (doRestart) goto Restart;
                            continue;
                        }

                        return true;
                    case OpCode.SetUpVal:
                        context.Closure.SetUpValue(instruction.B, stack.FastGet(iA + frameBase));
                        continue;
                    case OpCode.NewTable:

                        stack.GetWithNotifyTop(iA + frameBase) = new LuaTable(instruction.B, instruction.C);
                        continue;
                    case OpCode.Self:
                        stackHead = ref stack.FastGet(frameBase);
                        vc = ref RKC(ref stackHead, ref constHead, instruction);
                        table = Unsafe.Add(ref stackHead, instruction.UIntB);

                        doRestart = false;
                        if ((table.TryReadTable(out luaTable) && luaTable.TryGetValue(vc, out resultValue)) || GetTableValueSlowPath(table, vc, ref context, out resultValue, out doRestart))
                        {
                            if (doRestart) goto Restart;
                            Unsafe.Add(ref stackHead, iA) = resultValue;
                            Unsafe.Add(ref stackHead, iA + 1) = table;
                            stack.NotifyTop(iA + frameBase + 2);
                            continue;
                        }

                        return true;
                    case OpCode.Add:
                    case OpCode.Sub:
                    case OpCode.Mul:
                    case OpCode.Div:
                    case OpCode.Mod:
                    case OpCode.Pow:
                        stackHead = ref stack.FastGet(frameBase);
                        vb = ref RKB(ref stackHead, ref constHead, instruction);
                        vc = ref RKC(ref stackHead, ref constHead, instruction);

                        [MethodImpl(MethodImplOptions.NoInlining)]
                        static double Mod(double a, double b)
                        {
                            var mod = a % b;
                            if ((b > 0 && mod < 0) || (b < 0 && mod > 0))
                            {
                                mod += b;
                            }

                            return mod;
                        }

                        [MethodImpl(MethodImplOptions.AggressiveInlining)]
                        static double ArithmeticOperation(OpCode code, double a, double b)
                        {
                            return code switch
                            {
                                OpCode.Add => a + b,
                                OpCode.Sub => a - b,
                                OpCode.Mul => a * b,
                                OpCode.Div => a / b,
                                OpCode.Mod => Mod(a, b),
                                OpCode.Pow => Math.Pow(a, b),
                                _ => 0
                            };
                        }

                        if (vb.Type == LuaValueType.Number && vc.Type == LuaValueType.Number)
                        {
                            Unsafe.Add(ref stackHead, iA) = ArithmeticOperation(opCode, vb.UnsafeReadDouble(), vc.UnsafeReadDouble());
                            stack.NotifyTop(iA + frameBase + 1);
                            continue;
                        }

                        if (vb.TryReadDouble(out numB) && vc.TryReadDouble(out var numC))
                        {
                            Unsafe.Add(ref stackHead, iA) = ArithmeticOperation(opCode, numB, numC);
                            stack.NotifyTop(iA + frameBase + 1);
                            continue;
                        }

                        if (ExecuteBinaryOperationMetaMethod(vb, vc, ref context, opCode, out doRestart))
                        {
                            if (doRestart) goto Restart;
                            continue;
                        }

                        return true;
                    case OpCode.Unm:
                        stackHead = ref stack.FastGet(frameBase);
                        vb = ref Unsafe.Add(ref stackHead, instruction.UIntB);

                        if (vb.TryReadDouble(out numB))
                        {
                            ra1 = iA + frameBase + 1;
                            Unsafe.Add(ref stackHead, iA) = -numB;
                            stack.NotifyTop(ra1);
                            continue;
                        }

                        if (ExecuteUnaryOperationMetaMethod(vb, ref context, OpCode.Unm, out doRestart))
                        {
                            if (doRestart) goto Restart;
                            continue;
                        }

                        return true;
                    case OpCode.Not:
                        stackHead = ref stack.FastGet(frameBase);
                        Unsafe.Add(ref stackHead, iA) = !Unsafe.Add(ref stackHead, instruction.UIntB).ToBoolean();
                        stack.NotifyTop(iA + frameBase + 1);
                        continue;

                    case OpCode.Len:
                        stackHead = ref stack.FastGet(frameBase);
                        vb = ref Unsafe.Add(ref stackHead, instruction.UIntB);

                        if (vb.TryReadString(out var str))
                        {
                            ra1 = iA + frameBase + 1;
                            Unsafe.Add(ref stackHead, iA) = str.Length;
                            stack.NotifyTop(ra1);
                            continue;
                        }

                        if (ExecuteUnaryOperationMetaMethod(vb, ref context, OpCode.Len, out doRestart))
                        {
                            if (doRestart) goto Restart;
                            continue;
                        }

                        return true;
                    case OpCode.Concat:
                        if (Concat(ref context, out doRestart))
                        {
                            if (doRestart) goto Restart;
                            continue;
                        }

                        return true;
                    case OpCode.Jmp:
                        context.Pc += instruction.SBx;

                        if (iA != 0)
                        {
                            context.State.CloseUpValues(context.Thread, frameBase + iA - 1);
                        }

                        continue;
                    case OpCode.Eq:
                        stackHead = ref stack.Get(frameBase);
                        vb = ref RKB(ref stackHead, ref constHead, instruction);
                        vc = ref RKC(ref stackHead, ref constHead, instruction);
                        if (vb == vc)
                        {
                            if (iA != 1)
                            {
                                context.Pc++;
                            }

                            continue;
                        }

                        if (ExecuteCompareOperationMetaMethod(vb, vc, ref context, OpCode.Eq, out doRestart))
                        {
                            if (doRestart) goto Restart;
                            continue;
                        }

                        return true;
                    case OpCode.Lt:
                    case OpCode.Le:
                        stackHead = ref stack.Get(frameBase);
                        vb = ref RKB(ref stackHead, ref constHead, instruction);
                        vc = ref RKC(ref stackHead, ref constHead, instruction);

                        if (vb.TryReadNumber(out numB) && vc.TryReadNumber(out numC))
                        {
                            var compareResult = opCode == OpCode.Lt ? numB < numC : numB <= numC;
                            if (compareResult != (iA == 1))
                            {
                                context.Pc++;
                            }

                            continue;
                        }

                        if (vb.TryReadString(out var strB) && vc.TryReadString(out var strC))
                        {
                            var c = StringComparer.Ordinal.Compare(strB, strC);
                            var compareResult = opCode == OpCode.Lt ? c < 0 : c <= 0;
                            if (compareResult != (iA == 1))
                            {
                                context.Pc++;
                            }

                            continue;
                        }

                        if (ExecuteCompareOperationMetaMethod(vb, vc, ref context, opCode, out doRestart))
                        {
                            if (doRestart) goto Restart;
                            continue;
                        }

                        return true;
                    case OpCode.Test:
                        if (stack.Get(iA + frameBase).ToBoolean() != (instruction.C == 1))
                        {
                            context.Pc++;
                        }

                        continue;
                    case OpCode.TestSet:
                        vb = ref stack.Get(instruction.B + frameBase);
                        if (vb.ToBoolean() != (instruction.C == 1))
                        {
                            context.Pc++;
                        }
                        else
                        {
                            stack.GetWithNotifyTop(iA + frameBase) = vb;
                        }

                        continue;

                    case OpCode.Call:
                        if (Call(ref context, out doRestart))
                        {
                            if (doRestart)
                            {
                                goto Restart;
                            }

                            continue;
                        }

                        return true;
                    case OpCode.TailCall:
                        if (TailCall(ref context, out doRestart))
                        {
                            if (doRestart) goto Restart;
                            if (context.IsTopLevel) goto End;
                            continue;
                        }

                        return true;
                    case OpCode.Return:
                        context.State.CloseUpValues(context.Thread, frameBase);
                        if (context.Pop(instruction, frameBase))
                        {
                            goto Restart;
                        }

                        goto End;
                    case OpCode.ForLoop:
                        ref var indexRef = ref stack.Get(iA + frameBase);
                        var limit = Unsafe.Add(ref indexRef, 1).UnsafeReadDouble();
                        var step = Unsafe.Add(ref indexRef, 2).UnsafeReadDouble();
                        var index = indexRef.UnsafeReadDouble() + step;

                        if (step >= 0 ? index <= limit : limit <= index)
                        {
                            context.Pc += instruction.SBx;
                            indexRef = index;
                            Unsafe.Add(ref indexRef, 3) = index;
                            stack.NotifyTop(iA + frameBase + 4);
                            continue;
                        }

                        stack.NotifyTop(iA + frameBase + 1);
                        continue;
                    case OpCode.ForPrep:
                        indexRef = ref stack.Get(iA + frameBase);

                        if (!indexRef.TryReadDouble(out var init))
                        {
                            ThrowLuaRuntimeException(ref context, "'for' initial value must be a number");
                            return true;
                        }

                        if (!LuaValue.TryReadOrSetDouble(ref Unsafe.Add(ref indexRef, 1), out _))
                        {
                            ThrowLuaRuntimeException(ref context, "'for' limit must be a number");
                            return true;
                        }

                        if (!LuaValue.TryReadOrSetDouble(ref Unsafe.Add(ref indexRef, 2), out step))
                        {
                            ThrowLuaRuntimeException(ref context, "'for' step must be a number");
                            return true;
                        }

                        indexRef = init - step;
                        stack.NotifyTop(iA + frameBase + 1);
                        context.Pc += instruction.SBx;
                        continue;
                    case OpCode.TForCall:
                        if (TForCall(ref context, out doRestart))
                        {
                            if (doRestart) goto Restart;
                            continue;
                        }

                        return true;
                    case OpCode.TForLoop:
                        ref var forState = ref stack.Get(iA + frameBase + 1);

                        if (forState.Type is not LuaValueType.Nil)
                        {
                            Unsafe.Add(ref forState, -1) = forState;
                            context.Pc += instruction.SBx;
                        }

                        continue;
                    case OpCode.SetList:
                        SetList(ref context);
                        continue;
                    case OpCode.Closure:
                        ra1 = iA + frameBase + 1;
                        stack.EnsureCapacity(ra1);
                        stack.Get(ra1 - 1) = new Closure(context.State, context.Chunk.Functions[instruction.SBx]);
                        stack.NotifyTop(ra1);
                        continue;
                    case OpCode.VarArg:
                        VarArg(ref context);

                        static void VarArg(ref VirtualMachineExecutionContext context)
                        {
                            var instruction = context.Instruction;
                            var iA = instruction.A;
                            var frameBase = context.FrameBase;
                            var frameVariableArgumentCount = context.VariableArgumentCount;
                            var count = instruction.B == 0
                                ? frameVariableArgumentCount
                                : instruction.B - 1;
                            var ra = iA + frameBase;
                            var stack = context.Stack;
                            stack.EnsureCapacity(ra + count);
                            ref var stackHead = ref stack.Get(0);
                            for (int i = 0; i < count; i++)
                            {
                                Unsafe.Add(ref stackHead, ra + i) = frameVariableArgumentCount > i
                                    ? Unsafe.Add(ref stackHead, frameBase - (frameVariableArgumentCount - i))
                                    : default;
                            }

                            stack.NotifyTop(ra + count);
                        }

                        continue;
                    case OpCode.ExtraArg:
                    default:
                        ThrowLuaNotImplementedException(ref context, context.Instruction.OpCode);
                        return true;
                }
            }

        End:
            context.PostOperation = PostOperationType.None;
            return false;
        }
        catch (Exception e)
        {
            context.State.CloseUpValues(context.Thread, context.FrameBase);
            if (e is not LuaRuntimeException)
            {
                var newException = new LuaRuntimeException(context.State.GetTraceback(), e);
                context.PopOnTopCallStackFrames();
                context = default;
                throw newException;
            }

            context.PopOnTopCallStackFrames();
            throw;
        }
    }


    static void ThrowLuaRuntimeException(ref VirtualMachineExecutionContext context, string message)
    {
        throw new LuaRuntimeException(context.State.GetTraceback(), message);
    }

    static void ThrowLuaNotImplementedException(ref VirtualMachineExecutionContext context, OpCode opcode)
    {
        throw new LuaRuntimeException(context.State.GetTraceback(), $"OpCode {opcode} is not implemented");
    }


    static void SelfPostOperation(ref VirtualMachineExecutionContext context, Span<LuaValue> results)
    {
        var stack = context.Stack;
        var instruction = context.Instruction;
        var RA = instruction.A + context.FrameBase;
        var RB = instruction.B + context.FrameBase;
        ref var stackHead = ref stack.Get(0);
        var table = Unsafe.Add(ref stackHead, RB);
        Unsafe.Add(ref stackHead, RA + 1) = table;
        Unsafe.Add(ref stackHead, RA) = results.Length == 0 ? LuaValue.Nil : results[0];
        stack.NotifyTop(RA + 2);
    }

    static bool Concat(ref VirtualMachineExecutionContext context, out bool doRestart)
    {
        var instruction = context.Instruction;
        var stack = context.Stack;
        var RA = instruction.A + context.FrameBase;
        stack.EnsureCapacity(RA + 1);
        ref var stackHead = ref stack.Get(context.FrameBase);
        ref var constHead = ref MemoryMarshalEx.UnsafeElementAt(context.Chunk.Constants, 0);
        var vb = RKB(ref stackHead, ref constHead, instruction);
        var vc = RKC(ref stackHead, ref constHead, instruction);

        var bIsValid = vb.TryReadString(out var strB);
        var cIsValid = vc.TryReadString(out var strC);

        if (!bIsValid && vb.TryReadDouble(out var numB))
        {
            strB = numB.ToString(CultureInfo.InvariantCulture);
            bIsValid = true;
        }

        if (!cIsValid && vc.TryReadDouble(out var numC))
        {
            strC = numC.ToString(CultureInfo.InvariantCulture);
            cIsValid = true;
        }

        if (bIsValid && cIsValid)
        {
            stack.Get(RA) = strB + strC;
            stack.NotifyTop(RA + 1);
            doRestart = false;
            return true;
        }

        return ExecuteBinaryOperationMetaMethod(vb, vc, ref context, OpCode.Concat, out doRestart);
    }

    static bool Call(ref VirtualMachineExecutionContext context, out bool doRestart)
    {
        var instruction = context.Instruction;
        var RA = instruction.A + context.FrameBase;
        var va = context.Stack.Get(RA);
        var newBase = RA + 1;
        bool isMetamethod = false;
        if (!va.TryReadFunction(out var func))
        {
            if (va.TryGetMetamethod(context.State, Metamethods.Call, out var metamethod) &&
                metamethod.TryReadFunction(out func))
            {
                newBase -= 1;
                isMetamethod = true;
            }
            else
            {
                LuaRuntimeException.AttemptInvalidOperation(GetTracebacks(ref context), "call", va);
            }
        }

        var thread = context.Thread;
        var (argumentCount, variableArgumentCount) = PrepareForFunctionCall(thread, func, instruction, newBase, isMetamethod);newBase += variableArgumentCount;
        var newFrame = func.CreateNewFrame(ref context, newBase, RA, variableArgumentCount);

        thread.PushCallStackFrame(newFrame);
        if (thread.CallOrReturnHookMask.Value != 0 && !context.Thread.IsInHook)
        {
            context.PostOperation = PostOperationType.Call;
            context.Task = ExecuteCallHook(ref context, newFrame, argumentCount);
            doRestart = false;
            return false;
        }

        if (func is Closure)
        {
            context.Push(newFrame);
            doRestart = true;
            return true;
        }

        doRestart = false;
        return FuncCall(ref context, in newFrame, func, newBase, argumentCount);

        static bool FuncCall(ref VirtualMachineExecutionContext context, in CallStackFrame newFrame, LuaFunction func, int newBase, int argumentCount)
        {
            var task = func.Invoke(ref context, newFrame, argumentCount);

            if (!task.IsCompleted)
            {
                context.Task = task;
                return false;
            }

            var awaiter = task.GetAwaiter();

            awaiter.GetResult();
            var instruction = context.Instruction;
            var ic = instruction.C;

            if (ic != 0)
            {
                var resultCount = ic - 1;
                var stack = context.Stack;
                var top = instruction.A + context.FrameBase + resultCount;
                stack.EnsureCapacity(top);
                stack.PopUntil(top);
                stack.NotifyTop(top);
            }

            context.Thread.PopCallStackFrame();
            return true;
        }
    }

    static void CallPostOperation(ref VirtualMachineExecutionContext context)
    {
        var instruction = context.Instruction;
        var ic = instruction.C;

        if (ic != 0)
        {
            var resultCount = ic - 1;
            var stack = context.Stack;
            var top = instruction.A + context.FrameBase + resultCount;
            stack.EnsureCapacity(top);
            stack.PopUntil(top);
            stack.NotifyTop(top);
        }
    }

    static bool TailCall(ref VirtualMachineExecutionContext context, out bool doRestart)
    {
        var instruction = context.Instruction;
        var stack = context.Stack;
        var RA = instruction.A + context.FrameBase;
        var state = context.State;
        var thread = context.Thread;
        var newBase = RA + 1;
        bool isMetamethod = false;
        state.CloseUpValues(thread, context.FrameBase);

        var va = stack.Get(RA);
        if (!va.TryReadFunction(out var func))
        {
            if (va.TryGetMetamethod(state, Metamethods.Call, out var metamethod) &&
                metamethod.TryReadFunction(out func))
            {
                isMetamethod = true;
                newBase -= 1;
            }
            else
            {
                LuaRuntimeException.AttemptInvalidOperation(GetTracebacks(ref context), "call", metamethod);
            }
        }

        var (argumentCount, variableArgumentCount) = PrepareForFunctionTailCall(thread, func, instruction, newBase, isMetamethod);
        newBase = context.FrameBase + variableArgumentCount;
        var lastPc = thread.GetCurrentFrame().CallerInstructionIndex;
        context.Thread.PopCallStackFrame();
        var newFrame = func.CreateNewTailCallFrame(ref context, newBase, context.CurrentReturnFrameBase, variableArgumentCount);

        newFrame.CallerInstructionIndex = lastPc;
        thread.PushCallStackFrame(newFrame);

        if (thread.CallOrReturnHookMask.Value != 0 && !context.Thread.IsInHook)
        {
            context.PostOperation = PostOperationType.TailCall;
            context.Task = ExecuteCallHook(ref context, newFrame, argumentCount, true);
            doRestart = false;
            return false;
        }


        if (func is Closure)
        {
            context.Push(newFrame);
            doRestart = true;
            return true;
        }

        doRestart = false;
        var task = func.Invoke(ref context, newFrame, argumentCount);

        if (!task.IsCompleted)
        {
            context.PostOperation = PostOperationType.TailCall;
            context.Task = task;
            return false;
        }


        task.GetAwaiter().GetResult();
        if (!context.PopFromBuffer(context.CurrentReturnFrameBase, context.Stack.Count - context.CurrentReturnFrameBase))
        {
            return true;
        }

        doRestart = true;
        return true;
    }

    static bool TForCall(ref VirtualMachineExecutionContext context, out bool doRestart)
    {
        doRestart = false;
        var instruction = context.Instruction;
        var stack = context.Stack;
        var RA = instruction.A + context.FrameBase;

        var iteratorRaw = stack.Get(RA);
        if (!iteratorRaw.TryReadFunction(out var iterator))
        {
            LuaRuntimeException.AttemptInvalidOperation(GetTracebacks(ref context), "call", iteratorRaw);
        }

        var newBase = RA + 3 + instruction.C;
        stack.Get(newBase) = stack.Get(RA + 1);
        stack.Get(newBase + 1) = stack.Get(RA + 2);
        stack.NotifyTop(newBase + 2);
        var newFrame = iterator.CreateNewFrame(ref context, newBase, RA + 3, iterator.GetVariableArgumentCount(2));
        context.Thread.PushCallStackFrame(newFrame);
        if (context.Thread.CallOrReturnHookMask.Value != 0 && !context.Thread.IsInHook)
        {
            context.PostOperation = PostOperationType.TForCall;
            context.Task = ExecuteCallHook(ref context, newFrame, 2);
            doRestart = false;
            return false;
        }

        if (iterator is Closure)
        {
            context.Push(newFrame);
            doRestart = true;
            return true;
        }

        var task = iterator.Invoke(ref context, newFrame, 2);
        if (!task.IsCompleted)
        {
            context.PostOperation = PostOperationType.TForCall;
            context.Task = task;

            return false;
        }

        var awaiter = task.GetAwaiter();
        awaiter.GetResult();
        context.Thread.PopCallStackFrame();
        TForCallPostOperation(ref context);
        return true;
    }

    static void TForCallPostOperation(ref VirtualMachineExecutionContext context)
    {
        var stack = context.Stack;
        var instruction = context.Instruction;
        var RA = instruction.A + context.FrameBase;
        stack.SetTop(RA + instruction.C + 3);
    }

    static void SetList(ref VirtualMachineExecutionContext context)
    {
        var instruction = context.Instruction;
        var stack = context.Stack;
        var RA = instruction.A + context.FrameBase;

        if (!stack.Get(RA).TryReadTable(out var table))
        {
            throw new LuaException("internal error");
        }

        var count = instruction.B == 0
            ? stack.Count - (RA + 1)
            : instruction.B;

        table.EnsureArrayCapacity((instruction.C - 1) * 50 + count);
        stack.GetBuffer().Slice(RA + 1, count)
            .CopyTo(table.GetArraySpan()[((instruction.C - 1) * 50)..]);
        stack.PopUntil(RA + 1);
    }

    static void ComparePostOperation(ref VirtualMachineExecutionContext context, Span<LuaValue> results)
    {
        var compareResult = results.Length != 0 && results[0].ToBoolean();
        if (compareResult != (context.Instruction.A == 1))
        {
            context.Pc++;
        }

        results.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static ref readonly LuaValue RKB(ref LuaValue stack, ref LuaValue constants, Instruction instruction)
    {
        var index = instruction.UIntB;
        return ref (index >= 256 ? ref Unsafe.Add(ref constants, index - 256) : ref Unsafe.Add(ref stack, index));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static ref readonly LuaValue RKC(ref LuaValue stack, ref LuaValue constants, Instruction instruction)
    {
        var index = instruction.UIntC;
        return ref (index >= 256 ? ref Unsafe.Add(ref constants, index - 256) : ref Unsafe.Add(ref stack, index));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static bool GetTableValueSlowPath(LuaValue table, LuaValue key, ref VirtualMachineExecutionContext context, out LuaValue value, out bool doRestart)
    {
        var targetTable = table;
        const int MAX_LOOP = 100;
        doRestart = false;
        var skip = targetTable.Type == LuaValueType.Table;
        for (int i = 0; i < MAX_LOOP; i++)
        {
            if (table.TryReadTable(out var luaTable))
            {
                if (!skip && luaTable.TryGetValue(key, out value))
                {
                    return true;
                }

                skip = false;

                var metatable = luaTable.Metatable;
                if (metatable != null && metatable.TryGetValue(Metamethods.Index, out table))
                {
                    goto Function;
                }

                value = default;
                return true;
            }

            if (!table.TryGetMetamethod(context.State, Metamethods.Index, out var metatableValue))
            {
                LuaRuntimeException.AttemptInvalidOperation(GetTracebacks(ref context), "index", table);
            }

            table = metatableValue;
        Function:
            if (table.TryReadFunction(out var function))
            {
                return CallGetTableFunc(targetTable, function, key, ref context, out value, out doRestart);
            }
        }

        throw new LuaRuntimeException(GetTracebacks(ref context), "loop in gettable");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static bool CallGetTableFunc(LuaValue table, LuaFunction indexTable, LuaValue key, ref VirtualMachineExecutionContext context, out LuaValue result, out bool doRestart)
    {
        doRestart = false;
        var stack = context.Stack;
        stack.Push(table);
        stack.Push(key);
        var newFrame = indexTable.CreateNewFrame(ref context, stack.Count - 2);

        context.Thread.PushCallStackFrame(newFrame);
        if (context.Thread.CallOrReturnHookMask.Value != 0 && !context.Thread.IsInHook)
        {
            context.PostOperation = context.Instruction.OpCode == OpCode.GetTable ? PostOperationType.SetResult : PostOperationType.Self;
            context.Task = ExecuteCallHook(ref context, newFrame, 2);
            doRestart = false;
            result = default;
            return false;
        }

        if (indexTable is Closure)
        {
            context.Push(newFrame);
            doRestart = true;
            result = default;
            return true;
        }

        var task = indexTable.Invoke(ref context, newFrame, 2);

        if (!task.IsCompleted)
        {
            context.PostOperation = context.Instruction.OpCode == OpCode.GetTable ? PostOperationType.SetResult : PostOperationType.Self;
            context.Task = task;
            result = default;
            return false;
        }

        var awaiter = task.GetAwaiter();
        awaiter.GetResult();
        var results = stack.GetBuffer()[newFrame.Base..];
        result = results.Length == 0 ? default : results[0];
        context.Thread.PopCallStackFrameWithStackPop();
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static bool SetTableValueSlowPath(LuaValue table, LuaValue key, LuaValue value,
        ref VirtualMachineExecutionContext context, out bool doRestart)
    {
        var targetTable = table;
        const int MAX_LOOP = 100;
        doRestart = false;
        var skip = targetTable.Type == LuaValueType.Table;
        for (int i = 0; i < MAX_LOOP; i++)
        {
            if (table.TryReadTable(out var luaTable))
            {
                ref var valueRef = ref (skip ? ref Unsafe.NullRef<LuaValue>() : ref luaTable.FindValue(key));
                skip = false;
                if (!Unsafe.IsNullRef(ref valueRef) && valueRef.Type != LuaValueType.Nil)
                {
                    valueRef = value;
                    return true;
                }

                var metatable = luaTable.Metatable;
                if (metatable == null || !metatable.TryGetValue(Metamethods.NewIndex, out table))
                {
                    if (Unsafe.IsNullRef(ref valueRef))
                    {
                        luaTable[key] = value;
                        return true;
                    }

                    valueRef = value;
                    return true;
                }

                goto Function;
            }

            if (!table.TryGetMetamethod(context.State, Metamethods.NewIndex, out var metatableValue))
            {
                LuaRuntimeException.AttemptInvalidOperation(GetTracebacks(ref context), "index", table);
            }

            table = metatableValue;

        Function:
            if (table.TryReadFunction(out var function))
            {
                context.PostOperation = PostOperationType.Nop;
                return CallSetTableFunc(targetTable, function, key, value, ref context, out doRestart);
            }
        }

        throw new LuaRuntimeException(GetTracebacks(ref context), "loop in settable");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static bool CallSetTableFunc(LuaValue table, LuaFunction newIndexFunction, LuaValue key, LuaValue value, ref VirtualMachineExecutionContext context, out bool doRestart)
    {
        doRestart = false;
        var thread = context.Thread;
        var stack = thread.Stack;
        stack.Push(table);
        stack.Push(key);
        stack.Push(value);
        var newFrame = newIndexFunction.CreateNewFrame(ref context, stack.Count - 3);

        context.Thread.PushCallStackFrame(newFrame);
        if (context.Thread.CallOrReturnHookMask.Value != 0 && !context.Thread.IsInHook)
        {
            context.PostOperation = PostOperationType.Nop;
            context.Task = ExecuteCallHook(ref context, newFrame, 3);
            doRestart = false;
            return false;
        }

        if (newIndexFunction is Closure)
        {
            context.Push(newFrame);
            doRestart = true;
            return true;
        }

        var task = newIndexFunction.Invoke(ref context, newFrame, 3);
        if (!task.IsCompleted)
        {
            context.PostOperation = PostOperationType.Nop;
            context.Task = task;
            return false;
        }

        task.GetAwaiter().GetResult();
        thread.PopCallStackFrameWithStackPop();
        return true;
    }


    [MethodImpl(MethodImplOptions.NoInlining)]
    static bool ExecuteBinaryOperationMetaMethod(LuaValue vb, LuaValue vc,
        ref VirtualMachineExecutionContext context, OpCode opCode, out bool doRestart)
    {
        var (name, description) = opCode.GetNameAndDescription();
        doRestart = false;
        if (vb.TryGetMetamethod(context.State, name, out var metamethod) ||
            vc.TryGetMetamethod(context.State, name, out metamethod))
        {
            if (!metamethod.TryReadFunction(out var func))
            {
                LuaRuntimeException.AttemptInvalidOperation(GetTracebacks(ref context), "call", metamethod);
            }

            var stack = context.Stack;
            stack.Push(vb);
            stack.Push(vc);

            var newFrame = func.CreateNewFrame(ref context, stack.Count - 2, context.FrameBase + context.Instruction.A, func.GetVariableArgumentCount(2));

            context.Thread.PushCallStackFrame(newFrame);
            if (context.Thread.CallOrReturnHookMask.Value != 0 && !context.Thread.IsInHook)
            {
                context.PostOperation = PostOperationType.SetResult;
                context.Task = ExecuteCallHook(ref context, newFrame, 2);
                doRestart = false;
                return false;
            }

            if (func is Closure)
            {
                context.Push(newFrame);
                doRestart = true;
                return true;
            }


            var task = func.Invoke(ref context, newFrame, 2);

            if (!task.IsCompleted)
            {
                context.PostOperation = PostOperationType.SetResult;
                context.Task = task;
                return false;
            }

            task.GetAwaiter().GetResult();

            var RA = context.Instruction.A + context.FrameBase;

            var results = stack.GetBuffer()[newFrame.Base..];
            stack.Get(RA) = results.Length == 0 ? default : results[0];
            results.Clear();
            context.Thread.PopCallStackFrameWithStackPop();
            return true;
        }

        LuaRuntimeException.AttemptInvalidOperation(GetTracebacks(ref context), description, vb, vc);
        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static bool ExecuteUnaryOperationMetaMethod(LuaValue vb, ref VirtualMachineExecutionContext context,
        OpCode opCode, out bool doRestart)
    {
        var (name, description) = opCode.GetNameAndDescription();
        doRestart = false;
        var stack = context.Stack;
        if (vb.TryGetMetamethod(context.State, name, out var metamethod))
        {
            if (!metamethod.TryReadFunction(out var func))
            {
                LuaRuntimeException.AttemptInvalidOperation(GetTracebacks(ref context), "call", metamethod);
            }

            stack.Push(vb);
            var newFrame = func.CreateNewFrame(ref context, stack.Count - 1);

            context.Thread.PushCallStackFrame(newFrame);
            if (context.Thread.CallOrReturnHookMask.Value != 0 && !context.Thread.IsInHook)
            {
                context.PostOperation = PostOperationType.SetResult;
                context.Task = ExecuteCallHook(ref context, newFrame, 1);
                doRestart = false;
                return false;
            }

            if (func is Closure)
            {
                context.Push(newFrame);
                doRestart = true;
                return true;
            }


            var task = func.Invoke(ref context, newFrame, 1);

            if (!task.IsCompleted)
            {
                context.PostOperation = PostOperationType.SetResult;
                context.Task = task;
                return false;
            }

            var RA = context.Instruction.A + context.FrameBase;
            var results = stack.GetBuffer()[newFrame.Base..];
            stack.Get(RA) = results.Length == 0 ? default : results[0];
            results.Clear();
            context.Thread.PopCallStackFrameWithStackPop();
            return true;
        }

        if (opCode == OpCode.Len && vb.TryReadTable(out var table))
        {
            var RA = context.Instruction.A + context.FrameBase;
            stack.Get(RA) = table.ArrayLength;
            return true;
        }

        LuaRuntimeException.AttemptInvalidOperation(GetTracebacks(ref context), description, vb);
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static bool ExecuteCompareOperationMetaMethod(LuaValue vb, LuaValue vc,
        ref VirtualMachineExecutionContext context, OpCode opCode, out bool doRestart)
    {
        var (name, description) = opCode.GetNameAndDescription();
        doRestart = false;
        bool reverseLe = false;
    ReCheck:
        if (vb.TryGetMetamethod(context.State, name, out var metamethod) ||
            vc.TryGetMetamethod(context.State, name, out metamethod))
        {
            if (!metamethod.TryReadFunction(out var func))
            {
                LuaRuntimeException.AttemptInvalidOperation(GetTracebacks(ref context), "call", metamethod);
            }

            var stack = context.Stack;
            stack.Push(vb);
            stack.Push(vc);
            var newFrame = func.CreateNewFrame(ref context, stack.Count - 2);
            if (reverseLe) newFrame.Flags |= CallStackFrameFlags.ReversedLe;
            context.Thread.PushCallStackFrame(newFrame);
            if (context.Thread.CallOrReturnHookMask.Value != 0 && !context.Thread.IsInHook)
            {
                context.PostOperation = PostOperationType.Compare;
                context.Task = ExecuteCallHook(ref context, newFrame, 2);
                doRestart = false;
                return false;
            }

            if (func is Closure)
            {
                context.Push(newFrame);
                doRestart = true;
                return true;
            }

            var task = func.Invoke(ref context, newFrame, 2);

            if (!task.IsCompleted)
            {
                context.PostOperation = PostOperationType.Compare;
                context.Task = task;
                return false;
            }

            var results = stack.GetBuffer()[newFrame.Base..];
            var compareResult = results.Length == 0 && results[0].ToBoolean();
            compareResult = reverseLe ? !compareResult : compareResult;
            if (compareResult != (context.Instruction.A == 1))
            {
                context.Pc++;
            }

            results.Clear();
            context.Thread.PopCallStackFrameWithStackPop();

            return true;
        }

        if (opCode == OpCode.Le)
        {
            reverseLe = true;
            name = Metamethods.Lt;
            (vb, vc) = (vc, vb);
            goto ReCheck;
        }

        if (opCode != OpCode.Eq)
        {
            if (reverseLe)
            {
                (vb, vc) = (vc, vb);
            }

            LuaRuntimeException.AttemptInvalidOperation(GetTracebacks(ref context), description, vb, vc);
        }
        else
        {
            if (context.Instruction.A == 1)
            {
                context.Pc++;
            }
        }

        return true;
    }

    // If there are variable arguments, the base of the stack is moved by that number and the values of the variable arguments are placed in front of it.
    // see: https://wubingzheng.github.io/build-lua-in-rust/en/ch08-02.arguments.html
    [MethodImpl(MethodImplOptions.NoInlining)]
    static ( int ArgumentCount, int VariableArgumentCount) PrepareVariableArgument(LuaStack stack, int newBase, int argumentCount,
        int variableArgumentCount)
    {
        var temp = newBase;
        newBase += variableArgumentCount;

        stack.EnsureCapacity(newBase + argumentCount);
        stack.NotifyTop(newBase + argumentCount);

        var stackBuffer = stack.GetBuffer()[temp..];
        stackBuffer[..argumentCount].CopyTo(stackBuffer[variableArgumentCount..]);
        stackBuffer.Slice(argumentCount, variableArgumentCount).CopyTo(stackBuffer);
        return (argumentCount, variableArgumentCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static (int ArgumentCount, int VariableArgumentCount) PrepareForFunctionCall(LuaThread thread, LuaFunction function,
        Instruction instruction, int newBase, bool isMetaMethod)
    {
        var argumentCount = instruction.B - 1;
        if (argumentCount == -1)
        {
            argumentCount = (ushort)(thread.Stack.Count - newBase);
        }
        else
        {
            if (isMetaMethod)
            {
                argumentCount += 1;
            }

            thread.Stack.NotifyTop(newBase + argumentCount);
        }
        
        var variableArgumentCount = function.GetVariableArgumentCount(argumentCount);

        if (variableArgumentCount <= 0)
        {
            return (argumentCount, 0);
        }

        return PrepareVariableArgument(thread.Stack, newBase, argumentCount, variableArgumentCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static (int ArgumentCount, int VariableArgumentCount) PrepareForFunctionTailCall(LuaThread thread, LuaFunction function,
        Instruction instruction, int newBase, bool isMetaMethod)
    {
        var stack = thread.Stack;

        var argumentCount = instruction.B - 1;
        if (instruction.B == 0)
        {
            argumentCount = (ushort)(stack.Count - newBase);
        }
        else
        {
            if (isMetaMethod)
            {
                argumentCount += 1;
            }

            thread.Stack.NotifyTop(newBase + argumentCount);
        }

        // In the case of tailcall, the local variables of the caller are immediately discarded, so there is no need to retain them.
        // Therefore, a call can be made without allocating new registers.
        var currentBase = thread.GetCurrentFrame().Base;
        {
            var stackBuffer = stack.GetBuffer();
            if (argumentCount > 0)
                stackBuffer.Slice(newBase, argumentCount).CopyTo(stackBuffer.Slice(currentBase, argumentCount));
            newBase = currentBase;
        }

        var variableArgumentCount = function.GetVariableArgumentCount(argumentCount);

        if (variableArgumentCount <= 0)
        {
            return (argumentCount, 0);
        }

        return PrepareVariableArgument(thread.Stack, newBase, argumentCount, variableArgumentCount);
    }

    static Traceback GetTracebacks(ref VirtualMachineExecutionContext context)
    {
        return GetTracebacks(context.State, context.Pc);
    }

    static Traceback GetTracebacks(LuaState state, int pc)
    {
        var frame = state.CurrentThread.GetCurrentFrame();
        state.CurrentThread.PushCallStackFrame(frame with
        {
            CallerInstructionIndex = pc
        });
        var tracebacks = state.GetTraceback();
        state.CurrentThread.PopCallStackFrameWithStackPop();
        return tracebacks;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static CallStackFrame CreateNewFrame(this LuaFunction function, ref VirtualMachineExecutionContext context, int newBase)
    {
        return new()
        {
            Base = newBase,
            ReturnBase = newBase,
            Function = function,
            VariableArgumentCount = 0,
            CallerInstructionIndex = context.Pc,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static CallStackFrame CreateNewFrame(this LuaFunction function, ref VirtualMachineExecutionContext context, int newBase, int returnBase, int variableArgumentCount)
    {
        return new()
        {
            Base = newBase,
            ReturnBase = returnBase,
            Function = function,
            VariableArgumentCount = variableArgumentCount,
            CallerInstructionIndex = context.Pc,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static CallStackFrame CreateNewTailCallFrame(this LuaFunction function, ref VirtualMachineExecutionContext context, int newBase, int returnBase, int variableArgumentCount)
    {
        return new()
        {
            Base = newBase,
            ReturnBase = returnBase,
            Function = function,
            VariableArgumentCount = variableArgumentCount,
            CallerInstructionIndex = context.Pc,
            Flags = CallStackFrameFlags.TailCall
        };
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static ValueTask<int> Invoke(this LuaFunction function, ref VirtualMachineExecutionContext context, in CallStackFrame frame, int arguments)
    {
        return function.Func(new()
        {
            State = context.State,
            Thread = context.Thread,
            ArgumentCount = arguments,
            FrameBase = frame.Base,
            ReturnFrameBase = frame.ReturnBase,
            CallerInstructionIndex = frame.CallerInstructionIndex,
        }, context.CancellationToken);
    }
}