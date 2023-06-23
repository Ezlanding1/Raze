using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze
{
    internal class InlinedAssembler : Assembler
    {
        class InlineStateNoInline
        {
            public InlineStateNoInline lastState;

            public bool inline;

            public InlineStateNoInline(InlineStateNoInline lastState, bool inline=false)
            {
                this.lastState = lastState;
                this.inline = inline;
            }
        }

        class InlineStateInlined : InlineStateNoInline
        {
            public int inlineLabelIdx = -1;

            public Instruction.Register? callee;

            public InlineStateInlined(InlineStateNoInline lastState) : base(lastState, true)
            {
            }
        }

        InlineStateNoInline inlineState = new(null);

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
            ((Expr.StackRegister)expr.internalFunction.parameters[0].stack).register = HandleOperand(operand);
            alloc.Lock(((Expr.StackRegister)expr.internalFunction.parameters[0].stack).register);

            foreach (var bodyExpr in expr.internalFunction.block)
            {
                bodyExpr.Accept(this);
            }

            expr.internalFunction.parameters[0].stack.stackRegister = false;

            if (((InlineStateInlined)inlineState).inlineLabelIdx != -1)
            {
                emit(new Instruction.LocalProcedure(CreateConditionalLabel(((InlineStateInlined)inlineState).inlineLabelIdx)));
            }

            alloc.Free(operand, true);

            Instruction.Register tmp2 = ((InlineStateInlined)inlineState).callee;

            inlineState = inlineState.lastState;

            tmp2 = tmp2 ?? (expr.internalFunction.modifiers["unsafe"] ? ((Expr.StackRegister)expr.internalFunction.parameters[0].stack).register : null);

            if (tmp2 != null)
            {
                return alloc.GetRegister(alloc.NameToIdx(tmp2.name), tmp2.size);
            }
            return tmp2;
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
            ((Expr.StackRegister)expr.internalFunction.parameters[0].stack).register = HandleOperand(operand1);
            alloc.Lock(((Expr.StackRegister)expr.internalFunction.parameters[0].stack).register);

            expr.internalFunction.parameters[1].stack.stackRegister = true;
            ((Expr.StackRegister)expr.internalFunction.parameters[1].stack).register = HandleOperand(operand2);
            alloc.Lock(((Expr.StackRegister)expr.internalFunction.parameters[0].stack).register);

            foreach (var bodyExpr in expr.internalFunction.block)
            {
                bodyExpr.Accept(this);
            }

            expr.internalFunction.parameters[0].stack.stackRegister = false;
            expr.internalFunction.parameters[1].stack.stackRegister = false;

            if (((InlineStateInlined)inlineState).inlineLabelIdx != -1)
            {
                emit(new Instruction.LocalProcedure(CreateConditionalLabel(((InlineStateInlined)inlineState).inlineLabelIdx)));
            }
            alloc.Free(operand1, true);
            alloc.Free(operand2, true);

            Instruction.Register tmp2 = ((InlineStateInlined)inlineState).callee;

            inlineState = inlineState.lastState;

            tmp2 = tmp2 ?? (expr.internalFunction.modifiers["unsafe"] ? ((Expr.StackRegister)expr.internalFunction.parameters[0].stack).register : null);

            if (tmp2 != null)
            {
                return alloc.GetRegister(alloc.NameToIdx(tmp2.name), tmp2.size);
            }
            return tmp2;
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

            bool instance = !expr.internalFunction.modifiers["static"];

            var args = new Instruction.Value[expr.arguments.Count + Convert.ToInt16(instance)];

            if (!expr.internalFunction.modifiers["static"])
            {
                alloc.Lock((Instruction.Register)(args[0] = alloc.CurrentRegister(Instruction.Register.RegisterSize._64Bits)));
                if (!expr.constructor)
                {
                    if (expr.callee != null)
                    {
                        for (int i = expr.offsets.Length - 1; i >= 1; i--)
                        {
                            emit(new Instruction.Binary("MOV", args[0], new Instruction.Pointer(((i == expr.offsets.Length - 1) ? new(Instruction.Register.RegisterName.RBP, Instruction.Register.RegisterSize._64Bits) : alloc.CurrentRegister(Instruction.Register.RegisterSize._64Bits)), expr.offsets[i].stackOffset, 8)));
                        }
                        emit(new Instruction.Binary("MOV", args[0], new Instruction.Pointer((0 == expr.offsets.Length - 1) ? new(Instruction.Register.RegisterName.RBP, Instruction.Register.RegisterSize._64Bits) : alloc.CurrentRegister(Instruction.Register.RegisterSize._64Bits), expr.offsets[0].stackOffset, 8)));
                    }
                    else
                    {
                        emit(new Instruction.Binary("MOV", args[0], new Instruction.Pointer(8, 8)));
                    }
                }
                else
                {
                    emit(new Instruction.Binary("MOV", args[0], new Instruction.Register(Instruction.Register.RegisterName.RBX, Instruction.Register.RegisterSize._64Bits)));
                }
            }


            for (int i = 0; i < expr.arguments.Count; i++)
            {
                Instruction.Value arg = expr.arguments[i].Accept(this);

                args[i + Convert.ToInt16(instance)] = arg;

                expr.internalFunction.parameters[i].stack.stackRegister = true;
                ((Expr.StackRegister)expr.internalFunction.parameters[i].stack).register = HandleOperand(arg);
                alloc.Lock(((Expr.StackRegister)expr.internalFunction.parameters[i].stack).register);
            }

            foreach (var bodyExpr in expr.internalFunction.block)
            {
                bodyExpr.Accept(this);
            }

            if (instance)
            {
                alloc.Unlock((Instruction.Register)args[0]);
                alloc.Free(args[0]);
            }

            for (int i = 0; i < expr.arguments.Count; i++)
            {
                expr.internalFunction.parameters[i].stack.stackRegister = false;

                alloc.Free(args[i + Convert.ToInt16(instance)], true);
            }

            if (((InlineStateInlined)inlineState).inlineLabelIdx != -1)
            {
                emit(new Instruction.LocalProcedure(CreateConditionalLabel(((InlineStateInlined)inlineState).inlineLabelIdx)));
            }

            var tmp2 = ((InlineStateInlined)inlineState).callee;

            inlineState = inlineState.lastState;

            tmp2 = tmp2 ?? (expr.internalFunction.modifiers["unsafe"] ? ((Expr.StackRegister)expr.internalFunction.parameters[0].stack).register : null);

            if (tmp2 != null)
            {
                return alloc.GetRegister(alloc.NameToIdx(tmp2.name), tmp2.size);
            }
            return tmp2;
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
                    ((InlineStateInlined)inlineState).callee = HandleOperand(operand);
                }
                else
                {
                    if (operand.IsRegister())
                    {
                        var op = (Instruction.Register)operand;
                        if (op.name != ((InlineStateInlined)inlineState).callee.name)
                            emit(new Instruction.Binary("MOV", new Instruction.Register(((InlineStateInlined)inlineState).callee.name, op.size), operand));
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
    }
}
