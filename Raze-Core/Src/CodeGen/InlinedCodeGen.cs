
namespace Raze;

public partial class InlinedCodeGen : CodeGen
{
    internal class InlineState
    {
        public int inlineLabelIdx = -1;
        public bool secondJump = false;
        public AssemblyExpr.IValue? callee;
    }
    internal InlineState? inlineState = null;

    public InlinedCodeGen(SystemInfo systemInfo) : base(systemInfo) { }

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

        if (!expr.internalFunction.modifiers["static"])
        {
            thisSize = SymbolTableSingleton.SymbolTable.NearestEnclosingClass(expr.internalFunction)!.allocSize;
            var _this = Expr.ThisStackData.GetThis(thisSize);
            enclosingThisStackDataValue = _this.value;
            _this.inlinedData = true;

            if (!expr.constructor)
            {
                if (expr.callee != null)
                {
                    _this.value = expr.callee.Accept(this);
                }
                else
                {
                    var enclosing = SymbolTableSingleton.SymbolTable.NearestEnclosingClass(expr.internalFunction);
                    var size = enclosing.allocSize;
                    _this.value = new AssemblyExpr.Pointer(AssemblyExpr.Register.RegisterName.RBP, -size, (AssemblyExpr.Register.RegisterSize)size);
                }
            }
            else
            {
                _this.value = heapAllocResultValue;
            }
            alloc.Lock(_this.value);
        }

        var ret = HandleInvokable(expr);

        if (enclosingThisStackDataValue != null)
        {
            var _this = Expr.ThisStackData.GetThis(thisSize);
            _this.inlinedData = false;
            alloc.UnlockAndFree(_this.value);
            _this.value = enclosingThisStackDataValue;
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

            var inlinedArgument = HandleParameterRegister(invokable.internalFunction.parameters[i], args[i]);
            alloc.Lock(inlinedArgument);

            invokable.internalFunction.parameters[i].stack.value = inlinedArgument;
        }

        foreach (var bodyExpr in invokable.internalFunction.block.block)
        {
            alloc.Free(bodyExpr.Accept(this));
        }

        var ret = inlineState.callee;

        for (int i = 0; i < invokable.Arguments.Count; i++)
        {
            invokable.internalFunction.parameters[i].stack.inlinedData = false;
            alloc.UnlockAndFree(invokable.internalFunction.parameters[i].stack.value);
        }

        alloc.Unlock(ret);

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
        
        var current = (Expr.Function)alloc.Current;

        if (!expr.IsVoid(current._returnType.type))
        {
            AssemblyExpr.Instruction instruction = GetMoveInstruction(current.refReturn, current._returnType.type);
                
            var returnSize = InstructionUtils.ToRegisterSize(current._returnType.type.allocSize);
            AssemblyExpr.IValue operand = expr.value.Accept(this).IfLiteralCreateLiteral(returnSize);

            if (current.refReturn)
            {
                (instruction, operand) = PreserveRefPtrVariable(expr.value, (AssemblyExpr.Pointer)operand);
            }

            InlinedReturnIValue(operand, instruction, returnSize, current._returnType.type);
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

    public void InlinedReturnIValue(AssemblyExpr.IValue operand, AssemblyExpr.Instruction instruction, AssemblyExpr.Register.RegisterSize returnSize, Expr.Type returnType)
    {
        bool operandIsCallee = false;

        if (inlineState.callee == null)
        {
            if (operand.IsRegister() && !alloc.IsLocked(operand))
            {
                alloc.Lock(operand);
                inlineState.callee = operand;
                operandIsCallee = true;
            }
            else
            {
                alloc.Free(operand);
                inlineState.callee = alloc.NextRegister(returnSize, returnType);
                alloc.Lock(inlineState.callee);
            }
        }

        if (!operandIsCallee)
        {
            if (operand.IsRegister(out var op))
            {
                if (!HandleSeteOptimization(op, inlineState.callee))
                {
                    Emit(new AssemblyExpr.Binary(instruction, inlineState.callee, operand));
                }
            }
            else
            {
                Emit(new AssemblyExpr.Binary(instruction, inlineState.callee, operand));
            }
        }

        alloc.Free(operand);
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
