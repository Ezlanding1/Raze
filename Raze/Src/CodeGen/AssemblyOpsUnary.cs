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
        public static AssemblyExpr.Register.RegisterSize? GetOpSize(AssemblyExpr.Value operand, ExprUtils.AssignableInstruction.Unary.AssignType assignType, List<Expr.GetReference> vars, int count)
        {
            if (operand.IsRegister() || operand.IsPointer())
            {
                return ((AssemblyExpr.SizedValue)operand).size;
            }
            if (assignType == ExprUtils.AssignableInstruction.Unary.AssignType.AssignFirst)
            {
                return InstructionUtils.ToRegisterSize(vars[count - 1].GetLastData().size);
            }

            Diagnostics.errors.Push(new Error.BackendError("Inavalid Assembly Block", $"No size could be determined for the operand"));
            return null;
        }

        public static void ReturnOp(ref AssemblyExpr.Value operand, ExprUtils.AssignableInstruction.Unary.AssignType assignType, AssemblyOps assemblyOps)
        {
            if (((InlinedCodeGen)assemblyOps.assembler).inlineState.inline)
            {
                operand = operand.NonLiteral(GetOpSize(operand, assignType, assemblyOps.vars, assemblyOps.count), assemblyOps.assembler);
                ((InlinedCodeGen.InlineStateInlined)((InlinedCodeGen)assemblyOps.assembler).inlineState).callee = (AssemblyExpr.SizedValue)operand;
                ((InlinedCodeGen)assemblyOps.assembler).LockOperand((AssemblyExpr.SizedValue)operand);
            }
            else
            {
                if (operand.IsRegister())
                {
                    var op = (AssemblyExpr.Register)operand;
                    if (op.Name != AssemblyExpr.Register.RegisterName.RAX)
                        assemblyOps.assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RAX, op.size), operand));
                }
                else if (operand.IsPointer())
                {
                    assemblyOps.assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RAX, ((AssemblyExpr.SizedValue)operand).size), operand));
                }
                else
                {
                    var size = GetOpSize(operand, assignType, assemblyOps.vars, assemblyOps.count);
                    if (size != null)
                    {
                        assemblyOps.assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RAX, (AssemblyExpr.Register.RegisterSize)size), operand));
                    }
                }
            }
        }

        public static void DefaultUnOp(ExprUtils.AssignableInstruction.Unary instruction, AssemblyOps assemblyOps)
        {
            AssemblyExpr.Value operand = (AssemblyExpr.Value)(instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Unary.AssignType.AssignFirst) ? assemblyOps.vars[assemblyOps.count++].Accept(assemblyOps.assembler) : instruction.instruction.operand);

            if (instruction.returns && assemblyOps.assembler is InlinedCodeGen)
            {
                ReturnOp(ref operand, instruction.assignType, assemblyOps);
            }

            assemblyOps.assembler.Emit(new AssemblyExpr.Unary(instruction.instruction.instruction, operand));

            if (instruction.assignType == ExprUtils.AssignableInstruction.Unary.AssignType.AssignFirst)
                assemblyOps.assembler.alloc.Free(operand);
        }

        public static void DEREF(ExprUtils.AssignableInstruction.Unary instruction, AssemblyOps assemblyOps)
        {
            AssemblyExpr.Value operand = (AssemblyExpr.Value)(instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Unary.AssignType.AssignFirst) ? assemblyOps.vars[assemblyOps.count++].Accept(assemblyOps.assembler) : instruction.instruction.operand);

            if (operand.IsLiteral())
            {
                Diagnostics.errors.Push(new Error.BackendError("Invalid Assembly Instruction", "'DEREF' instruction's operand may not be literal"));
                return;
            }

            AssemblyExpr.Value deref = new AssemblyExpr.Pointer((AssemblyExpr.Register)operand.NonPointer(assemblyOps.assembler), 0, 1);

            if (instruction.returns && assemblyOps.assembler is InlinedCodeGen)
            {
                ReturnOp(ref deref, instruction.assignType, assemblyOps);
            }

            assemblyOps.assembler.alloc.Free(deref);

            if (instruction.assignType == ExprUtils.AssignableInstruction.Unary.AssignType.AssignFirst)
                assemblyOps.assembler.alloc.Free(operand);
        }
    }
}
