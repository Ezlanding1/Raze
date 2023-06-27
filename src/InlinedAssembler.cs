﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze
{
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

            public Instruction.SizedValue? callee;

            public InlineStateInlined(InlineStateNoInline lastState) : base(lastState, true)
            {
            }
        }

        public InlineStateNoInline inlineState = new(null);

        public InlinedAssembler(List<Expr> expressions) : base(expressions)
        {
        }

        public override Instruction.Value? visitUnaryExpr(Expr.Unary expr)
        {
            if (!expr.internalFunction.modifiers["inline"])
            {
                inlineState = new(inlineState);

                var tmp = base.visitUnaryExpr(expr);

                inlineState = inlineState.lastState;

                return tmp;
            }

            inlineState = new InlineStateInlined(inlineState);

            Instruction.Value operand = expr.operand.Accept(this);

            if (InstructionUtils.ConditionalJump.ContainsKey(expr.op.type))
            {
                lastJump = expr.op.type;
            }

            expr.internalFunction.parameters[0].stack.stackRegister = true;
            ((Expr.StackRegister)expr.internalFunction.parameters[0].stack).register = LockOperand(expr.internalFunction.parameters[0].modifiers["ref"]? operand : PassByValue(operand));

            foreach (var bodyExpr in expr.internalFunction.block)
            {
                bodyExpr.Accept(this);
            }

            expr.internalFunction.parameters[0].stack.stackRegister = false;
            alloc.Free(((Expr.StackRegister)expr.internalFunction.parameters[0].stack).register, true);

            if (((InlineStateInlined)inlineState).inlineLabelIdx != -1)
            {
                emit(new Instruction.LocalProcedure(CreateConditionalLabel(((InlineStateInlined)inlineState).inlineLabelIdx)));
            }

            var ret = ((InlineStateInlined)inlineState).callee;

            if (ret != ((Expr.StackRegister)expr.internalFunction.parameters[0].stack).register)
                alloc.Free(((Expr.StackRegister)expr.internalFunction.parameters[0].stack).register, true);

            UnlockOperand(ret);

            inlineState = inlineState.lastState;

            return ret;
        }

        public override Instruction.Value? visitBinaryExpr(Expr.Binary expr)
        {
            if (!expr.internalFunction.modifiers["inline"])
            {
                inlineState = new(inlineState);

                var tmp = base.visitBinaryExpr(expr);

                inlineState = inlineState.lastState;

                return tmp;
            }

            inlineState = new InlineStateInlined(inlineState);

            Instruction.Value operand1 = expr.left.Accept(this);
            Instruction.Value operand2 = expr.right.Accept(this);

            if (InstructionUtils.ConditionalJump.ContainsKey(expr.op.type))
            {
                lastJump = expr.op.type;
            }

            expr.internalFunction.parameters[0].stack.stackRegister = true;
            ((Expr.StackRegister)expr.internalFunction.parameters[0].stack).register = LockOperand(expr.internalFunction.parameters[0].modifiers["ref"]? operand1 : PassByValue(operand1));

            expr.internalFunction.parameters[1].stack.stackRegister = true;
            ((Expr.StackRegister)expr.internalFunction.parameters[1].stack).register = LockOperand(expr.internalFunction.parameters[1].modifiers["ref"]? operand2 : PassByValue(operand2));

            foreach (var bodyExpr in expr.internalFunction.block)
            {
                bodyExpr.Accept(this);
            }

            expr.internalFunction.parameters[0].stack.stackRegister = false;
            expr.internalFunction.parameters[1].stack.stackRegister = false;

            var ret = ((InlineStateInlined)inlineState).callee;

            if (ret != ((Expr.StackRegister)expr.internalFunction.parameters[0].stack).register)
                alloc.Free(((Expr.StackRegister)expr.internalFunction.parameters[0].stack).register, true);

            if (ret != ((Expr.StackRegister)expr.internalFunction.parameters[1].stack).register)
                alloc.Free(((Expr.StackRegister)expr.internalFunction.parameters[1].stack).register, true);

            UnlockOperand(ret);

            if (((InlineStateInlined)inlineState).inlineLabelIdx != -1)
            {
                emit(new Instruction.LocalProcedure(CreateConditionalLabel(((InlineStateInlined)inlineState).inlineLabelIdx)));
            }

            inlineState = inlineState.lastState;

            return ret;
        }

        public override Instruction.Value? visitCallExpr(Expr.Call expr)
        {
            if (!expr.internalFunction.modifiers["inline"])
            {
                inlineState = new(inlineState);

                var tmp = base.visitCallExpr(expr);

                inlineState = inlineState.lastState;

                return tmp;
            }

            inlineState = new InlineStateInlined(inlineState);

            Instruction.Value? instanceArg = null;

            if (!expr.internalFunction.modifiers["static"])
            {
                alloc.Lock((Instruction.Register)(instanceArg = alloc.CurrentRegister(Instruction.Register.RegisterSize._64Bits)));
                if (!expr.constructor)
                {
                    if (expr.callee != null)
                    {
                        for (int i = expr.offsets.Length - 1; i >= 1; i--)
                        {
                            emit(new Instruction.Binary("MOV", instanceArg, new Instruction.Pointer(((i == expr.offsets.Length - 1) ? new(Instruction.Register.RegisterName.RBP, Instruction.Register.RegisterSize._64Bits) : alloc.CurrentRegister(Instruction.Register.RegisterSize._64Bits)), expr.offsets[i].stackOffset, 8)));
                        }
                        emit(new Instruction.Binary("MOV", instanceArg, new Instruction.Pointer((0 == expr.offsets.Length - 1) ? new(Instruction.Register.RegisterName.RBP, Instruction.Register.RegisterSize._64Bits) : alloc.CurrentRegister(Instruction.Register.RegisterSize._64Bits), expr.offsets[0].stackOffset, 8)));
                    }
                    else
                    {
                        emit(new Instruction.Binary("MOV", instanceArg, new Instruction.Pointer(8, 8)));
                    }
                }
                else
                {
                    emit(new Instruction.Binary("MOV", instanceArg, new Instruction.Register(Instruction.Register.RegisterName.RBX, Instruction.Register.RegisterSize._64Bits)));
                }
            }


            for (int i = 0; i < expr.arguments.Count; i++)
            {
                Instruction.Value arg = expr.arguments[i].Accept(this);

                expr.internalFunction.parameters[i].stack.stackRegister = true;
                ((Expr.StackRegister)expr.internalFunction.parameters[i].stack).register = LockOperand(expr.internalFunction.parameters[0].modifiers["ref"]? arg : PassByValue(arg));
            }

            foreach (var bodyExpr in expr.internalFunction.block)
            {
                bodyExpr.Accept(this);
            }
            
            var ret = ((InlineStateInlined)inlineState).callee;

            if (instanceArg != null)
            {
                if (ret != ((Expr.StackRegister)expr.internalFunction.parameters[0].stack).register)
                    alloc.Free(instanceArg, true);
            }

            for (int i = 0; i < expr.arguments.Count; i++)
            {
                expr.internalFunction.parameters[i].stack.stackRegister = false;

                if (ret != ((Expr.StackRegister)expr.internalFunction.parameters[i].stack).register)
                    alloc.Free(((Expr.StackRegister)expr.internalFunction.parameters[i].stack).register, true);
            }

            UnlockOperand(ret);

            if (((InlineStateInlined)inlineState).inlineLabelIdx != -1)
            {
                emit(new Instruction.LocalProcedure(CreateConditionalLabel(((InlineStateInlined)inlineState).inlineLabelIdx)));
            }

            inlineState = inlineState.lastState;

            return ret;
        }

        public override Instruction.Value? visitAssignExpr(Expr.Assign expr)
        {
            if (!expr.binary || !((Expr.Binary)expr.value).internalFunction.modifiers["inline"])
            {
                return base.visitAssignExpr(expr);
            }

            ((Expr.Binary)expr.value).internalFunction.parameters[0].modifiers["ref"] = true;
            var operand2 = expr.value.Accept(this);
            ((Expr.Binary)expr.value).internalFunction.parameters[0].modifiers["ref"] = false;

            if (((Expr.StackRegister)((Expr.Binary)expr.value).internalFunction.parameters[0].stack).register != operand2)
            {
                emit(new Instruction.Binary("MOV", ((Expr.StackRegister)((Expr.Binary)expr.value).internalFunction.parameters[0].stack).register, operand2));
            }

            alloc.Free(operand2);

            return null;
        }

        public override Instruction.Value? visitReturnExpr(Expr.Return expr)
        {
            if (!inlineState.inline)
            {
                return base.visitReturnExpr(expr);
            }

            if (!expr._void)
            {
                Instruction.Value operand = expr.value.Accept(this);

                if (((InlineStateInlined)inlineState).callee == null)
                {
                    ((InlineStateInlined)inlineState).callee = FormatOperand1(operand);
                }
                else
                {
                    if (operand.IsRegister())
                    {
                        var op = (Instruction.Register)operand;
                        if (op.name != ((Instruction.Register)((InlineStateInlined)inlineState).callee).name)
                            emit(new Instruction.Binary("MOV", new Instruction.Register(((Instruction.Register)((InlineStateInlined)inlineState).callee).name, op.size), operand));
                    }
                    else if (operand.IsPointer())
                    {
                        emit(new Instruction.Binary("MOV", ((InlineStateInlined)inlineState).callee, operand));
                    }
                    else
                    {
                        emit(new Instruction.Binary("MOV", ((InlineStateInlined)inlineState).callee, operand));
                    }
                }
            }
            else
            {
                emit(new Instruction.Binary("MOV", ((InlineStateInlined)inlineState).callee, new Instruction.Literal(Parser.Literals[0], "0")));
            }

            if (((InlineStateInlined)inlineState).inlineLabelIdx == -1)
            {
                ((InlineStateInlined)inlineState).inlineLabelIdx = conditionalCount;
                emit(new Instruction.Unary("JMP", new Instruction.LocalProcedureRef(CreateConditionalLabel(conditionalCount++))));
            }
            else
            {
                emit(new Instruction.Unary("JMP", new Instruction.LocalProcedureRef(CreateConditionalLabel(((InlineStateInlined)inlineState).inlineLabelIdx))));
            }

            return null;
        }

        public override Instruction.SizedValue FormatOperand1(Instruction.Value operand)
        {
            if (!inlineState.inline)
            {
                return base.FormatOperand1(operand);
            }

            if (operand.IsLiteral())
            {
                emit(new Instruction.Binary("MOV", alloc.CurrentRegister(Instruction.Register.RegisterSize._32Bits), operand));
                return alloc.NextRegister(Instruction.Register.RegisterSize._32Bits);
            }
            return (Instruction.SizedValue)operand;
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
    }
}
