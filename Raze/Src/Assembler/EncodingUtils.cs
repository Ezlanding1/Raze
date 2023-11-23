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
        private static class EncodingUtils
        {
            internal static Instruction EncodingError()
            {
                Diagnostics.errors.Push(new Error.ImpossibleError("Invalid/Unsupported Instruction"));
                return new Instruction();
            }

            internal static bool SetRex(Encoding.EncodingTypes encodingType) => encodingType.HasFlag(Encoding.EncodingTypes.RexPrefix);

            internal static bool SetRexW(Encoding.EncodingTypes encodingType) => encodingType.HasFlag(Encoding.EncodingTypes.RexWPrefix);

            internal static bool SetSizePrefix(Encoding.EncodingTypes encodingType) => encodingType.HasFlag(Encoding.EncodingTypes.SizePrefix);

            internal static bool CanHaveZeroByteDisplacement(AssemblyExpr.Register register) => register.name switch
            {
                AssemblyExpr.Register.RegisterName.RAX or
                AssemblyExpr.Register.RegisterName.RBX or
                AssemblyExpr.Register.RegisterName.RCX or
                AssemblyExpr.Register.RegisterName.RDX or
                AssemblyExpr.Register.RegisterName.RDI or
                AssemblyExpr.Register.RegisterName.RSI => true,
                _ => false
            };

            internal static IInstruction GetImmInstruction(Operand.OperandSize size, AssemblyExpr.Literal op2Expr) => size switch
            {
                Operand.OperandSize._8Bits => new Instruction.Immediate8(sbyte.Parse(op2Expr.value)),
                Operand.OperandSize._16Bits => new Instruction.Immediate16(short.Parse(op2Expr.value)),
                Operand.OperandSize._32Bits => new Instruction.Immediate32(int.Parse(op2Expr.value)),
                Operand.OperandSize._64Bits => new Instruction.Immediate64(long.Parse(op2Expr.value)),
            };

            private static bool Disp8Bit(int disp) 
                => disp <= sbyte.MaxValue && disp >= sbyte.MinValue;

            internal static Instruction.ModRegRm.Mod GetDispSize(int disp) 
                => Disp8Bit(disp) ? Instruction.ModRegRm.Mod.OneByteDisplacement : Instruction.ModRegRm.Mod.FourByteDisplacement;

            internal static IInstruction GetDispInstruction(int disp)
            {
                if (Disp8Bit(disp))
                {
                    return new Instruction.Displacement8((sbyte)disp);
                }
                return new Instruction.Displacement32(disp);
            }

            internal static bool IsRegister(Operand op)
                => op.operandType.HasFlag(Operand.OperandType.A) || op.operandType.HasFlag(Operand.OperandType.P);
        }
    }
}