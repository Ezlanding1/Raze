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
                foreach (var (operand, idx) in assemblyExpr.Operands
                    .Select((op, idx) => (op, idx))
                    .Where(x => Operand.OperandType.IMM.HasFlag(operands[x.idx].type)))
                {
                    AssemblyExpr.Literal literal = (AssemblyExpr.Literal)operand;

                    if (operands[idx].size != this.operands[idx].size && literal.type < AssemblyExpr.Literal.LiteralType.RefData)
                    {
                        if (!AssemblyExpr.ImmediateGenerator.ResizeImmediate(literal, this.operands[idx].size))
                            return false;
                    }
                    
                    if (encodingType.HasFlag(EncodingTypes.SignExtends) && literal.type == AssemblyExpr.Literal.LiteralType.UnsignedInteger)
                    {
                        bool unsignedIntDoesNotFit = this.operands[idx].size switch
                        {
                            Operand.OperandSize._8Bits => literal.value[0] > sbyte.MaxValue,
                            Operand.OperandSize._16Bits => BitConverter.ToUInt16(literal.value) > short.MaxValue,
                            Operand.OperandSize._32Bits => BitConverter.ToUInt32(literal.value) > int.MaxValue,
                            _ => BitConverter.ToUInt64(literal.value) > long.MaxValue
                        };
                        
                        if (unsignedIntDoesNotFit)
                        {
                            return false;
                        }
                    }
                    else if (encodingType.HasFlag(EncodingTypes.ZeroExtends) && literal.type == AssemblyExpr.Literal.LiteralType.Integer)
                    {
                        bool signedIntDoesNotFit = this.operands[idx].size switch
                        {
                            Operand.OperandSize._8Bits => unchecked((sbyte)literal.value[0]) < 0,
                            Operand.OperandSize._16Bits => BitConverter.ToInt16(literal.value) < 0,
                            Operand.OperandSize._32Bits => BitConverter.ToInt32(literal.value) < 0,
                            _ => BitConverter.ToInt64(literal.value) < 0
                        };
                        
                        if (signedIntDoesNotFit)
                        {
                            return false;
                        }
                    }

                    if (this.operands[idx].type == Operand.OperandType.One && !AssemblyExpr.ImmediateGenerator.IsOne(literal))
                    {
                        return false;
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
                        ((register.name >= AssemblyExpr.Register.RegisterName.RSP && register.name <= AssemblyExpr.Register.RegisterName.RDI) ||
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