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
                    LiteralType.Integer => Instruction.Immediate8.Generate(Convert.ToSByte(literal.value)),
                    LiteralType.UnsignedInteger => Instruction.Immediate8.Generate(Convert.ToByte(literal.value[..^1])),
                    LiteralType.Floating => Instruction.Immediate8.Generate(Half.Parse(literal.value)),
                    LiteralType.String => Instruction.Immediate8.Generate(literal.value[0]),
                    // 8-bit REF_STRING cannot exist
                    LiteralType.Binary => Instruction.Immediate8.Generate(Convert.ToByte(literal.value, 2)),
                    LiteralType.Hex => Instruction.Immediate8.Generate(Convert.ToSByte(literal.value, 16)),
                    LiteralType.Boolean => Instruction.Immediate8.Generate(literal.value == "true" ? (sbyte)1 : (sbyte)0)
                };

                internal static IInstruction GenerateImm16(AssemblyExpr.Literal literal) => literal.type switch
                {
                    LiteralType.Integer => Instruction.Immediate16.Generate(Convert.ToInt16(literal.value)),
                    LiteralType.UnsignedInteger => Instruction.Immediate16.Generate(Convert.ToUInt16(literal.value[..^1])),
                    LiteralType.Floating => Instruction.Immediate16.Generate(Half.Parse(literal.value)),
                    LiteralType.String => Instruction.Immediate16.Generate(literal.value),
                    // 16-bit REF_STRING cannot exist
                    LiteralType.Binary => Instruction.Immediate16.Generate(Convert.ToUInt16(literal.value, 2)),
                    LiteralType.Hex => Instruction.Immediate16.Generate(Convert.ToInt16(literal.value, 16)),
                    LiteralType.Boolean => Instruction.Immediate16.Generate(literal.value == "true" ? (ushort)1 : (ushort)0)
                };

                internal static IInstruction GenerateImm32(AssemblyExpr.Literal literal) => literal.type switch
                {
                    LiteralType.Integer => Instruction.Immediate32.Generate(Convert.ToInt32(literal.value)),
                    LiteralType.UnsignedInteger => Instruction.Immediate32.Generate(Convert.ToUInt32(literal.value[..^1])),
                    LiteralType.Floating => Instruction.Immediate32.Generate(float.Parse(literal.value)),
                    LiteralType.String => Instruction.Immediate32.Generate(literal.value[0]),
                    // 32-bit REF_STRING cannot exist
                    LiteralType.Binary => Instruction.Immediate32.Generate(Convert.ToUInt32(literal.value, 2)),
                    LiteralType.Hex => Instruction.Immediate32.Generate(Convert.ToInt32(literal.value, 16)),
                    LiteralType.Boolean => Instruction.Immediate32.Generate(literal.value == "true" ? (uint)1 : (uint)0)
                };

                internal static IInstruction GenerateImm64(AssemblyExpr.Literal literal) => literal.type switch
                {
                    LiteralType.Integer => Instruction.Immediate64.Generate(Convert.ToInt64(literal.value)),
                    LiteralType.UnsignedInteger => Instruction.Immediate64.Generate(Convert.ToUInt64(literal.value[..^1])),
                    LiteralType.Floating => Instruction.Immediate64.Generate(double.Parse(literal.value)),
                    LiteralType.String => Instruction.Immediate64.Generate(literal.value[0]),
                    LiteralType.Binary => Instruction.Immediate64.Generate(Convert.ToUInt64(literal.value, 2)),
                    LiteralType.Hex => Instruction.Immediate64.Generate(Convert.ToInt64(literal.value, 16)),
                    LiteralType.Boolean => Instruction.Immediate64.Generate(literal.value == "true" ? (ulong)1 : (ulong)0)
                };
            }
        }
    }
}
