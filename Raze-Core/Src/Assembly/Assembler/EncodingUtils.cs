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
                
            internal static Exception EncodingError()
            {
                return Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("Invalid/Unsupported Instruction"));
            }

            internal static Instruction.ModRegRm.RegisterCode ExprRegisterToModRegRmRegister(AssemblyExpr.Register register) => (register.Name, register.Size) switch
            {
                (AssemblyExpr.Register.RegisterName.RAX, AssemblyExpr.Register.RegisterSize._8BitsUpper) => Instruction.ModRegRm.RegisterCode.AH,
                (AssemblyExpr.Register.RegisterName.RBX, AssemblyExpr.Register.RegisterSize._8BitsUpper) => Instruction.ModRegRm.RegisterCode.BH,
                (AssemblyExpr.Register.RegisterName.RCX, AssemblyExpr.Register.RegisterSize._8BitsUpper) => Instruction.ModRegRm.RegisterCode.CH,
                (AssemblyExpr.Register.RegisterName.RDX, AssemblyExpr.Register.RegisterSize._8BitsUpper) => Instruction.ModRegRm.RegisterCode.DH,
                (_, AssemblyExpr.Register.RegisterSize._8BitsUpper) => throw EncodingError(),
                _ => (Instruction.ModRegRm.RegisterCode)ToRegCode(register.Name)
            };

            private static int ToRegCode(AssemblyExpr.Register.RegisterName register)
            {
                return (int)register % 8;
            }

            internal static bool SetRex(Encoding.EncodingTypes encodingType) => encodingType.HasFlag(Encoding.EncodingTypes.RexPrefix);

            internal static bool SetRexW(Encoding.EncodingTypes encodingType) => encodingType.HasFlag(Encoding.EncodingTypes.RexWPrefix);

            internal static bool SetSizePrefix(Encoding.EncodingTypes encodingType) => encodingType.HasFlag(Encoding.EncodingTypes.SizePrefix);

            internal static bool BaseAddressingModeMustHaveDisplacement(AssemblyExpr.Register register) =>
                register.Name == AssemblyExpr.Register.RegisterName.RBP ||
                register.Name == AssemblyExpr.Register.RegisterName.R13;

            internal static bool BaseAddressingModeMustHaveSIB(AssemblyExpr.Register register) =>
                register.Name == AssemblyExpr.Register.RegisterName.RSP ||
                register.Name == AssemblyExpr.Register.RegisterName.R12;

            internal static IInstruction GetImmInstruction(Operand.OperandSize size, AssemblyExpr.Literal op2Expr, Assembler assembler, Encoding.EncodingTypes encodingTypes)
            {
                if (op2Expr.type == AssemblyExpr.Literal.LiteralType.RefData)
                {
                    return assembler.symbolTable.definitions.ContainsKey(((AssemblyExpr.LabelLiteral)op2Expr).Name) ? 
                        GenerateImmFromInt(size, assembler.symbolTable.definitions[((AssemblyExpr.LabelLiteral)op2Expr).Name] + (long)Linker.Elf64.Elf64_Shdr.dataVirtualAddress) : 
                        DefaultUnresolvedReference;
                }
                else if (op2Expr.type == AssemblyExpr.Literal.LiteralType.RefProcedure || op2Expr.type == AssemblyExpr.Literal.LiteralType.RefLocalProcedure)
                {
                    return assembler.symbolTable.definitions.ContainsKey(((AssemblyExpr.LabelLiteral)op2Expr).Name) ?
                        GenerateImmFromInt(size,
                            CalculateJumpLocation(
                                encodingTypes.HasFlag(Encoding.EncodingTypes.RelativeJump),
                                assembler.symbolTable.definitions[((AssemblyExpr.LabelLiteral)op2Expr).Name],
                                ((Linker.ReferenceInfo)assembler.symbolTable.unresolvedReferences[assembler.symbolTable.sTableUnresRefIdx]).location,
                                ((Linker.ReferenceInfo)assembler.symbolTable.unresolvedReferences[assembler.symbolTable.sTableUnresRefIdx]).size
                           )
                        ) : 
                        DefaultUnresolvedReference;
                }
                return new Instruction.Immediate(op2Expr.value, size);
            }

            private static long CalculateJumpLocation(bool relativeJump, int symbolLocation, int location, int size)
            {
                if (relativeJump)
                {
                    return symbolLocation - (location + size);
                }
                return (long)((ulong)symbolLocation + Linker.Elf64.Elf64_Shdr.textVirtualAddress);
            }

            private static IInstruction GenerateImmFromInt(Operand.OperandSize size, long value) => new Instruction.Immediate(BitConverter.GetBytes(value), size);

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

                if (SwapOperands(encoding))
                {
                    (op1R, op2R) = (op2R, op1R);
                }

                if (SetRex(encoding.encodingType) | rexw | op1R | op2R)
                {
                    rexPrefix = new Instruction.RexPrefix(rexw, op1R, false, op2R);
                    return true;
                }
                rexPrefix = new();
                return false;
            }
            internal static bool SetRexPrefix(AssemblyExpr.Unary unary, Encoding encoding, out Instruction.RexPrefix rexPrefix)
            {
                bool rexw = SetRexW(encoding.encodingType);
                bool op2R = IsRRegister(unary.operand);

                if (SetRex(encoding.encodingType) | rexw | op2R)
                {
                    rexPrefix = new Instruction.RexPrefix(rexw, op2R, false, false);
                    return true;
                }
                rexPrefix = new();
                return false;
            }

            internal static bool IsRRegister(AssemblyExpr.IOperand op)
            {
                if (op is AssemblyExpr.Register register)
                {
                    return ((int)register.Name / 8 % 2) == 1;
                }
                else if (op is AssemblyExpr.Pointer pointer && pointer.value.IsRegister(out var ptrReg))
                {
                    return ((int)ptrReg.Name / 8 % 2) == 1;
                }
                return false;
            }

            internal static bool SwapOperands(Encoding encoding)
            {
                return encoding.operands[0].type.HasFlag(Operand.OperandType.M);
            }

            internal static bool SetAddressSizeOverridePrefix(AssemblyExpr.IOperand operand)
            {
                if (operand is AssemblyExpr.Pointer ptr && ptr.value.IsRegister())
                {
                    if (ptr.value.Size == AssemblyExpr.Register.RegisterSize._32Bits)
                    {
                        return true;
                    }
                    else if (ptr.value.Size != AssemblyExpr.Register.RegisterSize._64Bits)
                    {
                        EncodingError();
                    }
                }
                return false;
            }

            internal static Exception ThrowIvalidEncodingType(string t1) =>
                Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic($"Cannot encode instruction with operand '{t1.ToUpper()}'"));
            internal static Exception ThrowIvalidEncodingType(string t1, string t2) =>
                Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic($"Cannot encode instruction with operands '{t1.ToUpper()}, {t2.ToUpper()}'"));

            internal static (Operand.OperandSize, Operand.OperandSize) HandleUnresolvedRef(AssemblyExpr expr, AssemblyExpr.LabelLiteral literal, Operand.OperandSize defualtSize, Assembler assembler)
            {
                if (literal.type == AssemblyExpr.Literal.LiteralType.RefData)
                {
                    if (assembler.nonResolvingPass)
                    {
                        if (!literal.scoped)
                        {
                            literal.Name = "data." + literal.Name;
                            literal.scoped = true;
                        }
                        assembler.symbolTable.unresolvedReferences.Add(new Linker.ReferenceInfo(expr, assembler.TextLocation, -1));
                    }
                    else
                    {
                        var operandSize = SizeOfIntegerUnsigned((ulong)assembler.symbolTable.definitions[literal.Name] + Linker.Elf64.Elf64_Shdr.dataVirtualAddress);
                        return (operandSize, operandSize);
                    }
                }
                else if (literal.type == AssemblyExpr.Literal.LiteralType.RefProcedure)
                {
                    if (assembler.nonResolvingPass)
                    {
                        if (!literal.scoped)
                        {
                            literal.Name = "text." + literal.Name;
                            literal.scoped = true;
                        }
                        assembler.symbolTable.unresolvedReferences.Add(new Linker.ReferenceInfo(expr, assembler.TextLocation, -1));
                    }
                    else
                    {
                        return (
                            SizeOfIntegerUnsigned((ulong)assembler.symbolTable.definitions[literal.Name] + Linker.Elf64.Elf64_Shdr.textVirtualAddress),
                            SizeOfIntegerSigned(CalculateJumpLocation(
                                true,
                                assembler.symbolTable.definitions[literal.Name],
                                ((Linker.ReferenceInfo)assembler.symbolTable.unresolvedReferences[assembler.symbolTable.sTableUnresRefIdx]).location,
                                ((Linker.ReferenceInfo)assembler.symbolTable.unresolvedReferences[assembler.symbolTable.sTableUnresRefIdx]).size
                            ))
                        );
                    }
                }
                else
                {
                    if (assembler.nonResolvingPass)
                    {
                        if (!literal.scoped)
                        {
                            literal.Name = assembler.enclosingLbl + '.' + literal.Name;
                            literal.scoped = true;
                        }
                        assembler.symbolTable.unresolvedReferences.Add(new Linker.ReferenceInfo(expr, assembler.TextLocation, -1));
                    }
                    else
                    {
                        return (
                            SizeOfIntegerUnsigned((ulong)assembler.symbolTable.definitions[literal.Name] + Linker.Elf64.Elf64_Shdr.textVirtualAddress),
                            SizeOfIntegerSigned(CalculateJumpLocation(
                                true,
                                assembler.symbolTable.definitions[literal.Name],
                                ((Linker.ReferenceInfo)assembler.symbolTable.unresolvedReferences[assembler.symbolTable.sTableUnresRefIdx]).location,
                                ((Linker.ReferenceInfo)assembler.symbolTable.unresolvedReferences[assembler.symbolTable.sTableUnresRefIdx]).size
                            ))
                        );
                    }
                }
                return (defualtSize, defualtSize);
            }

            private static Operand.OperandSize SizeOfIntegerUnsigned(ulong value) => (Operand.OperandSize)CodeGen.GetIntegralSizeUnsigned(value);
            private static Operand.OperandSize SizeOfIntegerSigned(long value) => (Operand.OperandSize)CodeGen.GetIntegralSizeSigned(value);

            private static bool IsReferenceLiteralType(AssemblyExpr.Literal.LiteralType literalType) =>
                literalType >= AssemblyExpr.Literal.LiteralType.RefData;

            internal static bool IsReferenceLiteralOperand(Operand operand, AssemblyExpr.IOperand instructionOperand, out AssemblyExpr.LabelLiteral labelLiteral)
            {
                if (operand.type == Operand.OperandType.IMM && IsReferenceLiteralType(((AssemblyExpr.Literal)instructionOperand).type))
                {
                    labelLiteral = (AssemblyExpr.LabelLiteral)instructionOperand;
                    return true;
                }
                if (operand.type == Operand.OperandType.MOFFS && IsReferenceLiteralType(((AssemblyExpr.Literal)((AssemblyExpr.Pointer)instructionOperand).value).type))
                {
                    labelLiteral = (AssemblyExpr.LabelLiteral)((AssemblyExpr.Pointer)instructionOperand).value;
                    return true;
                }
                labelLiteral = null;
                return false;
            }
        }
    }
}
