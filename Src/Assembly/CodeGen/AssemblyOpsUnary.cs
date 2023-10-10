using System;
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
                return InstructionUtils.ToRegisterSize(vars[count - 1].GetLastData().size);
            }

            Diagnostics.errors.Push(new Error.BackendError("Inavalid Assembly Block", $"No size could be determined for the operand"));
            return null;
        }

        public static void ReturnOp(ref Instruction.Value operand, ExprUtils.AssignableInstruction.Unary.AssignType assignType, AssemblyOps assemblyOps)
        {
            if (((InlinedAssembler)assemblyOps.assembler).inlineState.inline)
            {
                operand = assemblyOps.assembler.NonLiteral(operand, GetOpSize(operand, assignType, assemblyOps.vars, assemblyOps.count));
                ((InlinedAssembler.InlineStateInlined)((InlinedAssembler)assemblyOps.assembler).inlineState).callee = (Instruction.SizedValue)operand;
                ((InlinedAssembler)assemblyOps.assembler).LockOperand((Instruction.SizedValue)operand);
            }
            else
            {
                if (operand.IsRegister())
                {
                    var op = (Instruction.Register)operand;
                    if (op.name != Instruction.Register.RegisterName.RAX)
                        assemblyOps.assembler.Emit(new Instruction.Binary("MOV", new Instruction.Register(Instruction.Register.RegisterName.RAX, op.size), operand));
                }
                else if (operand.IsPointer())
                {
                    assemblyOps.assembler.Emit(new Instruction.Binary("MOV", new Instruction.Register(Instruction.Register.RegisterName.RAX, ((Instruction.SizedValue)operand).size), operand));
                }
                else
                {
                    var size = GetOpSize(operand, assignType, assemblyOps.vars, assemblyOps.count);
                    if (size != null)
                    {
                        assemblyOps.assembler.Emit(new Instruction.Binary("MOV", new Instruction.Register(Instruction.Register.RegisterName.RAX, (Instruction.Register.RegisterSize)size), operand));
                    }
                }
            }
        }

        public static void DefaultUnOp(ExprUtils.AssignableInstruction.Unary instruction, AssemblyOps assemblyOps)
        {
            Instruction.Value operand = (Instruction.Value)(instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Unary.AssignType.AssignFirst) ? assemblyOps.vars[assemblyOps.count++].Accept(assemblyOps.assembler) : instruction.instruction.operand);

            if (instruction.returns && assemblyOps.assembler is InlinedAssembler)
            {
                ReturnOp(ref operand, instruction.assignType, assemblyOps);
            }

            assemblyOps.assembler.Emit(new Instruction.Unary(instruction.instruction.instruction, operand));

            if (instruction.assignType == ExprUtils.AssignableInstruction.Unary.AssignType.AssignFirst)
                assemblyOps.assembler.alloc.Free(operand);
        }

        public static void DEREF(ExprUtils.AssignableInstruction.Unary instruction, AssemblyOps assemblyOps)
        {
            Instruction.Value operand = (Instruction.Value)(instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Unary.AssignType.AssignFirst) ? assemblyOps.vars[assemblyOps.count++].Accept(assemblyOps.assembler) : instruction.instruction.operand);

            if (operand.IsLiteral())
            {
                Diagnostics.errors.Push(new Error.BackendError("Invalid Assembly Instruction", "'DEREF' instruction's operand may not be literal"));
                return;
            }

            Instruction.Value deref = new Instruction.Pointer((Instruction.Register)assemblyOps.assembler.NonPointer(operand), 0, 1);

            if (instruction.returns && assemblyOps.assembler is InlinedAssembler)
            {
                ReturnOp(ref deref, instruction.assignType, assemblyOps);
            }

            assemblyOps.assembler.alloc.Free(deref);

            if (instruction.assignType == ExprUtils.AssignableInstruction.Unary.AssignType.AssignFirst)
                assemblyOps.assembler.alloc.Free(operand);
        }
    }
}
