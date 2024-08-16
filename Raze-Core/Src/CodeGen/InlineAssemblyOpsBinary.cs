using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public abstract partial class CodeGen
{
    internal static partial class InlineAssemblyOps
    {
        internal static class Binary
        {
            public static void DefaultInstruction(CodeGen codeGen, Expr.InlineAssembly.BinaryInstruction binary)
            {
                AssemblyExpr.IValue op1 = binary.operand1.ToOperand(codeGen, AssemblyExpr.Register.RegisterSize._32Bits).NonLiteral(codeGen, binary.operand1.Type());
                var op2 = ValidateOperand2(codeGen, binary.operand1, ref op1, binary.operand2, binary.operand2.ToOperand(codeGen, op1.Size));

                codeGen.Emit(new AssemblyExpr.Binary(binary.instruction, op1, op2));

                ReturnOperand(codeGen, binary, binary.operand1, op1);
                DeallocVariables(codeGen, (binary.operand1, op1), (binary.operand2, op2));
            }
            
            public static void DefaultFloatingInstruction(CodeGen codeGen, Expr.InlineAssembly.BinaryInstruction binary)
            {
                AssemblyExpr.IValue op1 = binary.operand1.ToOperand(codeGen, AssemblyExpr.Register.RegisterSize._32Bits).NonPointerNonLiteral(codeGen, binary.operand1.Type());
                var op2 = binary.operand2.ToOperand(codeGen, op1.Size);

                codeGen.Emit(new AssemblyExpr.Binary(binary.instruction, op1, op2));

                ReturnOperand(codeGen, binary, binary.operand1, op1);
                DeallocVariables(codeGen, (binary.operand1, op1), (binary.operand2, op2));
            }

            public static void IMUL(CodeGen codeGen, Expr.InlineAssembly.BinaryInstruction binary)
            {
                AssemblyExpr.IValue op1 = binary.operand1.ToOperand(codeGen, AssemblyExpr.Register.RegisterSize._32Bits).NonPointerNonLiteral(codeGen, binary.operand1.Type());
                var op2 = ValidateOperand2(codeGen, binary.operand1, ref op1, binary.operand2, binary.operand2.ToOperand(codeGen, op1.Size));

                codeGen.Emit(new AssemblyExpr.Binary(binary.instruction, op1, op2));

                ReturnOperand(codeGen, binary, binary.operand1, op1);
                DeallocVariables(codeGen, (binary.operand1, op1), (binary.operand2, op2));
            }
            
            public static void IDIV_DIV(CodeGen codeGen, Expr.InlineAssembly.BinaryInstruction binary)
            {
                var op1 = binary.operand1.ToOperand(codeGen, AssemblyExpr.Register.RegisterSize._32Bits);

                if (op1 is not AssemblyExpr.IRegisterPointer rp || rp.GetRegister().Name != AssemblyExpr.Register.RegisterName.RAX)
                {
                    codeGen.alloc.ReserveRegister(codeGen, AssemblyExpr.Register.RegisterName.RAX);
                }

                op1 = op1.NonPointerNonLiteral(codeGen, null);
                var op2 = binary.operand2.ToOperand(codeGen, op1.Size).NonLiteral(codeGen, null);

                var rax = codeGen.alloc.GetRegister(AssemblyExpr.Register.RegisterName.RAX, op1.Size);
                codeGen.alloc.ReserveRegister(codeGen, AssemblyExpr.Register.RegisterName.RDX);
                var rdx = codeGen.alloc.GetRegister(AssemblyExpr.Register.RegisterName.RDX, op1.Size);

                if (!(op1.IsRegister(out var register) && register.Name == AssemblyExpr.Register.RegisterName.RAX))
                {
                    codeGen.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, rax, op1));
                }

                codeGen.Emit(binary.instruction == AssemblyExpr.Instruction.DIV ?
                    new AssemblyExpr.Binary(AssemblyExpr.Instruction.XOR, rdx, rdx) :
                    new AssemblyExpr.Nullary(AssemblyExpr.Instruction.CDQ)
                );

                codeGen.Emit(new AssemblyExpr.Unary(binary.instruction, op2));

                codeGen.alloc.FreeRegister(rax);
                codeGen.alloc.FreeRegister(rdx);

                DeallocVariables(codeGen, (binary.operand1, op1), (binary.operand2, op2));
            }

            public static void SAL_SAR(CodeGen codeGen, Expr.InlineAssembly.BinaryInstruction binary)
            {
                var op1 = binary.operand1.ToOperand(codeGen, AssemblyExpr.Register.RegisterSize._32Bits).NonPointerNonLiteral(codeGen, binary.operand1.Type());
                var op2 = binary.operand2.ToOperand(codeGen, op1.Size);

                if (!op2.IsLiteral())
                {
                    codeGen.alloc.ReserveRegister(codeGen, AssemblyExpr.Register.RegisterName.RCX);
                    var cl = new AssemblyExpr.Register(InstructionUtils.paramRegister[3], AssemblyExpr.Register.RegisterSize._8Bits);

                    if (op2.Size != AssemblyExpr.Register.RegisterSize._8Bits)
                    {
                        Diagnostics.Report(new Diagnostic.BackendDiagnostic(Diagnostic.DiagnosticName.InstructionOperandsSizeMismatch));
                    }

                    codeGen.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, cl, op2));
                    codeGen.Emit(new AssemblyExpr.Binary(binary.instruction, op1, cl));
                }
                else
                {
                    codeGen.Emit(new AssemblyExpr.Binary(binary.instruction, op1, op2));
                }

                ReturnOperand(codeGen, binary, binary.operand1, op1);
                DeallocVariables(codeGen, (binary.operand1, op1), (binary.operand2, op2));
            }
        }
    }
}
