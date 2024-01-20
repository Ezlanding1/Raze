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
            public string Instruction
            {
                set => operands = GetOperandsFromInstruction(value);
            }
            internal Operand[] operands;

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
            internal EncodingTypes encodingType = EncodingTypes.None;

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

            public bool SpecialMatch(AssemblyExpr assemblyExpr, params Operand[] operands)
            {
                if (encodingType.HasFlag(EncodingTypes.SignExtends) && operands.Length == 2 && operands[1].operandType == Operand.OperandType.IMM
                    && ((AssemblyExpr.Literal)((AssemblyExpr.Binary)assemblyExpr).operand2).type < AssemblyExpr.Literal.LiteralType.REF_DATA)
                {
                    switch (this.operands[1].size)
                    {
                        case Operand.OperandSize._8Bits:
                        case Operand.OperandSize._8BitsUpper:
                            return sbyte.TryParse(((AssemblyExpr.Literal)((AssemblyExpr.Binary)assemblyExpr).operand2).value, out _);
                        case Operand.OperandSize._16Bits:
                            return short.TryParse(((AssemblyExpr.Literal)((AssemblyExpr.Binary)assemblyExpr).operand2).value, out _);
                        case Operand.OperandSize._32Bits:
                            return int.TryParse(((AssemblyExpr.Literal)((AssemblyExpr.Binary)assemblyExpr).operand2).value, out _);
                        default:
                            return long.TryParse(((AssemblyExpr.Literal)((AssemblyExpr.Binary)assemblyExpr).operand2).value, out _);
                    }
                }
                return true;
            }

            internal void AddRegisterCode(Instruction.ModRegRm.RegisterCode registerCode)
            {
                OpCode = (byte)((OpCode & 0xF8) | (byte)registerCode);
            }
        }
    }
}