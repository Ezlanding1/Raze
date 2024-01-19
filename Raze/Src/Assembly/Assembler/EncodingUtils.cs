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
            private static readonly IInstruction DefaultUnresolvedReference = new Instruction.Immediate8Byte(0);
                
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

            internal static IInstruction GetImmInstruction(Operand.OperandSize size, AssemblyExpr.Literal op2Expr, Linker.SymbolTable symbolTable)
            {
                if (op2Expr.type == AssemblyExpr.Literal.LiteralType.REF_DATA)
                {
                    return symbolTable.definitions.ContainsKey(op2Expr.value) ? GenerateImmFromInt(size, (ulong)symbolTable.definitions[op2Expr.value] + Linker.Elf64.Elf64_Shdr.dataOffset) : DefaultUnresolvedReference;
                }
                else if (op2Expr.type == AssemblyExpr.Literal.LiteralType.REF_PROCEDURE || op2Expr.type == AssemblyExpr.Literal.LiteralType.REF_LOCALPROCEDURE)
                {
                    return symbolTable.definitions.ContainsKey(op2Expr.value)? GenerateImmFromInt(size, (ulong)symbolTable.definitions[op2Expr.value] + Linker.Elf64.Elf64_Shdr.textOffset) : DefaultUnresolvedReference;
                }

                return size switch
                {
                    Operand.OperandSize._8Bits => ImmediateGenerator.GenerateImm8(op2Expr),
                    Operand.OperandSize._16Bits => ImmediateGenerator.GenerateImm16(op2Expr),
                    Operand.OperandSize._32Bits => ImmediateGenerator.GenerateImm32(op2Expr),
                    Operand.OperandSize._64Bits => ImmediateGenerator.GenerateImm64(op2Expr),
                };
            }

            private static IInstruction GenerateImmFromInt(Operand.OperandSize size, ulong value) => size switch
            {
                Operand.OperandSize._8Bits => new Instruction.Immediate8Byte((byte)value),
                Operand.OperandSize._16Bits => new Instruction.Immediate16UShort((ushort)value),
                Operand.OperandSize._32Bits => new Instruction.Immediate32UInt((uint)value),
                Operand.OperandSize._64Bits => new Instruction.Immediate64ULong((ulong)value),
            };

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

            internal static Operand HandleUnresolvedRef(AssemblyExpr expr, AssemblyExpr.Operand operand, Assembler assembler, out AssemblyExpr.Literal.LiteralType refResolveType)
            {
                refResolveType = (AssemblyExpr.Literal.LiteralType)(-1);

                var encodingType = operand.ToAssemblerOperand();
                if (encodingType.operandType != Operand.OperandType.IMM)
                {
                    return encodingType;
                }

                AssemblyExpr.Literal literal = (AssemblyExpr.Literal)operand;

                if (literal.type == AssemblyExpr.Literal.LiteralType.REF_DATA)
                {
                    if (assembler.nonResolvingPass)
                    {
                        refResolveType = AssemblyExpr.Literal.LiteralType.REF_DATA;
                        literal.value = "data." + literal.value;
                        assembler.symbolTable.unresolvedReferences.Add(new Linker.ReferenceInfo(expr, assembler.textLocation, -1));
                    }
                    if (assembler.symbolTable.definitions.ContainsKey(literal.value))
                    {
                        return new(encodingType.operandType, SizeOfIntegerUnsigned((ulong)assembler.symbolTable.definitions[literal.value] + Linker.Elf64.Elf64_Shdr.dataOffset));
                    }
                }
                else if (literal.type == AssemblyExpr.Literal.LiteralType.REF_PROCEDURE)
                {
                    if (assembler.nonResolvingPass)
                    {
                        refResolveType = AssemblyExpr.Literal.LiteralType.REF_PROCEDURE;
                        literal.value = "text." + literal.value;
                        assembler.symbolTable.unresolvedReferences.Add(new Linker.ReferenceInfo(expr, assembler.textLocation, -1));
                    }
                    if (assembler.symbolTable.definitions.ContainsKey(literal.value))
                    {
                        return new(encodingType.operandType, SizeOfIntegerUnsigned((ulong)assembler.symbolTable.definitions[literal.value] + Linker.Elf64.Elf64_Shdr.textOffset));
                    }
                }
                else if (literal.type == AssemblyExpr.Literal.LiteralType.REF_LOCALPROCEDURE)
                {
                    if (assembler.nonResolvingPass)
                    {
                        refResolveType = AssemblyExpr.Literal.LiteralType.REF_LOCALPROCEDURE;
                        literal.value = assembler.enclosingLbl + '.' + literal.value;
                        assembler.symbolTable.unresolvedReferences.Add(new Linker.ReferenceInfo(expr, assembler.textLocation, -1));
                    }
                    if (assembler.symbolTable.definitions.ContainsKey(literal.value))
                    {
                        return new(encodingType.operandType, SizeOfIntegerUnsigned((ulong)assembler.symbolTable.definitions[literal.value] + Linker.Elf64.Elf64_Shdr.textOffset));
                    }
                }
                return encodingType;
            }

            private static Operand.OperandSize SizeOfIntegerUnsigned(ulong value)
            {
                if (value <= byte.MaxValue)
                {
                    return Operand.OperandSize._8Bits;
                }
                else if (value <= ushort.MaxValue)
                {
                    return Operand.OperandSize._16Bits;
                }
                else if (value <= uint.MaxValue)
                {
                    return Operand.OperandSize._32Bits;
                }
                return Operand.OperandSize._64Bits;
            }

            internal static bool IsReferenceLiteralType(AssemblyExpr.Literal.LiteralType literalType) =>
                literalType == AssemblyExpr.Literal.LiteralType.REF_DATA ||
                literalType == AssemblyExpr.Literal.LiteralType.REF_PROCEDURE ||
                literalType == AssemblyExpr.Literal.LiteralType.REF_LOCALPROCEDURE;
        }
    }
}
