namespace Raze;

public partial class InlinedCodeGen : CodeGen
{
    internal class InlineState(Expr.Function currentInlined)
    {
        public int inlineLabelIdx = -1;
        public bool secondJump = false;
        public AssemblyExpr.IValue? callee;
        public Expr.Function currentInlined = currentInlined;
    }
    internal InlineState? inlineState = null;

    public override AssemblyExpr.IValue? VisitUnaryExpr(Expr.Unary expr)
    {
        if (expr.internalFunction == null)
        {
            return Analyzer.Primitives.Operation(expr.op, (AssemblyExpr.UnresolvedLiteral)expr.operand.Accept(this), this);
        }

        if (!expr.internalFunction.modifiers["inline"])
        {
            using (new SaveInlineStateNoInline(this))
                return base.VisitUnaryExpr(expr);
        }

        using (new SaveInlineStateInline(this, expr.internalFunction))
            return HandleInvokable(expr);
    }

    public override AssemblyExpr.IValue? VisitBinaryExpr(Expr.Binary expr)
    {
        if (expr.internalFunction == null)
        {
            return Analyzer.Primitives.Operation(expr.op, (AssemblyExpr.UnresolvedLiteral)expr.left.Accept(this), (AssemblyExpr.UnresolvedLiteral)expr.right.Accept(this), this);
        }

        if (!expr.internalFunction.modifiers["inline"])
        {
            using (new SaveInlineStateNoInline(this))
                return base.VisitBinaryExpr(expr);
        }

        using (new SaveInlineStateInline(this, expr.internalFunction))
            return HandleInvokable(expr);
    }

    public override AssemblyExpr.IValue? VisitCallExpr(Expr.Call expr)
    {
        if (!expr.internalFunction.modifiers["inline"])
        {
            using (new SaveInlineStateNoInline(this))
                return base.VisitCallExpr(expr);
        }

        var saveInlineState = new SaveInlineStateInline(this, expr.internalFunction);

        (AssemblyExpr.IValue? enclosingThisStackDataValue, int thisSize) = (null, -1);
        bool lastThisIsLocked = false;

        if (!expr.internalFunction.modifiers["static"])
        {
            thisSize = SymbolTableSingleton.SymbolTable.NearestEnclosingClass(expr.internalFunction)!.allocSize;
            enclosingThisStackDataValue = Expr.DataType.This(thisSize).value;
            Expr.DataType.This(thisSize).inlinedData = true;

            if (!expr.constructor)
            {
                if (expr.callee != null)
                {
                    Expr.DataType.This(thisSize).value = expr.callee.Accept(this);
                }
                else
                {
                    var enclosing = SymbolTableSingleton.SymbolTable.NearestEnclosingClass(expr.internalFunction);
                    var size = enclosing.allocSize;
                    Expr.DataType.This(thisSize).value = new AssemblyExpr.Pointer(AssemblyExpr.Register.RegisterName.RBP, -8, (AssemblyExpr.Register.RegisterSize)size);
                }
            }
            else
            {
                Expr.DataType.This(thisSize).value = alloc.GetRegister(AssemblyExpr.Register.RegisterName.RBX, InstructionUtils.SYS_SIZE);
            }


            if (Expr.DataType.This(thisSize).value.IsRegister(out var register))
            {
                lastThisIsLocked = !alloc.IsLocked(register.Name);
                alloc.Lock(register);
            }

        }

        var ret = HandleInvokable(expr);

        if (enclosingThisStackDataValue != null)
        {
            Expr.DataType.This(thisSize).inlinedData = false;
            bool retIsThis = !(ret?.IsLiteral() != false) && !Expr.DataType.This(thisSize).value.IsLiteral() && ((AssemblyExpr.IRegisterPointer)Expr.DataType.This(thisSize).value).GetRegister().Name == ((AssemblyExpr.IRegisterPointer)ret).GetRegister().Name;
            if (!retIsThis)
                alloc.Free(Expr.DataType.This(thisSize).value, lastThisIsLocked);
            Expr.DataType.This(thisSize).value = enclosingThisStackDataValue;
        }

        saveInlineState.Dispose();
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

        var ret = inlineState.callee;
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
        return HandleRefVariableDeref(invokable.internalFunction.refReturn, ret, (AssemblyExpr.Register.RegisterSize)invokable.internalFunction._returnType.type.size, invokable.internalFunction._returnType.type);
    }

    public override AssemblyExpr.IValue? VisitAssignExpr(Expr.Assign expr)
    {
        if (!expr.binary || !((Expr.Binary)expr.value).internalFunction.modifiers["inline"])
        {
            return base.VisitAssignExpr(expr);
        }

        AssemblyExpr.IValue? operand2;

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

        var operand1 = ((Expr.Binary)expr.value).internalFunction.parameters[0].stack.value;

        if (operand1 != operand2)
        {
            Assign(expr, operand1, operand2);
        }

        alloc.Free(operand2);

        return null;
    }

    public override AssemblyExpr.IValue? VisitReturnExpr(Expr.Return expr)
    {
        if (inlineState == null)
        {
            return base.VisitReturnExpr(expr);
        }

        if (!expr.IsVoid(inlineState.currentInlined))
        {
            Expr.Function current = inlineState.currentInlined;

            AssemblyExpr.Instruction instruction = GetMoveInstruction(current.refReturn, current._returnType.type);
                
            var returnSize = InstructionUtils.ToRegisterSize(current._returnType.type.allocSize);
            AssemblyExpr.IValue operand = expr.value.Accept(this).IfLiteralCreateLiteral(returnSize);

            if (current.refReturn)
            {
                (instruction, operand) = PreserveRefPtrVariable(expr.value, (AssemblyExpr.Pointer)operand);
            }

            if (inlineState.callee == null)
            {
                alloc.Free(operand);
                inlineState.callee = alloc.NextRegister(returnSize, current._returnType.type);
            }

            if (operand.IsRegister(out var op))
            {
                if (!inlineState.callee.IsRegister(out var reg) || op.Name != reg.Name)
                {
                    if (!HandleSeteOptimization(op, inlineState.callee))
                    {
                        Emit(new AssemblyExpr.Binary(instruction, inlineState.callee, operand));
                    }
                }
            }
            else
            {
                Emit(new AssemblyExpr.Binary(instruction, inlineState.callee, operand));
            }
        }
        else
        {
            Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, inlineState.callee, new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, [0])));
        }

        if (inlineState.inlineLabelIdx == -1)
        {
            inlineState.inlineLabelIdx = conditionalCount;
            Emit(new AssemblyExpr.Unary(AssemblyExpr.Instruction.JMP, new AssemblyExpr.LocalProcedureRef(CreateConditionalLabel(conditionalCount++))));
        }
        else
        {
            inlineState.secondJump = true;
            Emit(new AssemblyExpr.Unary(AssemblyExpr.Instruction.JMP, new AssemblyExpr.LocalProcedureRef(CreateConditionalLabel(inlineState.inlineLabelIdx))));
        }

        return null;
    }

    public AssemblyExpr.IValue LockOperand(AssemblyExpr.IValue operand)
    {
        if (operand.IsRegister(out var register))
        {
            alloc.Lock(register);
        }
        else if (operand.IsPointer(out var pointer) && pointer.value != null && !pointer.IsOnStack())
        {
            alloc.Lock(pointer.value);
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
        else if (operand.IsPointer(out var pointer) && pointer.value != null && !pointer.IsOnStack())
        {
            alloc.Unlock(pointer.value);
        }
    }

    private AssemblyExpr.IValue HandleParameterRegister(Expr.Parameter parameter, AssemblyExpr.IValue arg)
    {
        if (arg.IsLiteral(out var literal) && !parameter.modifiers["inlineRef"])
        {
            return (literal.CreateLiteral((AssemblyExpr.Register.RegisterSize)parameter.stack.size)).NonLiteral(this, parameter.stack.type);
        }
        if (IsRefParameter(parameter))
        {
            return arg;
        }
        return arg.NonPointer(this, parameter.stack.type);
    }
    private bool IsRefParameter(Expr.Parameter parameter)
    {
        return parameter.modifiers["ref"] || parameter.modifiers["inlineRef"];
    }

    private void HandleInlinedReturnOptimization()
    {
        if (assembly.text[^1] is AssemblyExpr.Unary jmpInstruction && jmpInstruction.instruction == AssemblyExpr.Instruction.JMP
            && jmpInstruction.operand is AssemblyExpr.LocalProcedureRef procRef && procRef.Name == CreateConditionalLabel(inlineState.inlineLabelIdx))
        {
            assembly.text.RemoveAt(assembly.text.Count - 1);

            if (inlineState.secondJump)
            {
                Emit(new AssemblyExpr.LocalProcedure(CreateConditionalLabel(inlineState.inlineLabelIdx)));
            }
            else
            {
                conditionalCount--;
            }
        }
        else if (inlineState.inlineLabelIdx != -1)
        {
            Emit(new AssemblyExpr.LocalProcedure(CreateConditionalLabel(inlineState.inlineLabelIdx)));
        }
    }
}
