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
                    return new Operand[0];
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

                    operands[i] = operandsStrings[i].Substring(0, subStrIdx).ToUpper() switch
                    {
                        "R" => new Operand(Operand.OperandType.R, ToSize(operandsStrings[i], subStrIdx)),
                        "M" => new Operand(Operand.OperandType.M, ToSize(operandsStrings[i], subStrIdx)),
                        "RM" => new Operand(Operand.OperandType.R | Operand.OperandType.M, ToSize(operandsStrings[i], subStrIdx)),
                        "IMM" => new Operand(Operand.OperandType.IMM, ToSize(operandsStrings[i], subStrIdx)),
                        "AL" => new Operand(Operand.OperandType.A, Operand.OperandSize._8Bits),
                        "AX" => new Operand(Operand.OperandType.A, Operand.OperandSize._16Bits),
                        "EAX" => new Operand(Operand.OperandType.A, Operand.OperandSize._32Bits),
                        "RAX" => new Operand(Operand.OperandType.A, Operand.OperandSize._64Bits),
                        "P" => new Operand(Operand.OperandType.P, Operand.OperandSize._8Bits),
                        "MOFFS" => new Operand(Operand.OperandType.MOFFS, ToSize(operandsStrings[i], subStrIdx)),
                        _ => throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic($"Invalid Encoding Type '{instruction}'")),
                    };
                    ;


                }
                return operands;

                Operand.OperandSize ToSize(string size, int start)
                {
                    return size.Substring(start, size.Length - start) switch
                    {
                        "64" => Operand.OperandSize._64Bits,
                        "32" => Operand.OperandSize._32Bits,
                        "16" => Operand.OperandSize._16Bits,
                        "8" => Operand.OperandSize._8Bits,
                        "8U" => Operand.OperandSize._8BitsUpper,
                        _ => throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic($"Invalid Encoding Type '{instruction}'")),
                    };
                }
            }
        }
    }
}
