namespace Raze;

public class InlinedCodeGen : CodeGen
{
    public class InlineStateNoInline
    {
        public InlineStateNoInline lastState;

        public bool inline;

        public InlineStateNoInline(InlineStateNoInline lastState, bool inline = false)
        {
            this.lastState = lastState;
            this.inline = inline;
        }
    }

    public class InlineStateInlined : InlineStateNoInline
    {
        public int inlineLabelIdx = -1;
        public bool secondJump = false;

        public AssemblyExpr.Value? callee;

        public InlineStateInlined(InlineStateNoInline lastState) : base(lastState, true)
        {
        }
    }

    public InlineStateNoInline inlineState = new(null);

    public InlinedCodeGen(List<Expr> expressions) : base(expressions)
    {
    }

    public override AssemblyExpr.Value? VisitUnaryExpr(Expr.Unary expr)
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

        inlineState = new InlineStateInlined(inlineState);

        var ret = HandleICall(expr);

        inlineState = inlineState.lastState;

        return ret;
    }

    public override AssemblyExpr.Value? VisitBinaryExpr(Expr.Binary expr)
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

        inlineState = new InlineStateInlined(inlineState);

        var ret = HandleICall(expr);

        inlineState = inlineState.lastState;

        return ret;
    }

    public override AssemblyExpr.Value? VisitCallExpr(Expr.Call expr)
    {
        if (!expr.internalFunction.modifiers["inline"])
        {
            inlineState = new(inlineState);

            var tmp = base.VisitCallExpr(expr);

            inlineState = inlineState.lastState;

            return tmp;
        }

        inlineState = new InlineStateInlined(inlineState);

        AssemblyExpr.Value? instanceArg = null;

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

            if (instanceArg.IsRegister())
            {
                alloc.Lock((AssemblyExpr.Register)instanceArg);
            }
        }

        var ret = HandleICall(expr);

        if (instanceArg != null)
        {
            alloc.Free(instanceArg, true);
        }

        inlineState = inlineState.lastState;

        return ret;
    }

    private AssemblyExpr.Value? HandleICall(Expr.ICall iCall)
    {
        if (iCall.InternalFunction.Abstract)
        {
            return null;
        }

        alloc.CreateBlock();

        AssemblyExpr.Value[] args = iCall.Arguments.Select(x => x.Accept(this)).ToArray();

        for (int i = 0; i < iCall.Arguments.Count; i++)
        {
            iCall.InternalFunction.parameters[i].stack.inlinedData = true;
            iCall.InternalFunction.parameters[i].stack.value = LockOperand(HandleParameterRegister(iCall.InternalFunction.parameters[i], args[i]));
        }

        foreach (var bodyExpr in iCall.InternalFunction.block.block)
        {
            alloc.Free(bodyExpr.Accept(this), false);
        }

        var ret = ((InlineStateInlined)inlineState).callee;
        RegisterState? state = alloc.SaveRegisterState(ret);

        for (int i = 0; i < iCall.Arguments.Count; i++)
        {
            iCall.InternalFunction.parameters[i].stack.inlinedData = false;
            alloc.Free(iCall.InternalFunction.parameters[i].stack.value, true);
        }

        alloc.SetRegisterState(state, ret);

        UnlockOperand(ret);

        HandleInlinedReturnOptimization();

        alloc.RemoveBlock();
        return ret;
    }

    public override AssemblyExpr.Value? VisitAssignExpr(Expr.Assign expr)
    {
        if (!expr.binary || !((Expr.Binary)expr.value).internalFunction.modifiers["inline"])
        {
            return base.VisitAssignExpr(expr);
        }

        AssemblyExpr.Value operand2;

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

    public override AssemblyExpr.Value? VisitReturnExpr(Expr.Return expr)
    {
        if (!inlineState.inline)
        {
            return base.VisitReturnExpr(expr);
        }

        if (!expr.IsVoid(alloc.current))
        {
            AssemblyExpr.Value operand = expr.value.Accept(this).IfLiteralCreateLiteral(InstructionUtils.ToRegisterSize(((Expr.Function)alloc.current)._returnType.type.allocSize));

            if (((InlineStateInlined)inlineState).callee == null)
            {
                ((InlineStateInlined)inlineState).callee = operand.NonLiteral(this);
            }
            else
            {
                if (operand.IsRegister())
                {
                    var op = (AssemblyExpr.Register)operand;
                    if (op.Name != ((AssemblyExpr.Register)((InlineStateInlined)inlineState).callee).Name)
                    {
                        if (!(HandleSeteOptimization((AssemblyExpr.Register)operand, ((InlineStateInlined)inlineState).callee)))
                        {
                            Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, new AssemblyExpr.Register(((AssemblyExpr.Register)((InlineStateInlined)inlineState).callee).Name, op.Size), operand));
                        }
                    }
                }
                else if (operand.IsPointer())
                {
                    Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, ((InlineStateInlined)inlineState).callee, operand));
                }
                else
                {
                    Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, ((InlineStateInlined)inlineState).callee, operand));
                }
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

    public AssemblyExpr.Value LockOperand(AssemblyExpr.Value operand)
    {
        if (operand.IsRegister())
        {
            alloc.Lock((AssemblyExpr.Register)operand);
        }
        else if (operand.IsPointer())
        {
            var idx = alloc.NameToIdx(((AssemblyExpr.Pointer)operand).register.Name);
            if (idx != -1)
            {
                alloc.Lock(idx);
            }
        }
        return operand;
    }
    private void UnlockOperand(AssemblyExpr.Value? operand)
    {
        if (operand == null)
        {
            return;
        }

        if (operand.IsRegister())
        {
            alloc.Unlock((AssemblyExpr.Register)operand);
        }
        else if (operand.IsPointer())
        {
            var idx = alloc.NameToIdx(((AssemblyExpr.Pointer)operand).register.Name);
            if (idx != -1)
            {
                alloc.Unlock(idx);
            }
        }
    }

    private AssemblyExpr.Value HandleParameterRegister(Expr.Parameter parameter, AssemblyExpr.Value arg)
    {
        if (arg.IsLiteral() && !parameter.modifiers["inlineRef"])
        {
            return arg.IfLiteralCreateLiteral((AssemblyExpr.Register.RegisterSize)parameter.stack.size).NonLiteral(this);
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
