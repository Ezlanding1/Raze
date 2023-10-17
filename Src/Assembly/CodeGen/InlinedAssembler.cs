using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

internal class InlinedAssembler : Assembler
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

        public Instruction.SizedValue? callee;

        public InlineStateInlined(InlineStateNoInline lastState) : base(lastState, true)
        {
        }
    }

    public InlineStateNoInline inlineState = new(null);

    public InlinedAssembler(List<Expr> expressions) : base(expressions)
    {
    }

    public override Instruction.Value? VisitUnaryExpr(Expr.Unary expr)
    {
        if (expr.internalFunction == null)
        {
            return Analyzer.Primitives.Operation(expr.op, (Instruction.Literal)expr.operand.Accept(this), this);
        }

        if (!expr.internalFunction.modifiers["inline"])
        {
            inlineState = new(inlineState);

            var tmp = base.VisitUnaryExpr(expr);

            inlineState = inlineState.lastState;

            return tmp;
        }

        inlineState = new InlineStateInlined(inlineState);

        Instruction.Value operand = expr.operand.Accept(this);

        expr.internalFunction.parameters[0].stack.stackRegister = true;
        ((Expr.StackRegister)expr.internalFunction.parameters[0].stack).register = LockOperand(HandleParameterRegister(expr.internalFunction.parameters[0], operand));

        foreach (var bodyExpr in expr.internalFunction.block)
        {
            bodyExpr.Accept(this);
            alloc.FreeAll(false);
        }

        expr.internalFunction.parameters[0].stack.stackRegister = false;
        alloc.Free(((Expr.StackRegister)expr.internalFunction.parameters[0].stack).register, true);

        if (instructions[^1] is Instruction.Unary jmpInstruction && jmpInstruction.instruction == "JMP"
            && jmpInstruction.operand is Instruction.LocalProcedureRef procRef && procRef.name == CreateConditionalLabel(((InlineStateInlined)inlineState).inlineLabelIdx))
        {
            instructions.RemoveAt(instructions.Count - 1);

            if (((InlineStateInlined)inlineState).secondJump)
            {
                Emit(new Instruction.LocalProcedure(CreateConditionalLabel(((InlineStateInlined)inlineState).inlineLabelIdx)));
            }
        }
        else if (((InlineStateInlined)inlineState).inlineLabelIdx != -1)
        {
            Emit(new Instruction.LocalProcedure(CreateConditionalLabel(((InlineStateInlined)inlineState).inlineLabelIdx)));
        }

        var ret = ((InlineStateInlined)inlineState).callee;

        alloc.Free(((Expr.StackRegister)expr.internalFunction.parameters[0].stack).register, true);

        if (ret != null)
        {
            if (ret.IsRegister())
            {
                alloc.GetRegister(alloc.NameToIdx(((Instruction.Register)ret).name), ((Instruction.Register)ret).size);
            }
            else if (ret.IsPointer() && !((Instruction.Pointer)ret).IsOnStack())
            {
                alloc.GetRegister(alloc.NameToIdx(((Instruction.Pointer)ret).register.name), ((Instruction.Pointer)ret).size);
            }
        }

        UnlockOperand(ret);

        inlineState = inlineState.lastState;

        return ret;
    }

    public override Instruction.Value? VisitBinaryExpr(Expr.Binary expr)
    {
        if (expr.internalFunction == null)
        {
            return Analyzer.Primitives.Operation(expr.op, (Instruction.Literal)expr.left.Accept(this), (Instruction.Literal)expr.right.Accept(this), this);
        }

        if (!expr.internalFunction.modifiers["inline"])
        {
            inlineState = new(inlineState);

            var tmp = base.VisitBinaryExpr(expr);

            inlineState = inlineState.lastState;

            return tmp;
        }

        inlineState = new InlineStateInlined(inlineState);

        Instruction.Value operand1 = expr.left.Accept(this);
        Instruction.Value operand2 = expr.right.Accept(this);

        expr.internalFunction.parameters[0].stack.stackRegister = true;
        ((Expr.StackRegister)expr.internalFunction.parameters[0].stack).register = LockOperand(HandleParameterRegister(expr.internalFunction.parameters[0], operand1));

        expr.internalFunction.parameters[1].stack.stackRegister = true;
        ((Expr.StackRegister)expr.internalFunction.parameters[1].stack).register = LockOperand(HandleParameterRegister(expr.internalFunction.parameters[1], operand2));

        foreach (var bodyExpr in expr.internalFunction.block)
        {
            bodyExpr.Accept(this);
            alloc.FreeAll(false);
        }

        expr.internalFunction.parameters[0].stack.stackRegister = false;
        expr.internalFunction.parameters[1].stack.stackRegister = false;

        var ret = ((InlineStateInlined)inlineState).callee;

        alloc.Free(((Expr.StackRegister)expr.internalFunction.parameters[0].stack).register, true);
        alloc.Free(((Expr.StackRegister)expr.internalFunction.parameters[1].stack).register, true);

        UnlockOperand(ret);

        if (instructions[^1] is Instruction.Unary jmpInstruction && jmpInstruction.instruction == "JMP"
            && jmpInstruction.operand is Instruction.LocalProcedureRef procRef && procRef.name == CreateConditionalLabel(((InlineStateInlined)inlineState).inlineLabelIdx))
        {
            instructions.RemoveAt(instructions.Count - 1);

            if (((InlineStateInlined)inlineState).secondJump)
            {
                Emit(new Instruction.LocalProcedure(CreateConditionalLabel(((InlineStateInlined)inlineState).inlineLabelIdx)));
            }
        }
        else if (((InlineStateInlined)inlineState).inlineLabelIdx != -1)
        {
            Emit(new Instruction.LocalProcedure(CreateConditionalLabel(((InlineStateInlined)inlineState).inlineLabelIdx)));
        }

        if (ret != null)
        {
            if (ret.IsRegister())
            {
                alloc.GetRegister(alloc.NameToIdx(((Instruction.Register)ret).name), ((Instruction.Register)ret).size);
            }
            else if (ret.IsPointer() && !((Instruction.Pointer)ret).IsOnStack())
            {
                alloc.GetRegister(alloc.NameToIdx(((Instruction.Pointer)ret).register.name), ((Instruction.Pointer)ret).size);
            }
        }

        inlineState = inlineState.lastState;

        return ret;
    }

    public override Instruction.Value? VisitCallExpr(Expr.Call expr)
    {
        if (!expr.internalFunction.modifiers["inline"])
        {
            inlineState = new(inlineState);

            var tmp = base.VisitCallExpr(expr);

            inlineState = inlineState.lastState;

            return tmp;
        }

        inlineState = new InlineStateInlined(inlineState);

        Instruction.Value? instanceArg = null;

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
                    var size = (enclosing?.definitionType == Expr.Definition.DefinitionType.Primitive) ? enclosing.size : 8;
                    instanceArg = new Instruction.Pointer(8, size);
                }
            }
            else
            {
                instanceArg = alloc.CurrentRegister(InstructionUtils.SYS_SIZE);
                Emit(new Instruction.Binary("MOV", instanceArg, new Instruction.Register(Instruction.Register.RegisterName.RBX, InstructionUtils.SYS_SIZE)));
            }

            if (instanceArg.IsRegister())
            {
                alloc.Lock((Instruction.Register)instanceArg);
            }
        }

        for (int i = 0; i < expr.arguments.Count; i++)
        {
            Instruction.Value arg = expr.arguments[i].Accept(this);

            expr.internalFunction.parameters[i].stack.stackRegister = true;
            ((Expr.StackRegister)expr.internalFunction.parameters[i].stack).register = LockOperand(HandleParameterRegister(expr.internalFunction.parameters[i], arg));
        }

        foreach (var bodyExpr in expr.internalFunction.block)
        {
            bodyExpr.Accept(this);
            alloc.FreeAll(false);
        }
        
        var ret = ((InlineStateInlined)inlineState).callee;

        if (instanceArg != null)
        {
            alloc.Free(instanceArg, true);
        }

        for (int i = 0; i < expr.arguments.Count; i++)
        {
            expr.internalFunction.parameters[i].stack.stackRegister = false;

            alloc.Free(((Expr.StackRegister)expr.internalFunction.parameters[i].stack).register, true);
        }

        UnlockOperand(ret);

        if (instructions[^1] is Instruction.Unary jmpInstruction && jmpInstruction.instruction == "JMP"
            && jmpInstruction.operand is Instruction.LocalProcedureRef procRef && procRef.name == CreateConditionalLabel(((InlineStateInlined)inlineState).inlineLabelIdx))
        {
            instructions.RemoveAt(instructions.Count - 1);

            if (((InlineStateInlined)inlineState).secondJump)
            {
                Emit(new Instruction.LocalProcedure(CreateConditionalLabel(((InlineStateInlined)inlineState).inlineLabelIdx)));
            }
        }
        else if (((InlineStateInlined)inlineState).inlineLabelIdx != -1)
        {
            Emit(new Instruction.LocalProcedure(CreateConditionalLabel(((InlineStateInlined)inlineState).inlineLabelIdx)));
        }

        if (ret != null)
        {
            if (ret.IsRegister())
            {
                alloc.GetRegister(alloc.NameToIdx(((Instruction.Register)ret).name), ((Instruction.Register)ret).size);
            }
            else if (ret.IsPointer() && !((Instruction.Pointer)ret).IsOnStack())
            {
                alloc.GetRegister(alloc.NameToIdx(((Instruction.Pointer)ret).register.name), ((Instruction.Pointer)ret).size);
            }
        }

        inlineState = inlineState.lastState;

        return ret;
    }

    public override Instruction.Value? VisitAssignExpr(Expr.Assign expr)
    {
        if (!expr.binary || !((Expr.Binary)expr.value).internalFunction.modifiers["inline"])
        {
            return base.VisitAssignExpr(expr);
        }

        Instruction.Value operand2;

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

        if (((Expr.StackRegister)((Expr.Binary)expr.value).internalFunction.parameters[0].stack).register != operand2)
        {
            Emit(new Instruction.Binary("MOV", ((Expr.StackRegister)((Expr.Binary)expr.value).internalFunction.parameters[0].stack).register, operand2));
        }

        alloc.Free(operand2);

        return null;
    }

    public override Instruction.Value? VisitReturnExpr(Expr.Return expr)
    {
        if (!inlineState.inline)
        {
            return base.VisitReturnExpr(expr);
        }

        if (!expr._void)
        {
            Instruction.Value operand = expr.value.Accept(this);

            if (((InlineStateInlined)inlineState).callee == null)
            {
                ((InlineStateInlined)inlineState).callee = operand.NonLiteral(InstructionUtils.ToRegisterSize(expr.size), this);
            }
            else
            {
                if (operand.IsRegister())
                {
                    var op = (Instruction.Register)operand;
                    if (op.name != ((Instruction.Register)((InlineStateInlined)inlineState).callee).name)
                    {
                        if (!(HandleSeteOptimization((Instruction.Register)operand, ((InlineStateInlined)inlineState).callee)))
                        {
                            Emit(new Instruction.Binary("MOV", new Instruction.Register(((Instruction.Register)((InlineStateInlined)inlineState).callee).name, op.size), operand));
                        }
                    }
                }
                else if (operand.IsPointer())
                {
                    Emit(new Instruction.Binary("MOV", ((InlineStateInlined)inlineState).callee, operand));
                }
                else
                {
                    Emit(new Instruction.Binary("MOV", ((InlineStateInlined)inlineState).callee, operand));
                }
            }
        }
        else
        {
            Emit(new Instruction.Binary("MOV", ((InlineStateInlined)inlineState).callee, new Instruction.Literal(Parser.Literals[0], "0")));
        }

        if (((InlineStateInlined)inlineState).inlineLabelIdx == -1)
        {
            ((InlineStateInlined)inlineState).inlineLabelIdx = conditionalCount;
            Emit(new Instruction.Unary("JMP", new Instruction.LocalProcedureRef(CreateConditionalLabel(conditionalCount++))));
        }
        else
        {
            ((InlineStateInlined)inlineState).secondJump = true;
            Emit(new Instruction.Unary("JMP", new Instruction.LocalProcedureRef(CreateConditionalLabel(((InlineStateInlined)inlineState).inlineLabelIdx))));
        }

        return null;
    }

    public Instruction.Value LockOperand(Instruction.Value operand) 
    {
        if (operand.IsRegister())
        {
            alloc.Lock((Instruction.Register)operand);
        }
        else if (operand.IsPointer())
        {
            var idx = alloc.NameToIdx(((Instruction.Pointer)operand).register.name);
            if (idx != -1)
            {
                alloc.Lock(idx);
            }
        }
        return operand;
    }
    private void UnlockOperand(Instruction.Value? operand)
    {
        if (operand == null)
        {
            return;
        }

        if (operand.IsRegister())
        {
            alloc.Unlock((Instruction.Register)operand);
        }
        else if (operand.IsPointer())
        {
            var idx = alloc.NameToIdx(((Instruction.Pointer)operand).register.name);
            if (idx != -1)
            {
                alloc.Unlock(idx);
            }
        }
    }
    
    private Instruction.Value HandleParameterRegister(Expr.Parameter parameter, Instruction.Value arg)
    {
        return IsRefParameter(parameter) ? arg : MovToRegister(arg, InstructionUtils.ToRegisterSize(parameter.stack.size));
    }
    private bool IsRefParameter(Expr.Parameter parameter)
    {
        return parameter.modifiers["ref"] || parameter.modifiers["inlineRef"];
    }
}
