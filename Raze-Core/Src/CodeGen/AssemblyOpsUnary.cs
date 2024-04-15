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
        public static AssemblyExpr.Value CreateOperand(AssemblyOps assemblyOps)
        {
            var operand = assemblyOps.vars[assemblyOps.count++].Item2.Accept(assemblyOps.assembler);

            if (assemblyOps.vars[assemblyOps.count - 1].Item1 != (AssemblyExpr.Register.RegisterSize)(-1))
            {
                if (operand.IsLiteral())
                {
                    operand = ((AssemblyExpr.ILiteralBase)operand).CreateLiteral(assemblyOps.vars[assemblyOps.count - 1].Item1);
                }
                else
                {
                    operand = ((AssemblyExpr.RegisterPointer)operand).Clone();
                    ((AssemblyExpr.RegisterPointer)operand).size = assemblyOps.vars[assemblyOps.count - 1].Item1;
                }
            }
            return operand;
        }

        public static void ReturnOp(ref AssemblyExpr.Value operand, ExprUtils.AssignableInstruction.Unary.AssignType assignType, AssemblyOps assemblyOps)
        {
            if (((InlinedCodeGen)assemblyOps.assembler).inlineState.inline)
            {
                ((InlinedCodeGen.InlineStateInlined)((InlinedCodeGen)assemblyOps.assembler).inlineState).callee = operand;
                ((InlinedCodeGen)assemblyOps.assembler).LockOperand(operand);
            }
            else
            {
                if (operand.IsRegister())
                {
                    var op = (AssemblyExpr.Register)operand;
                    if (op.Name != AssemblyExpr.Register.RegisterName.RAX)
                        assemblyOps.assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RAX, op.Size), operand));
                }
                else
                {   
                    assemblyOps.assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RAX, operand.Size), operand));
                }
            }
        }
        private static AssemblyExpr.Value HandleOperandUnsafe(ExprUtils.AssignableInstruction.Unary instruction, AssemblyOps assemblyOps)
        {
            return instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Unary.AssignType.AssignFirst) ?
                CreateOperand(assemblyOps).IfLiteralCreateLiteral(InstructionUtils.ToRegisterSize(assemblyOps.vars[assemblyOps.count - 1].Item2.GetLastData().size)) :
                instruction.instruction.operand.IfLiteralCreateLiteral(AssemblyExpr.Register.RegisterSize._64Bits);
        }
        private static AssemblyExpr.Value HandleOperand(ExprUtils.AssignableInstruction.Unary instruction, AssemblyOps assemblyOps)
        {
            return HandleOperandUnsafe(instruction, assemblyOps).NonLiteral(assemblyOps.assembler);
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
                Diagnostics.Report(new Diagnostic.BackendDiagnostic(Diagnostic.DiagnosticName.InvalidInstructionOperandType_Arity1, "DEREF", "literal"));
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

        public static void RETURN(ExprUtils.AssignableInstruction.Unary instruction, AssemblyOps assemblyOps)
        {
            AssemblyExpr.Value operand = HandleOperandUnsafe(instruction, assemblyOps);

            if (instruction.returns && assemblyOps.assembler is InlinedCodeGen)
            {
                ReturnOp(ref operand, instruction.assignType, assemblyOps);
            }

            if (instruction.assignType == ExprUtils.AssignableInstruction.Unary.AssignType.AssignFirst)
                assemblyOps.assembler.alloc.Free(operand);
        }
    }
}
