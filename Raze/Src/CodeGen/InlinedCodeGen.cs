﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public AssemblyExpr.SizedValue? callee;

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
            return Analyzer.Primitives.Operation(expr.op, (AssemblyExpr.Literal)expr.operand.Accept(this), this);
        }

        if (!expr.internalFunction.modifiers["inline"])
        {
            inlineState = new(inlineState);

            var tmp = base.VisitUnaryExpr(expr);

            inlineState = inlineState.lastState;

            return tmp;
        }

        inlineState = new InlineStateInlined(inlineState);

        AssemblyExpr.Value operand = expr.operand.Accept(this);

        expr.internalFunction.parameters[0].stack.stackRegister = true;
        ((Expr.StackRegister)expr.internalFunction.parameters[0].stack).register = LockOperand(HandleParameterRegister(expr.internalFunction.parameters[0], operand));

        foreach (var bodyExpr in expr.internalFunction.block.block)
        {
            bodyExpr.Accept(this);
            alloc.FreeAll(false);
        }

        expr.internalFunction.parameters[0].stack.stackRegister = false;
        alloc.Free(((Expr.StackRegister)expr.internalFunction.parameters[0].stack).register, true);

        if (assembly.text[^1] is AssemblyExpr.Unary jmpInstruction && jmpInstruction.instruction == AssemblyExpr.Instruction.JMP
            && jmpInstruction.operand is AssemblyExpr.LocalProcedureRef procRef && procRef.name == CreateConditionalLabel(((InlineStateInlined)inlineState).inlineLabelIdx))
        {
            assembly.text.RemoveAt(assembly.text.Count - 1);

            if (((InlineStateInlined)inlineState).secondJump)
            {
                Emit(new AssemblyExpr.LocalProcedure(CreateConditionalLabel(((InlineStateInlined)inlineState).inlineLabelIdx)));
            }
        }
        else if (((InlineStateInlined)inlineState).inlineLabelIdx != -1)
        {
            Emit(new AssemblyExpr.LocalProcedure(CreateConditionalLabel(((InlineStateInlined)inlineState).inlineLabelIdx)));
        }

        var ret = ((InlineStateInlined)inlineState).callee;

        alloc.Free(((Expr.StackRegister)expr.internalFunction.parameters[0].stack).register, true);

        if (ret != null)
        {
            if (ret.IsRegister())
            {
                alloc.GetRegister(alloc.NameToIdx(((AssemblyExpr.Register)ret).name), ((AssemblyExpr.Register)ret).size);
            }
            else if (ret.IsPointer() && !((AssemblyExpr.Pointer)ret).IsOnStack())
            {
                alloc.GetRegister(alloc.NameToIdx(((AssemblyExpr.Pointer)ret).register.name), ((AssemblyExpr.Pointer)ret).size);
            }
        }

        UnlockOperand(ret);

        inlineState = inlineState.lastState;

        return ret;
    }

    public override AssemblyExpr.Value? VisitBinaryExpr(Expr.Binary expr)
    {
        if (expr.internalFunction == null)
        {
            return Analyzer.Primitives.Operation(expr.op, (AssemblyExpr.Literal)expr.left.Accept(this), (AssemblyExpr.Literal)expr.right.Accept(this), this);
        }

        if (!expr.internalFunction.modifiers["inline"])
        {
            inlineState = new(inlineState);

            var tmp = base.VisitBinaryExpr(expr);

            inlineState = inlineState.lastState;

            return tmp;
        }

        inlineState = new InlineStateInlined(inlineState);

        AssemblyExpr.Value operand1 = expr.left.Accept(this);
        AssemblyExpr.Value operand2 = expr.right.Accept(this);

        expr.internalFunction.parameters[0].stack.stackRegister = true;
        ((Expr.StackRegister)expr.internalFunction.parameters[0].stack).register = LockOperand(HandleParameterRegister(expr.internalFunction.parameters[0], operand1));

        expr.internalFunction.parameters[1].stack.stackRegister = true;
        ((Expr.StackRegister)expr.internalFunction.parameters[1].stack).register = LockOperand(HandleParameterRegister(expr.internalFunction.parameters[1], operand2));

        foreach (var bodyExpr in expr.internalFunction.block.block)
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

        if (assembly.text[^1] is AssemblyExpr.Unary jmpInstruction && jmpInstruction.instruction == AssemblyExpr.Instruction.JMP
            && jmpInstruction.operand is AssemblyExpr.LocalProcedureRef procRef && procRef.name == CreateConditionalLabel(((InlineStateInlined)inlineState).inlineLabelIdx))
        {
            assembly.text.RemoveAt(assembly.text.Count - 1);

            if (((InlineStateInlined)inlineState).secondJump)
            {
                Emit(new AssemblyExpr.LocalProcedure(CreateConditionalLabel(((InlineStateInlined)inlineState).inlineLabelIdx)));
            }
        }
        else if (((InlineStateInlined)inlineState).inlineLabelIdx != -1)
        {
            Emit(new AssemblyExpr.LocalProcedure(CreateConditionalLabel(((InlineStateInlined)inlineState).inlineLabelIdx)));
        }

        if (ret != null)
        {
            if (ret.IsRegister())
            {
                alloc.GetRegister(alloc.NameToIdx(((AssemblyExpr.Register)ret).name), ((AssemblyExpr.Register)ret).size);
            }
            else if (ret.IsPointer() && !((AssemblyExpr.Pointer)ret).IsOnStack())
            {
                alloc.GetRegister(alloc.NameToIdx(((AssemblyExpr.Pointer)ret).register.name), ((AssemblyExpr.Pointer)ret).size);
            }
        }

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
                    var size = (enclosing?.definitionType == Expr.Definition.DefinitionType.Primitive) ? enclosing.size : 8;
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

        for (int i = 0; i < expr.arguments.Count; i++)
        {
            AssemblyExpr.Value arg = expr.arguments[i].Accept(this);

            expr.internalFunction.parameters[i].stack.stackRegister = true;
            ((Expr.StackRegister)expr.internalFunction.parameters[i].stack).register = LockOperand(HandleParameterRegister(expr.internalFunction.parameters[i], arg));
        }

        foreach (var bodyExpr in expr.internalFunction.block.block)
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

        if (assembly.text[^1] is AssemblyExpr.Unary jmpInstruction && jmpInstruction.instruction == AssemblyExpr.Instruction.JMP
            && jmpInstruction.operand is AssemblyExpr.LocalProcedureRef procRef && procRef.name == CreateConditionalLabel(((InlineStateInlined)inlineState).inlineLabelIdx))
        {
            assembly.text.RemoveAt(assembly.text.Count - 1);

            if (((InlineStateInlined)inlineState).secondJump)
            {
                Emit(new AssemblyExpr.LocalProcedure(CreateConditionalLabel(((InlineStateInlined)inlineState).inlineLabelIdx)));
            }
        }
        else if (((InlineStateInlined)inlineState).inlineLabelIdx != -1)
        {
            Emit(new AssemblyExpr.LocalProcedure(CreateConditionalLabel(((InlineStateInlined)inlineState).inlineLabelIdx)));
        }

        if (ret != null)
        {
            if (ret.IsRegister())
            {
                alloc.GetRegister(alloc.NameToIdx(((AssemblyExpr.Register)ret).name), ((AssemblyExpr.Register)ret).size);
            }
            else if (ret.IsPointer() && !((AssemblyExpr.Pointer)ret).IsOnStack())
            {
                alloc.GetRegister(alloc.NameToIdx(((AssemblyExpr.Pointer)ret).register.name), ((AssemblyExpr.Pointer)ret).size);
            }
        }

        inlineState = inlineState.lastState;

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

        if (((Expr.StackRegister)((Expr.Binary)expr.value).internalFunction.parameters[0].stack).register != operand2)
        {
            Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, ((Expr.StackRegister)((Expr.Binary)expr.value).internalFunction.parameters[0].stack).register, operand2));
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

        if (!expr._void)
        {
            AssemblyExpr.Value operand = expr.value.Accept(this);

            if (((InlineStateInlined)inlineState).callee == null)
            {
                ((InlineStateInlined)inlineState).callee = operand.NonLiteral(InstructionUtils.ToRegisterSize(expr.size), this);
            }
            else
            {
                if (operand.IsRegister())
                {
                    var op = (AssemblyExpr.Register)operand;
                    if (op.name != ((AssemblyExpr.Register)((InlineStateInlined)inlineState).callee).name)
                    {
                        if (!(HandleSeteOptimization((AssemblyExpr.Register)operand, ((InlineStateInlined)inlineState).callee)))
                        {
                            Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, new AssemblyExpr.Register(((AssemblyExpr.Register)((InlineStateInlined)inlineState).callee).name, op.size), operand));
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
            Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, ((InlineStateInlined)inlineState).callee, new AssemblyExpr.Literal(Parser.LiteralTokenType.INTEGER, "0")));
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
            var idx = alloc.NameToIdx(((AssemblyExpr.Pointer)operand).register.name);
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
            var idx = alloc.NameToIdx(((AssemblyExpr.Pointer)operand).register.name);
            if (idx != -1)
            {
                alloc.Unlock(idx);
            }
        }
    }
    
    private AssemblyExpr.Value HandleParameterRegister(Expr.Parameter parameter, AssemblyExpr.Value arg)
    {
        return IsRefParameter(parameter) ? arg : MovToRegister(arg, InstructionUtils.ToRegisterSize(parameter.stack.size));
    }
    private bool IsRefParameter(Expr.Parameter parameter)
    {
        return parameter.modifiers["ref"] || parameter.modifiers["inlineRef"];
    }
}
