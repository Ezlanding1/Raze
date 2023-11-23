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

            internal static Instruction.ModRegRm.RegisterCode ExprRegisterToModRegRmRegister(AssemblyExpr.Register register) => (register.name, register.size) switch
            {
                (AssemblyExpr.Register.RegisterName.RAX, AssemblyExpr.Register.RegisterSize._8BitsUpper) => Instruction.ModRegRm.RegisterCode.AH,
                (AssemblyExpr.Register.RegisterName.RBX, AssemblyExpr.Register.RegisterSize._8BitsUpper) => Instruction.ModRegRm.RegisterCode.BH,
                (AssemblyExpr.Register.RegisterName.RCX, AssemblyExpr.Register.RegisterSize._8BitsUpper) => Instruction.ModRegRm.RegisterCode.CH,
                (AssemblyExpr.Register.RegisterName.RDX, AssemblyExpr.Register.RegisterSize._8BitsUpper) => Instruction.ModRegRm.RegisterCode.DH,
                _ => (Instruction.ModRegRm.RegisterCode)ToRegCode((int)register.name)
            };

            private static int ToRegCode(int register) => (register < 0) ? -register - 1 : register;

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

            internal static void AddRexPrefix(List<IInstruction> instructions, Encoding.EncodingTypes encodingType, Operand op1, Operand op2, AssemblyExpr op1Expr, AssemblyExpr op2Expr)
            {
                bool rexw = SetRexW(encodingType);
                bool op1R = IsRRegister(op1, op1Expr);
                bool op2R = IsRRegister(op2, op2Expr);

                if (SetRex(encodingType) | rexw | op1R | op2R)
                {
                    instructions.Add(new Instruction.RexPrefix(rexw, op2R, false, op1R));
                }
            }

            private static bool IsRRegister(Operand op, AssemblyExpr opExpr)
                => op.operandType.HasFlag(Operand.OperandType.A) && (int)((AssemblyExpr.Register)opExpr).name < 0
                    || op.operandType.HasFlag(Operand.OperandType.M) && (int)((AssemblyExpr.Pointer)opExpr).register.name < 0;
        }
    }
}