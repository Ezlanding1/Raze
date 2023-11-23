using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Assembler
{
    internal partial class Encoder
    {
        private partial class Encoding
        {
            public Instruction GenerateInstruction(Operand op1, Operand op2, AssemblyExpr op1Expr, AssemblyExpr op2Expr)
            {
                List<IInstruction> instructions = new();

                if (EncodingUtils.SetRexW(encodingType))
                {
                    instructions.Add(new Instruction.RexPrefix(0b1000));
                }
                else if (EncodingUtils.SetRex(encodingType))
                {
                    instructions.Add(new Instruction.RexPrefix(0b0000));
                }

                if (EncodingUtils.SetSizePrefix(encodingType))
                    instructions.Add(new Instruction.InstructionOpCodeSizePrefix());

                instructions.Add(new Instruction.InstructionOpCode(OpCode));

                return op1.operandType switch
                {
                    _ when EncodingUtils.IsRegister(op1) => EncodeRegister(instructions, op1, op2, op1Expr, op2Expr),
                    Operand.OperandType.M => EncodeMemory(instructions, op1, op2, op1Expr, op2Expr),
                    _ => EncodingUtils.EncodingError()
                };
            }

            private Instruction EncodeRegister(List<IInstruction> instructions, Operand op1, Operand op2, AssemblyExpr op1Expr, AssemblyExpr op2Expr)
            {
                if (EncodingUtils.IsRegister(op2))
                {
                    instructions.Add(new Instruction.ModRegRm(Assembler.Instruction.ModRegRm.Mod.RegisterAdressingMode,
                        ExprRegisterToModRegRmRegister((AssemblyExpr.Register)op2Expr),
                        ExprRegisterToModRegRmRegister((AssemblyExpr.Register)op1Expr)
                    ));
                }
                else if (op2.operandType == Operand.OperandType.M)
                {
                    var ptr = (AssemblyExpr.Pointer)op2Expr;

                    if (ptr.register.size == AssemblyExpr.Register.RegisterSize._32Bits)
                    {
                        instructions.Insert(0, new Instruction.AddressSizeOverridePrefix());
                    }
                    else if (ptr.register.size != AssemblyExpr.Register.RegisterSize._64Bits)
                    {
                        return EncodingUtils.EncodingError();
                    }

                    if (ptr.offset == 0 && EncodingUtils.CanHaveZeroByteDisplacement(ptr.register))
                    {
                        instructions.Add(new Instruction.ModRegRm(Assembler.Instruction.ModRegRm.Mod.ZeroByteDisplacement,
                            ExprRegisterToModRegRmRegister((AssemblyExpr.Register)op1Expr),
                            ExprRegisterToModRegRmRegister(ptr.register)
                        ));
                    }
                    else
                    {
                        instructions.Add(new Instruction.ModRegRm(EncodingUtils.GetDispSize(ptr.offset),
                            ExprRegisterToModRegRmRegister((AssemblyExpr.Register)op1Expr),
                            ExprRegisterToModRegRmRegister(ptr.register)
                        ));
                        instructions.Add(EncodingUtils.GetDispInstruction(ptr.offset));
                    }
                }
                else if (op2.operandType == Operand.OperandType.IMM)
                {
                    instructions.Add(new Instruction.ModRegRm(Assembler.Instruction.ModRegRm.Mod.RegisterAdressingMode,
                        (Instruction.ModRegRm.OpCodeExtension)OpCodeExtension,
                        ExprRegisterToModRegRmRegister((AssemblyExpr.Register)op1Expr)
                    ));

                    instructions.Add(EncodingUtils.GetImmInstruction(this.operands[1].size, (AssemblyExpr.Literal)op2Expr));
                }
                else
                {
                    return EncodingUtils.EncodingError();
                }
                return new Instruction(instructions.ToArray());
            }

            private Instruction EncodeMemory(List<IInstruction> instructions, Operand op1, Operand op2, AssemblyExpr op1Expr, AssemblyExpr op2Expr)
            {
                var ptr = (AssemblyExpr.Pointer)op1Expr;

                if (ptr.register.size == AssemblyExpr.Register.RegisterSize._32Bits)
                {
                    instructions.Insert(0, new Instruction.AddressSizeOverridePrefix());
                }
                else if (ptr.register.size != AssemblyExpr.Register.RegisterSize._64Bits)
                {
                    return EncodingUtils.EncodingError();
                }

                if (EncodingUtils.IsRegister(op2))
                {
                    if (ptr.offset == 0 && EncodingUtils.CanHaveZeroByteDisplacement(ptr.register))
                    {
                        instructions.Add(new Instruction.ModRegRm(Assembler.Instruction.ModRegRm.Mod.ZeroByteDisplacement,
                            ExprRegisterToModRegRmRegister((AssemblyExpr.Register)op2Expr),
                            ExprRegisterToModRegRmRegister(ptr.register)
                        ));
                    }
                    else
                    {
                        instructions.Add(new Instruction.ModRegRm(EncodingUtils.GetDispSize(ptr.offset),
                            ExprRegisterToModRegRmRegister((AssemblyExpr.Register)op2Expr),
                            ExprRegisterToModRegRmRegister(ptr.register)
                        ));
                        instructions.Add(EncodingUtils.GetDispInstruction(ptr.offset));
                    }
                }
                else if (op2.operandType == Operand.OperandType.M)
                {
                    return EncodingUtils.EncodingError();
                }
                else if (op2.operandType == Operand.OperandType.IMM)
                {
                    if (ptr.offset == 0 && EncodingUtils.CanHaveZeroByteDisplacement(ptr.register))
                    {
                        instructions.Add(new Instruction.ModRegRm(Assembler.Instruction.ModRegRm.Mod.ZeroByteDisplacement,
                            (Instruction.ModRegRm.OpCodeExtension)OpCodeExtension,
                            ExprRegisterToModRegRmRegister(ptr.register)
                        ));
                    }
                    else
                    {
                        instructions.Add(new Instruction.ModRegRm(EncodingUtils.GetDispSize(ptr.offset),
                            (Instruction.ModRegRm.OpCodeExtension)OpCodeExtension,
                            ExprRegisterToModRegRmRegister(ptr.register)
                        ));
                        instructions.Add(EncodingUtils.GetDispInstruction(ptr.offset));
                    }
                    instructions.Add(EncodingUtils.GetImmInstruction(this.operands[1].size, (AssemblyExpr.Literal)op2Expr));
                }
                else
                {
                    return EncodingUtils.EncodingError();
                }
                return new Instruction(instructions.ToArray());
            }
        }
    }
}