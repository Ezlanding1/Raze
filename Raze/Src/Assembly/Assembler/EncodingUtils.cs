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
            private static readonly IInstruction DefaultUnresolvedReference = new Instruction.RawInstruction(new byte[] { 0 });
                
            internal static Instruction EncodingError()
            {
                Diagnostics.errors.Push(new Error.ImpossibleError("Invalid/Unsupported Instruction"));
                return new Instruction();
            }

            internal static Instruction.ModRegRm.RegisterCode ExprRegisterToModRegRmRegister(AssemblyExpr.Register register) => (register.Name, register.Size) switch
            {
                (AssemblyExpr.Register.RegisterName.RAX, AssemblyExpr.Register.RegisterSize._8BitsUpper) => Instruction.ModRegRm.RegisterCode.AH,
                (AssemblyExpr.Register.RegisterName.RBX, AssemblyExpr.Register.RegisterSize._8BitsUpper) => Instruction.ModRegRm.RegisterCode.BH,
                (AssemblyExpr.Register.RegisterName.RCX, AssemblyExpr.Register.RegisterSize._8BitsUpper) => Instruction.ModRegRm.RegisterCode.CH,
                (AssemblyExpr.Register.RegisterName.RDX, AssemblyExpr.Register.RegisterSize._8BitsUpper) => Instruction.ModRegRm.RegisterCode.DH,
                _ => (Instruction.ModRegRm.RegisterCode)ToRegCode((int)register.Name)
            };

            private static int ToRegCode(int register) => (register < 0) ? -register - 1 : register;

            internal static bool SetRex(Encoding.EncodingTypes encodingType) => encodingType.HasFlag(Encoding.EncodingTypes.RexPrefix);

            internal static bool SetRexW(Encoding.EncodingTypes encodingType) => encodingType.HasFlag(Encoding.EncodingTypes.RexWPrefix);

            internal static bool SetSizePrefix(Encoding.EncodingTypes encodingType) => encodingType.HasFlag(Encoding.EncodingTypes.SizePrefix);

            internal static bool CanHaveZeroByteDisplacement(AssemblyExpr.Register register) => register.Name switch
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
                if (op2Expr.type == AssemblyExpr.Literal.LiteralType.RefData)
                {
                    return symbolTable.definitions.ContainsKey(((AssemblyExpr.LabelLiteral)op2Expr).Name) ? 
                        GenerateImmFromInt(size, (ulong)symbolTable.definitions[((AssemblyExpr.LabelLiteral)op2Expr).Name] + Linker.Elf64.Elf64_Shdr.dataVirtualAddress) : 
                        DefaultUnresolvedReference;
                }
                else if (op2Expr.type == AssemblyExpr.Literal.LiteralType.RefProcedure || op2Expr.type == AssemblyExpr.Literal.LiteralType.RefLocalProcedure)
                {
                    return symbolTable.definitions.ContainsKey(((AssemblyExpr.LabelLiteral)op2Expr).Name)? 
                        GenerateImmFromInt(size, (ulong)symbolTable.definitions[((AssemblyExpr.LabelLiteral)op2Expr).Name] + Linker.Elf64.Elf64_Shdr.textVirtualAddress) : 
                        DefaultUnresolvedReference;
                }
                return new Instruction.Immediate(op2Expr.value, size);
            }

            private static IInstruction GenerateImmFromInt(Operand.OperandSize size, ulong value) => new Instruction.Immediate(BitConverter.GetBytes(value), size);

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
                => op is AssemblyExpr.Register && (int)((AssemblyExpr.Register)op).Name < 0
                    || op is AssemblyExpr.Pointer && (int)((AssemblyExpr.Pointer)op).register.Name < 0;

            internal static bool SetAddressSizeOverridePrefix(AssemblyExpr.Operand operand)
            {
                if (operand is AssemblyExpr.Pointer ptr)
                {
                    if (ptr.register.Size == AssemblyExpr.Register.RegisterSize._32Bits)
                    {
                        return true;
                    }
                    else if (ptr.register.Size != AssemblyExpr.Register.RegisterSize._64Bits)
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

                AssemblyExpr.Literal.LiteralType literalType = ((AssemblyExpr.Literal)operand).type;

                if (literalType == AssemblyExpr.Literal.LiteralType.RefData)
                {
                    var literal = (AssemblyExpr.LabelLiteral)operand;
                    if (assembler.nonResolvingPass)
                    {
                        refResolveType = AssemblyExpr.Literal.LiteralType.RefData;
                        literal.Name = "data." + literal.value;
                        assembler.symbolTable.unresolvedReferences.Add(new Linker.ReferenceInfo(expr, assembler.textLocation, -1));
                    }
                    if (assembler.symbolTable.definitions.ContainsKey(literal.Name))
                    {
                        return new(encodingType.operandType, SizeOfIntegerUnsigned((ulong)assembler.symbolTable.definitions[literal.Name] + Linker.Elf64.Elf64_Shdr.dataVirtualAddress));
                    }
                }
                else if (literalType == AssemblyExpr.Literal.LiteralType.RefProcedure)
                {
                    var literal = (AssemblyExpr.LabelLiteral)operand;
                    if (assembler.nonResolvingPass)
                    {
                        refResolveType = AssemblyExpr.Literal.LiteralType.RefProcedure;
                        literal.Name = "text." + literal.Name;
                        assembler.symbolTable.unresolvedReferences.Add(new Linker.ReferenceInfo(expr, assembler.textLocation, -1));
                    }
                    if (assembler.symbolTable.definitions.ContainsKey(literal.Name))
                    {
                        return new(encodingType.operandType, SizeOfIntegerUnsigned((ulong)assembler.symbolTable.definitions[literal.Name] + Linker.Elf64.Elf64_Shdr.textVirtualAddress));
                    }
                }
                else if (literalType == AssemblyExpr.Literal.LiteralType.RefLocalProcedure)
                {
                    var literal = (AssemblyExpr.LabelLiteral)operand;
                    if (assembler.nonResolvingPass)
                    {
                        refResolveType = AssemblyExpr.Literal.LiteralType.RefLocalProcedure;
                        literal.Name = assembler.enclosingLbl + '.' + literal.Name;
                        assembler.symbolTable.unresolvedReferences.Add(new Linker.ReferenceInfo(expr, assembler.textLocation, -1));
                    }
                    if (assembler.symbolTable.definitions.ContainsKey(literal.Name))
                    {
                        return new(encodingType.operandType, SizeOfIntegerUnsigned((ulong)assembler.symbolTable.definitions[literal.Name] + Linker.Elf64.Elf64_Shdr.textVirtualAddress));
                    }
                }
                return encodingType;
            }

            private static Operand.OperandSize SizeOfIntegerUnsigned(ulong value) => (Operand.OperandSize)CodeGen.GetIntegralSizeUnsigned(value);

            internal static bool IsReferenceLiteralType(AssemblyExpr.Literal.LiteralType literalType) =>
                literalType == AssemblyExpr.Literal.LiteralType.RefData ||
                literalType == AssemblyExpr.Literal.LiteralType.RefProcedure ||
                literalType == AssemblyExpr.Literal.LiteralType.RefLocalProcedure;
        }
    }
}
