namespace Raze;

public class InlinedCodeGen : CodeGen
{
    public class InlineStateNoInline(InlineStateNoInline lastState, bool inline = false)
    {
        public InlineStateNoInline lastState = lastState;

        public bool inline = inline;
    }

    public class InlineStateInlined(InlineStateNoInline lastState, Expr.Function currentInlined)
        : InlineStateNoInline(lastState, true)
    {
        public int inlineLabelIdx = -1;
        public bool secondJump = false;
        public Expr.Function currentInlined = currentInlined;
        public AssemblyExpr.IValue? callee;
    }

    public InlineStateNoInline inlineState = new(null);

    public InlinedCodeGen()
    {
    }

    public override AssemblyExpr.IValue? VisitUnaryExpr(Expr.Unary expr)
    {
        if (expr.internalFunction == null)
        {
            return Analyzer.Primitives.Operation(expr.op, (AssemblyExpr.UnresolvedLiteral)expr.operand.Accept(this), this);
        }

        if (!expr.internalFunction.modifiers["inline"])
        {
            inlineState = new(inlineState);

            var tmp = base.VisitUnaryExpr(expr);

            inlineState = inlineState.lastState;

            return tmp;
        }

        inlineState = new InlineStateInlined(inlineState, expr.internalFunction);

        var ret = HandleInvokable(expr);

        inlineState = inlineState.lastState;

        return ret;
    }

    public override AssemblyExpr.IValue? VisitBinaryExpr(Expr.Binary expr)
    {
        if (expr.internalFunction == null)
        {
            return Analyzer.Primitives.Operation(expr.op, (AssemblyExpr.UnresolvedLiteral)expr.left.Accept(this), (AssemblyExpr.UnresolvedLiteral)expr.right.Accept(this), this);
        }

        if (!expr.internalFunction.modifiers["inline"])
        {
            inlineState = new(inlineState);

            var tmp = base.VisitBinaryExpr(expr);

            inlineState = inlineState.lastState;

            return tmp;
        }

        inlineState = new InlineStateInlined(inlineState, expr.internalFunction);

        var ret = HandleInvokable(expr);

        inlineState = inlineState.lastState;

        return ret;
    }

    public override AssemblyExpr.IValue? VisitCallExpr(Expr.Call expr)
    {
        if (!expr.internalFunction.modifiers["inline"])
        {
            inlineState = new(inlineState);

            var tmp = base.VisitCallExpr(expr);

            inlineState = inlineState.lastState;

            return tmp;
        }

        inlineState = new InlineStateInlined(inlineState, expr.internalFunction);

        AssemblyExpr.IValue? instanceArg = null;

        if (!expr.internalFunction.modifiers["static"])
        {
            if (!expr.constructor)
            {
                if (expr.callee != null)
                {
                    instanceArg = expr.callee.Accept(this);
                }
                else
                {
                    var enclosing = SymbolTableSingleton.SymbolTable.NearestEnclosingClass(expr.internalFunction);
                    var size = enclosing.allocSize;
                    instanceArg = new AssemblyExpr.Pointer(8, size);
                }
            }
            else
            {
                instanceArg = alloc.CurrentRegister(InstructionUtils.SYS_SIZE);
                Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, instanceArg, new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RBX, InstructionUtils.SYS_SIZE)));
            }

            if (instanceArg.IsRegister(out var register))
            {
                alloc.Lock(register);
            }
        }

        var ret = HandleInvokable(expr);

        if (instanceArg != null)
        {
            alloc.Free(instanceArg, true);
        }

        inlineState = inlineState.lastState;

        return ret;
    }

    private AssemblyExpr.IValue? HandleInvokable(Expr.Invokable invokable)
    {
        if (invokable.internalFunction.Abstract)
        {
            return null;
        }

        alloc.CreateBlock();

        AssemblyExpr.IValue[] args = invokable.Arguments.Select(x => x.Accept(this)).ToArray();

        for (int i = 0; i < invokable.Arguments.Count; i++)
        {
            invokable.internalFunction.parameters[i].stack.inlinedData = true;
            invokable.internalFunction.parameters[i].stack.value = LockOperand(HandleParameterRegister(invokable.internalFunction.parameters[i], args[i]));
        }

        foreach (var bodyExpr in invokable.internalFunction.block.block)
        {
            alloc.Free(bodyExpr.Accept(this), false);
        }

        var ret = ((InlineStateInlined)inlineState).callee;
        RegisterState? state = alloc.SaveRegisterState(ret);

        for (int i = 0; i < invokable.Arguments.Count; i++)
        {
            invokable.internalFunction.parameters[i].stack.inlinedData = false;
            alloc.Free(invokable.internalFunction.parameters[i].stack.value, true);
        }

        alloc.SetRegisterState(state, ret);

        UnlockOperand(ret);

        HandleInlinedReturnOptimization();

        alloc.RemoveBlock();
        return HandleRefVariableDeref(invokable.internalFunction.refReturn, ret);
    }

    public override AssemblyExpr.IValue? VisitAssignExpr(Expr.Assign expr)
    {
        if (!expr.binary || !((Expr.Binary)expr.value).internalFunction.modifiers["inline"])
        {
            return base.VisitAssignExpr(expr);
        }

        AssemblyExpr.IValue operand2;

        if (((Expr.Binary)expr.value).internalFunction.parameters[0].modifiers["ref"] == false)
        {
            ((Expr.Binary)expr.value).internalFunction.parameters[0].modifiers["ref"] = true;
            operand2 = expr.value.Accept(this);
            ((Expr.Binary)expr.value).internalFunction.parameters[0].modifiers["ref"] = false;
        }
        else
        {
            operand2 = expr.value.Accept(this);
        }

        if (((Expr.Binary)expr.value).internalFunction.parameters[0].stack.value != operand2)
        {
            Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, ((Expr.Binary)expr.value).internalFunction.parameters[0].stack.value, operand2));
        }

        alloc.Free(operand2);

        return null;
    }

    public override AssemblyExpr.IValue? VisitReturnExpr(Expr.Return expr)
    {
        if (!inlineState.inline)
        {
            return base.VisitReturnExpr(expr);
        }

        if (!expr.IsVoid(((InlineStateInlined)inlineState).currentInlined))
        {
            AssemblyExpr.Instruction instruction = alloc.current is Expr.Function function ?
                GetMoveInstruction(function.refReturn) :
                AssemblyExpr.Instruction.MOV;

            var returnSize = InstructionUtils.ToRegisterSize(((InlineStateInlined)inlineState).currentInlined._returnType.type.allocSize);
            AssemblyExpr.IValue operand = expr.value.Accept(this).IfLiteralCreateLiteral(returnSize);

            if (((InlineStateInlined)inlineState).currentInlined.refReturn)
            {
                (instruction, operand) = PreserveRefPtrVariable(expr.value, (AssemblyExpr.Pointer)operand);
            }

            if (((InlineStateInlined)inlineState).callee == null)
            {
                alloc.Free(operand);
                ((InlineStateInlined)inlineState).callee = alloc.NextRegister(returnSize);

            }
            if (operand.IsRegister(out var op))
            {
                if (op.Name != ((AssemblyExpr.Register)((InlineStateInlined)inlineState).callee).Name)
                {
                    if (!(HandleSeteOptimization((AssemblyExpr.Register)operand, ((InlineStateInlined)inlineState).callee)))
                    {
                        Emit(new AssemblyExpr.Binary(instruction, new AssemblyExpr.Register(((AssemblyExpr.Register)((InlineStateInlined)inlineState).callee).Name, op.Size), operand));
                    }
                }
            }
            else
            {
                Emit(new AssemblyExpr.Binary(instruction, ((InlineStateInlined)inlineState).callee, operand));
            }
        }
        else
        {
            Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, ((InlineStateInlined)inlineState).callee, new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, new byte[] { 0 })));
        }

        if (((InlineStateInlined)inlineState).inlineLabelIdx == -1)
        {
            ((InlineStateInlined)inlineState).inlineLabelIdx = conditionalCount;
            Emit(new AssemblyExpr.Unary(AssemblyExpr.Instruction.JMP, new AssemblyExpr.LocalProcedureRef(CreateConditionalLabel(conditionalCount++))));
        }
        else
        {
            ((InlineStateInlined)inlineState).secondJump = true;
            Emit(new AssemblyExpr.Unary(AssemblyExpr.Instruction.JMP, new AssemblyExpr.LocalProcedureRef(CreateConditionalLabel(((InlineStateInlined)inlineState).inlineLabelIdx))));
        }

        return null;
    }

    public AssemblyExpr.IValue LockOperand(AssemblyExpr.IValue operand)
    {
        if (operand.IsRegister(out var register))
        {
            alloc.Lock(register);
        }
        else if (operand.IsPointer(out var ptr))
        {
            var idx = alloc.NameToIdx(ptr.register.Name);
            if (idx != -1)
            {
                alloc.Lock(idx);
            }
        }
        return operand;
    }
    private void UnlockOperand(AssemblyExpr.IValue? operand)
    {
        if (operand == null)
        {
            return;
        }

        if (operand.IsRegister(out var register))
        {
            alloc.Unlock(register);
        }
        else if (operand.IsPointer(out var pointer))
        {
            var idx = alloc.NameToIdx(pointer.register.Name);
            if (idx != -1)
            {
                alloc.Unlock(idx);
            }
        }
    }

    private AssemblyExpr.IValue HandleParameterRegister(Expr.Parameter parameter, AssemblyExpr.IValue arg)
    {
        if (arg.IsLiteral(out var literal) && !parameter.modifiers["inlineRef"])
        {
            return ((AssemblyExpr.IValue)literal.CreateLiteral((AssemblyExpr.Register.RegisterSize)parameter.stack.size)).NonLiteral(this);
        }
        if (IsRefParameter(parameter))
        {
            return arg;
        }
        return arg.NonPointer(this);
    }
    private bool IsRefParameter(Expr.Parameter parameter)
    {
        return parameter.modifiers["ref"] || parameter.modifiers["inlineRef"];
    }

    private void HandleInlinedReturnOptimization()
    {
        if (assembly.text[^1] is AssemblyExpr.Unary jmpInstruction && jmpInstruction.instruction == AssemblyExpr.Instruction.JMP
            && jmpInstruction.operand is AssemblyExpr.LocalProcedureRef procRef && procRef.Name == CreateConditionalLabel(((InlineStateInlined)inlineState).inlineLabelIdx))
        {
            assembly.text.RemoveAt(assembly.text.Count - 1);

            if (((InlineStateInlined)inlineState).secondJump)
            {
                Emit(new AssemblyExpr.LocalProcedure(CreateConditionalLabel(((InlineStateInlined)inlineState).inlineLabelIdx)));
            }
            else
            {
                conditionalCount--;
            }
        }
        else if (((InlineStateInlined)inlineState).inlineLabelIdx != -1)
        {
            Emit(new AssemblyExpr.LocalProcedure(CreateConditionalLabel(((InlineStateInlined)inlineState).inlineLabelIdx)));
        }
    }
}
