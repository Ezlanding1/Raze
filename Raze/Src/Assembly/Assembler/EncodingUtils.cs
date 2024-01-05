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
        internal static partial class EncodingUtils
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

            internal static IInstruction GetImmInstruction(Operand.OperandSize size, AssemblyExpr.Literal op2Expr, Assembler assembler)
            {
                if (op2Expr.type == AssemblyExpr.Literal.LiteralType.REF_DATA)
                {
                    assembler.symbolTable.unresolvedData.Push((op2Expr.value, assembler.location));
                }
                else if (op2Expr.type == AssemblyExpr.Literal.LiteralType.REF_PROCEDURE)
                {
                    assembler.symbolTable.unresolvedLabels.Push((op2Expr.value, assembler.location));
                }
                else if (op2Expr.type == AssemblyExpr.Literal.LiteralType.REF_LOCALPROCEDURE)
                {
                    assembler.symbolTable.unresolvedLocalLabels.Push((op2Expr.value, assembler.location));
                }

                return size switch
                {
                    Operand.OperandSize._8Bits => ImmediateGenerator.GenerateImm8(op2Expr),
                    Operand.OperandSize._16Bits => ImmediateGenerator.GenerateImm16(op2Expr),
                    Operand.OperandSize._32Bits => ImmediateGenerator.GenerateImm32(op2Expr),
                    Operand.OperandSize._64Bits => ImmediateGenerator.GenerateImm64(op2Expr),
                };
            }

            internal static bool Disp8Bit(int disp)
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

            internal static bool SetRexPrefix(AssemblyExpr.Binary binary, Encoding encoding, out Instruction.RexPrefix rexPrefix)
            {
                bool rexw = SetRexW(encoding.encodingType);
                bool op1R = IsRRegister(binary.operand1);
                bool op2R = IsRRegister(binary.operand2);

                if (SetRex(encoding.encodingType) | rexw | op1R | op2R)
                {
                    rexPrefix = new Instruction.RexPrefix(rexw, op2R, false, op1R);
                    return true;
                }
                rexPrefix = new();
                return false;
            }
            internal static bool SetRexPrefix(AssemblyExpr.Unary binary, Encoding encoding, out Instruction.RexPrefix rexPrefix)
            {
                bool rexw = SetRexW(encoding.encodingType);
                bool op2R = IsRRegister(binary.operand);

                if (SetRex(encoding.encodingType) | rexw | op2R)
                {
                    rexPrefix = new Instruction.RexPrefix(rexw, op2R, false, false);
                    return true;
                }
                rexPrefix = new();
                return false;
            }

            private static bool IsRRegister(AssemblyExpr.Operand op)
                => op is AssemblyExpr.Register && (int)((AssemblyExpr.Register)op).name < 0
                    || op is AssemblyExpr.Pointer && (int)((AssemblyExpr.Pointer)op).register.name < 0;

            internal static bool SetAddressSizeOverridePrefix(AssemblyExpr.Operand operand)
            {
                if (operand is AssemblyExpr.Pointer ptr)
                {
                    if (ptr.register.size == AssemblyExpr.Register.RegisterSize._32Bits)
                    {
                        return true;
                    }
                    else if (ptr.register.size != AssemblyExpr.Register.RegisterSize._64Bits)
                    {
                        EncodingError();
                    }
                }
                return false;
            }

            internal static void ThrowIvalidEncodingType(string t1, string t2)
            {
                Diagnostics.errors.Push(new Error.ImpossibleError($"Cannot encode instruction with operands '{t1.ToUpper()}, {t2.ToUpper()}'"));
            }
        }
    }
}
