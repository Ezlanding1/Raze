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
            public string Instruction
            {
                set
                {
                    var operandsStrings = value.Split(' ', 2)[1].Split(", ");
                    operands = new Operand[operandsStrings.Length];

                    for (int i = 0; i < operandsStrings.Length; i++)
                    {
                        int subStrIdx = 0;
                        while (subStrIdx < operandsStrings[i].Length && char.IsLetter(operandsStrings[i][subStrIdx]))
                        {
                            subStrIdx++;
                        }

                        switch (operandsStrings[i].Substring(0, subStrIdx).ToUpper())
                        {
                            case "R":
                                operands[i] = new Operand(Operand.OperandType.R, ToSize(operandsStrings[i], subStrIdx));
                                break;
                            case "M":
                                operands[i] = new Operand(Operand.OperandType.M, ToSize(operandsStrings[i], subStrIdx));
                                break;
                            case "RM":
                                operands[i] = new Operand(Operand.OperandType.R | Operand.OperandType.M, ToSize(operandsStrings[i], subStrIdx));
                                break;
                            case "IMM":
                                operands[i] = new Operand(Operand.OperandType.IMM, ToSize(operandsStrings[i], subStrIdx));
                                break;
                            case "AL":
                                operands[i] = new Operand(Operand.OperandType.A, Operand.OperandSize._8Bits);
                                break;
                            case "AX":
                                operands[i] = new Operand(Operand.OperandType.A, Operand.OperandSize._16Bits);
                                break;
                            case "EAX":
                                operands[i] = new Operand(Operand.OperandType.A, Operand.OperandSize._32Bits);
                                break;
                            case "RAX":
                                operands[i] = new Operand(Operand.OperandType.A, Operand.OperandSize._64Bits);
                                break;
                            case "P":
                                operands[i] = new Operand(Operand.OperandType.P, Operand.OperandSize._8Bits);
                                break;
                            default: Diagnostics.errors.Push(new Error.ImpossibleError($"Invalid Encoding Type '{value}'")); break;
                        };
                    }

                    Operand.OperandSize ToSize(string size, int start)
                    {
                        switch (size.Substring(start, size.Length - start))
                        {
                            case "64": return Operand.OperandSize._64Bits;
                            case "32": return Operand.OperandSize._32Bits;
                            case "16": return Operand.OperandSize._16Bits;
                            case "8": return Operand.OperandSize._8Bits;
                            case "8U": return Operand.OperandSize._8BitsUpper;
                            default: Diagnostics.errors.Push(new Error.ImpossibleError($"Invalid Encoding Type '{value}'")); return 0;
                        }
                    }
                }
            }
            private Operand[] operands;

            public string EncodingType
            {
                set
                {
                    if (!Enum.TryParse(value.Replace('|', ','), out encodingType))
                    {
                        Diagnostics.errors.Push(new Error.ImpossibleError($"Invalid Encoding Type {value}"));
                    }
                }
            }
            private EncodingTypes encodingType = Encoding.EncodingTypes.None;

            public byte OpCodeExtension { get; set; }
            public byte OpCode { get; set; }

            public bool Matches(params Operand[] operands)
            {
                if (this.operands.Length != operands.Length)
                {
                    return false;
                }
                for (int i = 0; i < operands.Length; i++)
                {
                    if (!operands[i].Matches(this.operands[i]))
                    {
                        return false;
                    }
                }
                return true;
            }

            public static Operand ToEncodingType(AssemblyExpr assemblyExpr)
            {
                if (assemblyExpr is AssemblyExpr.Value value)
                {
                    Operand operand;

                    if (value.IsLiteral())
                    {
                        operand = new(Operand.OperandType.IMM, CodeGen.SizeOfLiteral((AssemblyExpr.Literal)value));
                    }
                    else if (value.IsRegister())
                    {
                        operand = new(RegisterOperandType((AssemblyExpr.Register)value), (int)((AssemblyExpr.SizedValue)value).size);
                    }
                    else
                    {
                        operand = new(Operand.OperandType.M, (int)((AssemblyExpr.SizedValue)value).size);
                        ThrowTMP(((AssemblyExpr.Pointer)value).register);
                    }
                    return operand;
                }
                Diagnostics.errors.Push(new Error.ImpossibleError("Invalid/Unsupported Instruction"));
                return new();
            }

            private static Operand.OperandType RegisterOperandType(AssemblyExpr.Register reg)
            {
                ThrowTMP(reg);

                return reg.name switch
            {
                AssemblyExpr.Register.RegisterName.RAX => Operand.OperandType.A,
                AssemblyExpr.Register.RegisterName.RSP => Operand.OperandType.P,
                AssemblyExpr.Register.RegisterName.RBP => Operand.OperandType.P,
                AssemblyExpr.Register.RegisterName.RSI => Operand.OperandType.P,
                AssemblyExpr.Register.RegisterName.RDI => Operand.OperandType.P,
                _ => Operand.OperandType.R
            };
        }

            private static void ThrowTMP(AssemblyExpr.Register reg)
            {
                if (reg.name == AssemblyExpr.Register.RegisterName.TMP)
                {
                    Diagnostics.errors.Push(new Error.ImpossibleError("TMP Register Emitted"));
                }
            }
        }
    }
}