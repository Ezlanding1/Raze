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
        public static void ReturnOp(ref AssemblyExpr.Value operand, ExprUtils.AssignableInstruction.Unary.AssignType assignType, AssemblyOps assemblyOps)
        {
            if (((InlinedCodeGen)assemblyOps.assembler).inlineState.inline)
            {
                var nonLiteral = operand.NonLiteral(assemblyOps.assembler);
                ((InlinedCodeGen.InlineStateInlined)((InlinedCodeGen)assemblyOps.assembler).inlineState).callee = nonLiteral;
                ((InlinedCodeGen)assemblyOps.assembler).LockOperand(nonLiteral);
            }
            else
            {
                if (operand.IsRegister())
                {
                    var op = (AssemblyExpr.Register)operand;
                    if (op.Name != AssemblyExpr.Register.RegisterName.RAX)
                        assemblyOps.assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RAX, op.size), operand));
                }
                else
                {   
                    assemblyOps.assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RAX, operand.size), operand));
                }
            }
        }
        private static AssemblyExpr.Value HandleOperand(ExprUtils.AssignableInstruction.Unary instruction, AssemblyOps assemblyOps)
        {
            return (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Unary.AssignType.AssignFirst) ? 
                assemblyOps.vars[assemblyOps.count++].Accept(assemblyOps.assembler) : 
                instruction.instruction.operand);
        }

        public static void DefaultUnOp(ExprUtils.AssignableInstruction.Unary instruction, AssemblyOps assemblyOps)
        {
            AssemblyExpr.Value operand = HandleOperand(instruction, assemblyOps);

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
            AssemblyExpr.Value operand = HandleOperand(instruction, assemblyOps);

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
