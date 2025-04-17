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


                if (op2.IsLiteral())
                {
                    var reg = codeGen.alloc.CurrentRegister(op2.Size);
                    codeGen.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, reg, op2));
                    codeGen.Emit(new AssemblyExpr.Binary(binary.instruction, op1, reg));
                }
                else
                {
                    codeGen.Emit(new AssemblyExpr.Binary(binary.instruction, op1, op2));
                }

                ReturnOperand(codeGen, binary, binary.operand1, op1);
                DeallocVariables(codeGen, (binary.operand1, op1), (binary.operand2, op2));
            }

            public static void MUL_IDIV_DIV(CodeGen codeGen, Expr.InlineAssembly.BinaryInstruction binary)
            {
                var op1 = binary.operand1.ToOperand(codeGen, AssemblyExpr.Register.RegisterSize._32Bits);

                op1 = op1.NonPointerNonLiteral(codeGen, null);
                var op2 = binary.operand2.ToOperand(codeGen, op1.Size).NonLiteral(codeGen, null);

                codeGen.alloc.Free(op1);
                codeGen.alloc.SetSuggestedRegister(op1, AssemblyExpr.Register.RegisterName.RAX);

                var rax = codeGen.alloc.ReserveRegister(AssemblyExpr.Register.RegisterName.RAX, op1.Size);
                var rdx = codeGen.alloc.ReserveRegister(AssemblyExpr.Register.RegisterName.RDX, op1.Size);

                codeGen.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, rax, op1));

                codeGen.Emit(binary.instruction == AssemblyExpr.Instruction.DIV ?
                    new AssemblyExpr.Binary(AssemblyExpr.Instruction.XOR, rdx, rdx) :
                    new AssemblyExpr.Nullary(AssemblyExpr.Instruction.CDQ)
                );

                codeGen.Emit(new AssemblyExpr.Unary(binary.instruction, op2));

                codeGen.alloc.Free(rax);
                codeGen.alloc.Free(rdx);

                DeallocVariables(codeGen, (binary.operand1, op1), (binary.operand2, op2));
            }

            public static void MOVSX(CodeGen codeGen, Expr.InlineAssembly.BinaryInstruction binary)
            {
                AssemblyExpr.IValue op1 = binary.operand1.ToOperand(codeGen, AssemblyExpr.Register.RegisterSize._32Bits).NonLiteral(codeGen, binary.operand1.Type());
                var op2 = ValidateOperand2(codeGen, binary.operand1, ref op1, binary.operand2, binary.operand2.ToOperand(codeGen, op1.Size));

                var instruction = op2.IsLiteral() ? 
                    AssemblyExpr.Instruction.MOV :
                    (op1.Size, op2.Size) switch
                    {
                        (AssemblyExpr.Register.RegisterSize._16Bits, AssemblyExpr.Register.RegisterSize._16Bits) or
                        (AssemblyExpr.Register.RegisterSize._32Bits, AssemblyExpr.Register.RegisterSize._32Bits) or
                        (AssemblyExpr.Register.RegisterSize._64Bits, AssemblyExpr.Register.RegisterSize._32Bits)
                            => AssemblyExpr.Instruction.MOVSXD,
                        _ => AssemblyExpr.Instruction.MOVSX
                    };

                codeGen.Emit(new AssemblyExpr.Binary(instruction, op1, op2));

                ReturnOperand(codeGen, binary, binary.operand1, op1);
                DeallocVariables(codeGen, (binary.operand1, op1), (binary.operand2, op2));
            }

            public static void SAL_SAR(CodeGen codeGen, Expr.InlineAssembly.BinaryInstruction binary)
            {
                var op1 = binary.operand1.ToOperand(codeGen, AssemblyExpr.Register.RegisterSize._32Bits).NonPointerNonLiteral(codeGen, binary.operand1.Type());
                var op2 = binary.operand2.ToOperand(codeGen, op1.Size);

                if (!op2.IsLiteral())
                {
                    codeGen.alloc.Free(op2);
                    codeGen.alloc.SetSuggestedRegister(op2, AssemblyExpr.Register.RegisterName.RCX);

                    var cl = codeGen.alloc.ReserveRegister(AssemblyExpr.Register.RegisterName.RCX, AssemblyExpr.Register.RegisterSize._8Bits);

                    codeGen.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, cl, op2));
                    codeGen.Emit(new AssemblyExpr.Binary(binary.instruction, op1, cl));
                }
                else
                {
                    codeGen.Emit(new AssemblyExpr.Binary(binary.instruction, op1, op2));
                }

                if (op2.Size != AssemblyExpr.Register.RegisterSize._8Bits)
                {
                    Diagnostics.Report(new Diagnostic.BackendDiagnostic(Diagnostic.DiagnosticName.InstructionOperandsSizeMismatch));
                }

                ReturnOperand(codeGen, binary, binary.operand1, op1);
                DeallocVariables(codeGen, (binary.operand1, op1), (binary.operand2, op2));
            }

            public static void CAST(CodeGen codeGen, Expr.InlineAssembly.BinaryInstruction binary)
            {
                AssemblyExpr.IValue op1 = binary.operand1.ToOperand(codeGen, AssemblyExpr.Register.RegisterSize._32Bits).NonLiteral(codeGen, binary.operand1.Type());
                var op2 = ValidateOperand2(codeGen, binary.operand1, ref op1, binary.operand2, binary.operand2.ToOperand(codeGen, op1.Size));

                codeGen.Emit(new AssemblyExpr.Binary(binary.instruction, op1, op2));

                ReturnOperand(codeGen, binary, binary.operand1, op1);
                DeallocVariables(codeGen, (binary.operand1, op1), (binary.operand2, op2));
            }

            // This method could be generalized to 'DefaultPackedFloatingInstruction' if needed
            // Note: The Raze compiler will not align stack memory for this method! (The data section will be aligned, however)
            // Please use the XORPS/XORPD instructions carefully to prevent alignment issues
            public static void XORPS_XORPD(CodeGen codeGen, Expr.InlineAssembly.BinaryInstruction binary)
            {
                AssemblyExpr.IValue op1 = binary.operand1.ToOperand(codeGen, AssemblyExpr.Register.RegisterSize._32Bits).NonPointerNonLiteral(codeGen, binary.operand1.Type());
                
                int dataSectionSize = codeGen.currentDataSectionSize;
                var op2 = binary.operand2.ToOperand(codeGen, op1.Size);
                
                if (op2.IsPointer(out var ptr) && ptr.offset.type == AssemblyExpr.Literal.LiteralType.RefData)
                {
                    ((AssemblyExpr.IRegisterPointer)op2).Size = op1.Size;

                    // Align data section to 16-byte boundary
                    int numPadBytes = 16 - (dataSectionSize % 16);
                    if (numPadBytes != 16)
                    {
                        IEnumerable<byte[]> padBytes = Enumerable.Range(0, numPadBytes)
                            .Select(_ => new byte[1]);

                        codeGen.assembly.data.Insert(codeGen.assembly.data.Count - 1,
                            new AssemblyExpr.Data(null,
                                AssemblyExpr.Literal.LiteralType.Integer,
                                padBytes
                            )
                        );
                        codeGen.currentDataSectionSize += numPadBytes;
                    }
                }

                codeGen.Emit(new AssemblyExpr.Binary(binary.instruction, op1, op2));

                ReturnOperand(codeGen, binary, binary.operand1, op1);
                DeallocVariables(codeGen, (binary.operand1, op1), (binary.operand2, op2));
            }

        }
    }
}
