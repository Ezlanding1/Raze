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
            private static readonly IInstruction DefaultUnresolvedReference = new Instruction.RawInstruction([0]);
            private static readonly Operand.OperandSize DefaultUnresolvedReferenceSize = Operand.OperandSize._8Bits;

            internal static Exception EncodingError()
            {
                return Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("Invalid/Unsupported Instruction"));
            }

            internal static Instruction.ModRegRm.RegisterCode ExprRegisterToModRegRmRegister(AssemblyExpr.Register register) => (register.name, register.Size) switch
            {
                (AssemblyExpr.Register.RegisterName.RAX, AssemblyExpr.Register.RegisterSize._8BitsUpper) => Instruction.ModRegRm.RegisterCode.AH,
                (AssemblyExpr.Register.RegisterName.RBX, AssemblyExpr.Register.RegisterSize._8BitsUpper) => Instruction.ModRegRm.RegisterCode.BH,
                (AssemblyExpr.Register.RegisterName.RCX, AssemblyExpr.Register.RegisterSize._8BitsUpper) => Instruction.ModRegRm.RegisterCode.CH,
                (AssemblyExpr.Register.RegisterName.RDX, AssemblyExpr.Register.RegisterSize._8BitsUpper) => Instruction.ModRegRm.RegisterCode.DH,
                (_, AssemblyExpr.Register.RegisterSize._8BitsUpper) => throw EncodingError(),
                _ => (Instruction.ModRegRm.RegisterCode)ToRegCode(register.name)
            };

            private static int ToRegCode(AssemblyExpr.Register.RegisterName register)
            {
                return (int)register % 8;
            }

            internal static bool SetRex(Encoding.EncodingTypes encodingType) => encodingType.HasFlag(Encoding.EncodingTypes.RexPrefix);

            internal static bool SetRexW(Encoding.EncodingTypes encodingType) => encodingType.HasFlag(Encoding.EncodingTypes.RexWPrefix);

            internal static bool SetSizePrefix(Encoding.EncodingTypes encodingType) => encodingType.HasFlag(Encoding.EncodingTypes.SizePrefix);

            internal static bool BaseAddressingModeMustHaveDisplacement(AssemblyExpr.Register register) =>
                register.name == AssemblyExpr.Register.RegisterName.RBP ||
                register.name == AssemblyExpr.Register.RegisterName.R13;

            internal static bool BaseAddressingModeMustHaveSIB(AssemblyExpr.Register register) =>
                register.name == AssemblyExpr.Register.RegisterName.RSP ||
                register.name == AssemblyExpr.Register.RegisterName.R12;

            internal static IInstruction GetImmInstruction(Operand.OperandSize size, AssemblyExpr.Literal op2Expr, Assembler assembler, Encoding.EncodingTypes encodingTypes)
            {
                if (op2Expr.type == AssemblyExpr.Literal.LiteralType.RefData)
                {
                    return assembler.symbolTable.definitions.ContainsKey(((AssemblyExpr.LabelLiteral)op2Expr).Name) ? 
                        GenerateImmFromSignedInt(size, assembler.symbolTable.definitions[((AssemblyExpr.LabelLiteral)op2Expr).Name] + (long)assembler.dataVirtualAddress) : 
                        DefaultUnresolvedReference;
                }
                else if (op2Expr.type == AssemblyExpr.Literal.LiteralType.RefProcedure || op2Expr.type == AssemblyExpr.Literal.LiteralType.RefLocalProcedure)
                {
                    return assembler.symbolTable.definitions.ContainsKey(((AssemblyExpr.LabelLiteral)op2Expr).Name) ?
                        GenerateImmFromSignedInt(size,
                            CalculateJumpLocation(
                                encodingTypes.HasFlag(Encoding.EncodingTypes.RelativeJump),
                                assembler.symbolTable.definitions[((AssemblyExpr.LabelLiteral)op2Expr).Name],
                                (Linker.ReferenceInfo)assembler.symbolTable.unresolvedReferences[assembler.symbolTable.sTableUnresRefIdx],
                                assembler
                           )
                        ) : 
                        DefaultUnresolvedReference;
                }
                return new Instruction.Immediate(op2Expr.value);
            }

            private static long CalculateJumpLocation(bool relativeJump, int symbolLocation, Linker.ReferenceInfo refInfo, Assembler assembler)
            {
                refInfo.absoluteAddress = !relativeJump;
                return _CalculateJumpLocation(relativeJump, symbolLocation, refInfo.location, refInfo.size, assembler);
            }
            private static long _CalculateJumpLocation(bool relativeJump, int symbolLocation, int location, int size, Assembler assembler)
            {
                if (relativeJump)
                {
                    return symbolLocation - (location + size);
                }
                return (long)((ulong)symbolLocation + assembler.textVirtualAddress);
            }

            private static IInstruction GenerateImmFromSignedInt(Operand.OperandSize size, long value)
            {
                byte[] bytes = BitConverter.GetBytes(value);
                AssemblyExpr.ImmediateGenerator.ResizeSignedInteger(ref bytes, (int)size);

                return new Instruction.Immediate(bytes);
            }

            internal static Instruction.ModRegRm.Mod GetDispSize(byte[] disp)
                => disp.Length == 1 ? Instruction.ModRegRm.Mod.OneByteDisplacement : Instruction.ModRegRm.Mod.FourByteDisplacement;

            internal static IInstruction GetDispInstruction(byte[] disp)
            {
                disp = AssemblyExpr.ImmediateGenerator.MinimizeImmediate(AssemblyExpr.Literal.LiteralType.Integer, disp);

                if (disp.Length == 1)
                {
                    return new Instruction.Displacement8(unchecked((sbyte)disp[0]));
                }
                else if (disp.Length <= 4)
                {
                    AssemblyExpr.ImmediateGenerator.ResizeSignedInteger(ref disp, 4);
                    return new Instruction.Displacement32(BitConverter.ToInt32(disp));
                }
                throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("Invalid displacement length: " + disp.Length));
            }

            internal static bool SetRexPrefix(AssemblyExpr.Binary binary, Encoding encoding, out Instruction.RexPrefix rexPrefix)
            {
                bool rexw = SetRexW(encoding.encodingType);
                bool op1R = IsRRegister(binary.operand1);
                bool op2R = IsRRegister(binary.operand2);

                if (SwapOperands(encoding) || encoding.operands[1].type.HasFlag(Operand.OperandType.IMM))
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
                    rexPrefix = new Instruction.RexPrefix(rexw, false, false, op2R);
                    return true;
                }
                rexPrefix = new();
                return false;
            }

            internal static bool IsRRegister(AssemblyExpr.IOperand op)
            {
                if (op is AssemblyExpr.Register register)
                {
                    return ((int)register.name / 8 % 2) == 1;
                }
                else if (op is AssemblyExpr.Pointer pointer && pointer.value != null)
                {
                    return ((int)pointer.value.name / 8 % 2) == 1;
                }
                return false;
            }

            internal static bool SwapOperands(Encoding encoding)
            {
                return encoding.operands[0].type.HasFlag(Operand.OperandType.M);
            }

            internal static bool SetAddressSizeOverridePrefix(AssemblyExpr.IOperand operand)
            {
                if (operand is AssemblyExpr.Pointer ptr && ptr.value != null)
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

            internal static (Operand.OperandSize, Operand.OperandSize) HandleUnresolvedRef(AssemblyExpr expr, AssemblyExpr.LabelLiteral literal, Assembler assembler)
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
                        assembler.symbolTable.unresolvedReferences.Add(new Linker.ReferenceInfo(expr, assembler.TextLocation, -1, true));
                        assembler.symbolTable.sTableUnresRefIdx = assembler.symbolTable.unresolvedReferences.Count - 1;
                    }
                    else
                    {
                        return (
                            SizeOfIntegerUnsigned((ulong)assembler.symbolTable.definitions[literal.Name] + assembler.dataVirtualAddress),
                            SizeOfIntegerUnsigned((ulong)assembler.symbolTable.definitions[literal.Name] + (assembler.dataVirtualAddress - assembler.textVirtualAddress))
                        );
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
                        assembler.symbolTable.unresolvedReferences.Add(new Linker.ReferenceInfo(expr, assembler.TextLocation, -1, true));
                        assembler.symbolTable.sTableUnresRefIdx = assembler.symbolTable.unresolvedReferences.Count - 1;
                    }
                    else
                    {
                        return (
                            SizeOfIntegerUnsigned((ulong)assembler.symbolTable.definitions[literal.Name] + assembler.textVirtualAddress),
                            SizeOfIntegerSigned(CalculateJumpLocation(
                                true,
                                assembler.symbolTable.definitions[literal.Name],
                                (Linker.ReferenceInfo)assembler.symbolTable.unresolvedReferences[assembler.symbolTable.sTableUnresRefIdx],
                                assembler
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
                        assembler.symbolTable.unresolvedReferences.Add(new Linker.ReferenceInfo(expr, assembler.TextLocation, -1, true));
                        assembler.symbolTable.sTableUnresRefIdx = assembler.symbolTable.unresolvedReferences.Count - 1;
                    }
                    else
                    {
                        return (
                            SizeOfIntegerUnsigned((ulong)assembler.symbolTable.definitions[literal.Name] + assembler.textVirtualAddress),
                            SizeOfIntegerSigned(CalculateJumpLocation(
                                true,
                                assembler.symbolTable.definitions[literal.Name],
                                (Linker.ReferenceInfo)assembler.symbolTable.unresolvedReferences[assembler.symbolTable.sTableUnresRefIdx],
                                assembler
                            ))
                        );
                    }
                }
                return (DefaultUnresolvedReferenceSize, DefaultUnresolvedReferenceSize);
            }

            private static Operand.OperandSize SizeOfIntegerUnsigned(ulong value) => (Operand.OperandSize)CodeGen.GetIntegralSizeUnsigned(value);
            private static Operand.OperandSize SizeOfIntegerSigned(long value) => (Operand.OperandSize)CodeGen.GetIntegralSizeSigned(value);

            internal static bool IsReferenceLiteralType(AssemblyExpr.Literal.LiteralType literalType) =>
                literalType >= AssemblyExpr.Literal.LiteralType.RefData;

            internal static bool IsReferenceLiteralOperand(ref Operand operand, AssemblyExpr.OperandInstruction operandInstruction, Assembler assembler, out AssemblyExpr.LabelLiteral labelLiteral, out bool refResolve)
            {
                var instructionOperand = operandInstruction.Operands[^1];

                if (operand.type == Operand.OperandType.IMM && IsReferenceLiteralType(((AssemblyExpr.Literal)instructionOperand).type))
                {
                    labelLiteral = (AssemblyExpr.LabelLiteral)instructionOperand;
                    return refResolve = true;
                }
                if (operand.type.HasFlag(Operand.OperandType.M))
                {
                    AssemblyExpr.Pointer ptr = (AssemblyExpr.Pointer)instructionOperand;

                    if (IsReferenceLiteralType(ptr.offset.type))
                    {
                        labelLiteral = (AssemblyExpr.LabelLiteral)ptr.offset;
                        
                        (_, var size) = HandleUnresolvedRef(operandInstruction, labelLiteral, assembler);

                        if (size == Operand.OperandSize._64Bits)
                        {
                            operand = new(Operand.OperandType.MOFFS, operand.size);
                        }

                        refResolve = true;
                        return false;
                    }
                    else if (ptr.value == null)
                    {
                        // Rip-relative encoding (add to symbolTable)

                        if ((Operand.OperandSize)ptr.offset.Size == Operand.OperandSize._64Bits)
                        {
                            operand = new(Operand.OperandType.MOFFS, operand.size);
                        }

                        if (assembler.nonResolvingPass)
                        {
                            assembler.symbolTable.unresolvedReferences.Add(new Linker.ReferenceInfo(operandInstruction, assembler.TextLocation, -1, true));
                        }
                        labelLiteral = null;
                        refResolve = true;
                        return false;
                    }
                }
                labelLiteral = null;
                return refResolve = false;
            }

            internal static void ShrinkSignedDisplacement(ref byte[] disp, int newSize)
            {
                disp = AssemblyExpr.ImmediateGenerator.MinimizeImmediate(AssemblyExpr.Literal.LiteralType.Integer, disp);
                if (disp.Length > newSize)
                {
                    throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("Invalid displacement length: " + disp.Length));
                }
                AssemblyExpr.ImmediateGenerator.ResizeSignedInteger(ref disp, newSize);
            }
        }
    }
}
