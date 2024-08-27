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
                        Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic($"Invalid Encoding Type {value}"));
                    }
                }
            }
            internal EncodingTypes encodingType = EncodingTypes.None;

            public byte OpCodeExtension { get; set; }
            public string OpCode
            {
                set => opCode = value.Split().Select(x => (byte)Convert.ToInt16(x, 16)).ToArray();
            }
            internal byte[] opCode;

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

            public bool SpecialMatch(AssemblyExpr.OperandInstruction assemblyExpr, params Operand[] operands)
            {
                for (int i = 0; i < operands.Length; i++)
                {
                    if (Operand.OperandType.IMM.HasFlag(operands[i].type) && operands[i].size != this.operands[i].size && ((AssemblyExpr.Literal)assemblyExpr.Operands[i]).type < AssemblyExpr.Literal.LiteralType.RefData)
                    {
                        if (!AssemblyExpr.ImmediateGenerator.ResizeImmediate((AssemblyExpr.Literal)assemblyExpr.Operands[i], this.operands[i].size))
                            return false;
                    }
                }

                    && ((AssemblyExpr.Literal)((AssemblyExpr.Binary)assemblyExpr).operand2).type < AssemblyExpr.Literal.LiteralType.RefData)
                {
                    switch (this.operands[1].size)
                    {
                        case Operand.OperandSize._8Bits:
                            if (((AssemblyExpr.Literal)((AssemblyExpr.Binary)assemblyExpr).operand2).value[0] > sbyte.MaxValue) return false;
                            break;
                        case Operand.OperandSize._16Bits:
                            if (BitConverter.ToInt16(((AssemblyExpr.Literal)((AssemblyExpr.Binary)assemblyExpr).operand2).value) > short.MaxValue) return false;
                            break;
                        case Operand.OperandSize._32Bits:
                            if (BitConverter.ToInt32(((AssemblyExpr.Literal)((AssemblyExpr.Binary)assemblyExpr).operand2).value) > int.MaxValue) return false;
                            break;
                        default:
                            if (BitConverter.ToInt64(((AssemblyExpr.Literal)((AssemblyExpr.Binary)assemblyExpr).operand2).value) > long.MaxValue) return false;
                            break;
                    }
                }

                if (encodingType.HasFlag(EncodingTypes.NoUpper8BitEncoding))
                {
                    if (assemblyExpr.Operands.Any(x => x.Size == AssemblyExpr.Register.RegisterSize._8BitsUpper))
                    {
                        return false;
                    }
                }
                else if (assemblyExpr.Operands.Any(x => x.IsRegister(out var register) &&
                    x.Size == AssemblyExpr.Register.RegisterSize._8Bits && 
                        ((register.Name >= AssemblyExpr.Register.RegisterName.RSP && register.Name <= AssemblyExpr.Register.RegisterName.RDI) ||
                        EncodingUtils.IsRRegister(register))
                    ))
                {
                    return false;
                }

                return true;
            }

            internal void AddRegisterCode(Instruction.ModRegRm.RegisterCode registerCode)
            {
                opCode[^1] = (byte)((opCode[^1] & 0xF8) | (byte)registerCode);
            }
        }
    }
}