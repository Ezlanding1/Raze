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
                    int subStrIdx = 1;
                    while (subStrIdx < operandsStrings[i].Length && char.IsLetter(operandsStrings[i][subStrIdx]))
                    {
                        subStrIdx++;
                    }

                    operands[i] = operandsStrings[i][..subStrIdx].ToUpper() switch
                    {
                        "R" => new Operand(Operand.OperandType.R, ToSize(operandsStrings[i], subStrIdx)),
                        "RM" => new Operand(Operand.OperandType.R | Operand.OperandType.M, ToSize(operandsStrings[i], subStrIdx)),

                        "AL" => new Operand(Operand.OperandType.A, ConstantSize(operandsStrings[i], subStrIdx, Operand.OperandSize._8Bits)),
                        "AX" => new Operand(Operand.OperandType.A, ConstantSize(operandsStrings[i], subStrIdx, Operand.OperandSize._16Bits)),
                        "EAX" => new Operand(Operand.OperandType.A, ConstantSize(operandsStrings[i], subStrIdx, Operand.OperandSize._32Bits)),
                        "RAX" => new Operand(Operand.OperandType.A, ConstantSize(operandsStrings[i], subStrIdx, Operand.OperandSize._64Bits)),

                        "CL" => new Operand(Operand.OperandType.C, ConstantSize(operandsStrings[i], subStrIdx, Operand.OperandSize._8Bits)),
                        "CX" => new Operand(Operand.OperandType.C, ConstantSize(operandsStrings[i], subStrIdx, Operand.OperandSize._16Bits)),
                        "ECX" => new Operand(Operand.OperandType.C, ConstantSize(operandsStrings[i], subStrIdx, Operand.OperandSize._32Bits)),
                        "RCX" => new Operand(Operand.OperandType.C, ConstantSize(operandsStrings[i], subStrIdx, Operand.OperandSize._64Bits)),

                        "DL" => new Operand(Operand.OperandType.D, ConstantSize(operandsStrings[i], subStrIdx, Operand.OperandSize._8Bits)),
                        "DX" => new Operand(Operand.OperandType.D, ConstantSize(operandsStrings[i], subStrIdx, Operand.OperandSize._16Bits)),
                        "EDX" => new Operand(Operand.OperandType.D, ConstantSize(operandsStrings[i], subStrIdx, Operand.OperandSize._32Bits)),
                        "RDX" => new Operand(Operand.OperandType.D, ConstantSize(operandsStrings[i], subStrIdx, Operand.OperandSize._64Bits)),

                        "RNA" => new Operand(Operand.OperandType.RNA, ToSize(operandsStrings[i], subStrIdx)),

                        "M" => new Operand(Operand.OperandType.M, ToSize(operandsStrings[i], subStrIdx)),
                        "MOFFS" => new Operand(Operand.OperandType.MOFFS, ToSize(operandsStrings[i], subStrIdx)),

                        "IMM" => new Operand(Operand.OperandType.IMM, ToSize(operandsStrings[i], subStrIdx)),
                        "1" => new Operand(Operand.OperandType.One, Operand.OperandSize._8Bits), 

                        "XMM" => new Operand(Operand.OperandType.XMM, ConstantSize(operandsStrings[i], subStrIdx, Operand.OperandSize._128Bits)),
                        "XMMRM" => new Operand(Operand.OperandType.XMM | Operand.OperandType.M, ToSize(operandsStrings[i], subStrIdx)),

                        "MMX" => new Operand(Operand.OperandType.MMX, ConstantSize(operandsStrings[i], subStrIdx, Operand.OperandSize._128Bits)),
                        "MMXRM" => new Operand(Operand.OperandType.MMX | Operand.OperandType.M, ToSize(operandsStrings[i], subStrIdx)),

                        "CR" => new Operand(Operand.OperandType.CR, ConstantSize(operandsStrings[i], subStrIdx, Operand.OperandSize._64Bits)),
                        "DR" => new Operand(Operand.OperandType.DR, ConstantSize(operandsStrings[i], subStrIdx, Operand.OperandSize._64Bits)),
                        "TR" => new Operand(Operand.OperandType.TR, ConstantSize(operandsStrings[i], subStrIdx, Operand.OperandSize._32Bits)),

                        "SEG" => new Operand(Operand.OperandType.SEG, ConstantSize(operandsStrings[i], subStrIdx, Operand.OperandSize._16Bits)),
                        "CS" => new Operand(Operand.OperandType.CS, ConstantSize(operandsStrings[i], subStrIdx, Operand.OperandSize._16Bits)),
                        "FS" => new Operand(Operand.OperandType.FS, ConstantSize(operandsStrings[i], subStrIdx, Operand.OperandSize._16Bits)),
                        "GS" => new Operand(Operand.OperandType.GS, ConstantSize(operandsStrings[i], subStrIdx, Operand.OperandSize._16Bits)),

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
                        "128" => Operand.OperandSize._128Bits,
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
