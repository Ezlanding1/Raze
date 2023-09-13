﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

internal partial class AssemblyOps
{
    internal class Unary
    {
        public static Instruction.Register.RegisterSize? GetOpSize(Instruction.Value operand, ExprUtils.AssignableInstruction.Unary.AssignType assignType, List<Expr.GetReference> vars, int count)
        {
            if (operand.IsRegister() || operand.IsPointer())
            {
                return ((Instruction.SizedValue)operand).size;
            }
            if (assignType == ExprUtils.AssignableInstruction.Unary.AssignType.AssignFirst)
            {
                return InstructionUtils.ToRegisterSize(((Expr.Get)vars[count - 1].getters[^1]).data.size);
            }
            return null;
        }

        public static void ReturnOp(ref Instruction.Value operand, Assembler assembler, ExprUtils.AssignableInstruction.Unary.AssignType assignType, List<Expr.GetReference> vars, int count)
        {
            operand = assembler.NonLiteral(operand, GetOpSize(operand, assignType, vars, count) ?? throw new Error.BackendError("Inavalid Assembly Block", "No size could be determined for the first operand"));
            if (((InlinedAssembler)assembler).inlineState.inline)
            {
                ((InlinedAssembler.InlineStateInlined)((InlinedAssembler)assembler).inlineState).callee = (Instruction.SizedValue)operand;
                ((InlinedAssembler)assembler).LockOperand((Instruction.SizedValue)operand);
            }
        }

        public static void DefaultUnOp(ExprUtils.AssignableInstruction.Unary instruction, AssemblyOps assemblyOps)
        {
            Instruction.Value operand = (Instruction.Value)(instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Unary.AssignType.AssignFirst) ? assemblyOps.vars[assemblyOps.count++].Accept(assemblyOps.assembler) : instruction.instruction.operand);

            if (instruction.returns && assemblyOps.assembler is InlinedAssembler)
            {
                ReturnOp(ref operand, assemblyOps.assembler, instruction.assignType, assemblyOps.vars, assemblyOps.count);
            }

            assemblyOps.assembler.Emit(new Instruction.Unary(instruction.instruction.instruction, operand));

            if (instruction.assignType == ExprUtils.AssignableInstruction.Unary.AssignType.AssignFirst)
                assemblyOps.assembler.alloc.Free(operand);
        }
    }
}
