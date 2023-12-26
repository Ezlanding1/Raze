using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Raze.AssemblyExpr.Literal;

namespace Raze;

public partial class Assembler
{
    public partial class Encoder
    {
        internal static partial class EncodingUtils
        {
            private static partial class ImmediateGenerator
            {
                internal static IInstruction GenerateImm8(AssemblyExpr.Literal literal) => literal.type switch
                {
                    LiteralType.INTEGER => ParseSigned8(literal.value),
                    LiteralType.FLOATING => Instruction.Immediate8.Generate(Half.Parse(literal.value)),
                    LiteralType.STRING => Instruction.Immediate8.Generate(literal.value[0]),
                    // 8-bit REF_STRING cannot exist
                    LiteralType.BINARY => Instruction.Immediate8.Generate(Convert.ToByte(literal.value, 2)),
                    LiteralType.HEX => ParseSigned8(literal.value, 16),
                    LiteralType.BOOLEAN => Instruction.Immediate8.Generate(literal.value == "true" ? (sbyte)1 : (sbyte)0)
                };

                internal static IInstruction GenerateImm16(AssemblyExpr.Literal literal) => literal.type switch
                {
                    LiteralType.INTEGER => ParseSigned16(literal.value),
                    LiteralType.FLOATING => Instruction.Immediate16.Generate(Half.Parse(literal.value)),
                    LiteralType.STRING => Instruction.Immediate16.Generate(literal.value),
                    // 16-bit REF_STRING cannot exist
                    LiteralType.BINARY => Instruction.Immediate16.Generate(Convert.ToUInt16(literal.value, 2)),
                    LiteralType.HEX => ParseSigned16(literal.value, 16),
                    LiteralType.BOOLEAN => Instruction.Immediate16.Generate(literal.value == "true" ? (ushort)1 : (ushort)0)
                };

                internal static IInstruction GenerateImm32(AssemblyExpr.Literal literal) => literal.type switch
                {
                    LiteralType.INTEGER => ParseSigned32(literal.value),
                    LiteralType.FLOATING => Instruction.Immediate32.Generate(float.Parse(literal.value)),
                    LiteralType.STRING => Instruction.Immediate32.Generate(literal.value[0]),
                    // 32-bit REF_STRING cannot exist
                    LiteralType.BINARY => Instruction.Immediate32.Generate(Convert.ToUInt32(literal.value, 2)),
                    LiteralType.HEX => ParseSigned32(literal.value, 16),
                    LiteralType.BOOLEAN => Instruction.Immediate32.Generate(literal.value == "true" ? (uint)1 : (uint)0)
                };

                internal static IInstruction GenerateImm64(AssemblyExpr.Literal literal) => literal.type switch
                {
                    LiteralType.INTEGER => ParseSigned64(literal.value),
                    LiteralType.FLOATING => Instruction.Immediate64.Generate(double.Parse(literal.value)),
                    LiteralType.STRING => Instruction.Immediate64.Generate(literal.value[0]),
                    LiteralType.BINARY => Instruction.Immediate64.Generate(Convert.ToUInt64(literal.value, 2)),
                    LiteralType.HEX => ParseSigned64(literal.value, 16),
                    LiteralType.BOOLEAN => Instruction.Immediate64.Generate(literal.value == "true" ? (ulong)1 : (ulong)0),
                    LiteralType.REF_DATA => new Instruction.Immediate64Long(0),
                    LiteralType.REF_PROCEDURE => new Instruction.Immediate64Long(0),
                    LiteralType.REF_LOCALPROCEDURE => new Instruction.Immediate64Long(0),
                };

                private static IInstruction ParseSigned8(string literal, int _base=10) =>
                    (literal[0] == '-') ? Instruction.Immediate8.Generate(Convert.ToSByte(literal, _base)) : Instruction.Immediate8.Generate(Convert.ToByte(literal, _base));
                private static IInstruction ParseSigned16(string literal, int _base=10) =>
                    (literal[0] == '-') ? Instruction.Immediate16.Generate(Convert.ToInt16(literal, _base)) : Instruction.Immediate16.Generate(Convert.ToUInt16(literal, _base));
                private static IInstruction ParseSigned32(string literal, int _base=10) =>
                    (literal[0] == '-') ? Instruction.Immediate32.Generate(Convert.ToInt32(literal, _base)) : Instruction.Immediate32.Generate(Convert.ToUInt32(literal, _base));
                private static IInstruction ParseSigned64(string literal, int _base=10) =>
                    (literal[0] == '-') ? Instruction.Immediate64.Generate(Convert.ToInt64(literal, _base)) : Instruction.Immediate64.Generate(Convert.ToUInt64(literal, _base));
            }
        }
    }
}
