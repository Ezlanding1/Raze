using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Assembler
{
    public partial class Encoder
    {
        internal partial class Encoding
        {
            public static Operand[] GetOperandsFromInstruction(string instruction)
            {
                var operandsStrings = instruction.Split(' ', 2);

                if (operandsStrings.Length < 2)
                {
                    return Array.Empty<Operand>();
                }

                operandsStrings = operandsStrings[1].Split(", ");

                var operands = new Operand[operandsStrings.Length];

                for (int i = 0; i < operandsStrings.Length; i++)
                {
                    int subStrIdx = 0;
                    while (subStrIdx < operandsStrings[i].Length && char.IsLetter(operandsStrings[i][subStrIdx]))
                    {
                        subStrIdx++;
                    }

                    operands[i] = operandsStrings[i][..subStrIdx].ToUpper() switch
                    {
                        "R" => new Operand(Operand.OperandType.R, ToSize(operandsStrings[i], subStrIdx)),
                        "M" => new Operand(Operand.OperandType.M, ToSize(operandsStrings[i], subStrIdx)),
                        "RM" => new Operand(Operand.OperandType.R | Operand.OperandType.M, ToSize(operandsStrings[i], subStrIdx)),
                        "IMM" => new Operand(Operand.OperandType.IMM, ToSize(operandsStrings[i], subStrIdx)),
                        "AL" => new Operand(Operand.OperandType.A, ConstantSize(operandsStrings[i], subStrIdx, Operand.OperandSize._8Bits)),
                        "AX" => new Operand(Operand.OperandType.A, ConstantSize(operandsStrings[i], subStrIdx, Operand.OperandSize._16Bits)),
                        "EAX" => new Operand(Operand.OperandType.A, ConstantSize(operandsStrings[i], subStrIdx, Operand.OperandSize._32Bits)),
                        "RAX" => new Operand(Operand.OperandType.A, ConstantSize(operandsStrings[i], subStrIdx, Operand.OperandSize._64Bits)),
                        "MOFFS" => new Operand(Operand.OperandType.MOFFS, ToSize(operandsStrings[i], subStrIdx)),
                        "XMM" => new Operand(Operand.OperandType.XMM, ConstantSize(operandsStrings[i], subStrIdx, Operand.OperandSize._128Bits)),
                        _ => throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic($"Invalid Encoding Type '{instruction}'")),
                    };
                }
                return operands;

                Operand.OperandSize ConstantSize(string sizeStr, int start, Operand.OperandSize size)
                {
                    if (sizeStr.Length > start)
                    {
                        throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic($"Invalid Encoding Type '{instruction}'"));
                    }
                    return size;
                }

                Operand.OperandSize ToSize(string sizeStr, int start)
                {
                    return sizeStr[start..] switch
                    {
                        "64" => Operand.OperandSize._64Bits,
                        "32" => Operand.OperandSize._32Bits,
                        "16" => Operand.OperandSize._16Bits,
                        "8" => Operand.OperandSize._8Bits,
                        _ => throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic($"Invalid Encoding Type '{instruction}'")),
                    };
                }
            }
        }
    }
}
