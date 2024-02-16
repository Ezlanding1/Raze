﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze.Tools;

partial class Syntaxes
{
    partial class SyntaxFactory 
    {
        class IntelSyntax : ISyntaxFactory, AssemblyExpr.IVisitor<string>, AssemblyExpr.IUnaryOperandVisitor<string>
        {
            public IntelSyntax()
            {
                header = new AssemblyExpr.Comment("Raze Compiler Version ALPHA 0.0.0 Intel_x86-64 NASM");
            }

            public override void Run(CodeGen.ISection instructions)
            {
                foreach (var instruction in instructions)
                {
                    Run(instruction);
                }
            }

            public override void Run(AssemblyExpr instruction)
            {
                var res = instruction.Accept(this);

                if (!string.IsNullOrEmpty(res))
                {
                    Output.AppendLine(res);
                }
            }

            public string VisitBinary(AssemblyExpr.Binary instruction)
            {
                if (instruction.operand2 is AssemblyExpr.Pointer && !SpecifyPointerSizeOperand2(instruction.instruction))
                    return $"{instruction.instruction}\t{instruction.operand1.Accept(this)}, {PointerToString((AssemblyExpr.Pointer)instruction.operand2)}";

                return $"{instruction.instruction}\t{instruction.operand1.Accept(this)}, {instruction.operand2.Accept(this)}";
            }

            public string VisitComment(AssemblyExpr.Comment instruction)
            {
                return $"; {instruction.comment}";
            }

            public string VisitData(AssemblyExpr.Data instruction)
            {
                if (instruction.literal.type == AssemblyExpr.Literal.LiteralType.String)
                {
                    return $"{instruction.name}: {InstructionUtils.dataSize[AssemblyExpr.Register.RegisterSize._8Bits]} {UnescapeString(instruction.literal.value)}";
                }
                return $"{instruction.name}: {InstructionUtils.dataSize[instruction.literal.Size]} {instruction.literal.Accept(this)}";
            }

            public string VisitProcedure(AssemblyExpr.Procedure instruction)
            {
                return $"{instruction.name}:";
            }

            public string VisitLocalProcedure(AssemblyExpr.LocalProcedure instruction)
            {
                return $".{instruction.name}:";
            }

            public string VisitGlobal(AssemblyExpr.Global instruction)
            {
                return $"global {instruction.name}";
            }

            public string VisitMemory(AssemblyExpr.Pointer instruction)
            {
                if (instruction.register.Name == AssemblyExpr.Register.RegisterName.TMP)
                {
                    Diagnostics.errors.Push(new Error.ImpossibleError("TMP Register Emitted"));
                }

                if (instruction.offset == 0)
                {
                    return $"{InstructionUtils.wordSize[instruction.Size]} [{instruction.register.Accept(this)}]";
                }
                return $"{InstructionUtils.wordSize[instruction.Size]} [{instruction.register.Accept(this)} {((instruction.offset < 0) ? '-' : '+')} {Math.Abs(instruction.offset)}]";
            }

            public string VisitRegister(AssemblyExpr.Register instruction)
            {
                if (instruction.Name == AssemblyExpr.Register.RegisterName.TMP)
                {
                    Diagnostics.errors.Push(new Error.ImpossibleError("TMP Register Cannot Be Emitted"));
                }

                return $"{RegisterToString[(instruction.Name, instruction.Size)]}";
            }

            public string VisitSection(AssemblyExpr.Section instruction)
            {
                return $"\nsection .{instruction.name}";
            }

            public string VisitUnary(AssemblyExpr.Unary instruction)
            {
                return $"{instruction.instruction}\t{instruction.operand.Accept(this)}";
            }

            public string VisitZero(AssemblyExpr.Zero instruction)
            {
                return $"{instruction.instruction}";
            }

            private string PointerToString(AssemblyExpr.Pointer instruction)
            {
                if (instruction.register.Name == AssemblyExpr.Register.RegisterName.TMP)
                {
                    Diagnostics.errors.Push(new Error.ImpossibleError("TMP Register Cannot Be Emitted"));
                }

                if (instruction.offset == 0)
                {
                    return $"[{instruction.register.Accept(this)}]";
                }
                return $"[{instruction.register.Accept(this)} {((instruction.offset < 0) ? '-' : '+')} {Math.Abs(instruction.offset)}]";
            }

            public string VisitImmediate(AssemblyExpr.Literal instruction)
            {
                switch (instruction.type)
                {
                    case AssemblyExpr.Literal.LiteralType.String:
                    {
                        if (instruction.value.Length == 0) return "0";

                        int strAsInt = instruction.value[^1];
                        for (int i = instruction.value.Length - 2; i >= 0; i--)
                        {
                            strAsInt <<= 8;
                            strAsInt += instruction.value[i];
                        }
                        return $"{strAsInt}";
                    }
                    case AssemblyExpr.Literal.LiteralType.RefData:
                        return ((AssemblyExpr.LabelLiteral)instruction).Name;
                    case AssemblyExpr.Literal.LiteralType.RefProcedure:
                        return ((AssemblyExpr.LabelLiteral)instruction).Name;
                    case AssemblyExpr.Literal.LiteralType.RefLocalProcedure:
                        return $".{((AssemblyExpr.LabelLiteral)instruction).Name}";
                    case AssemblyExpr.Literal.LiteralType.UnsignedInteger:
                    {
                        byte[] number = instruction.value.ToArray();
                        Array.Resize(ref number, 8);
                        return BitConverter.ToUInt64(number).ToString();
                    }
                    case AssemblyExpr.Literal.LiteralType.Hex:
                        return "0x" + IntegralImmediateToString(instruction.value, 16);
                    case AssemblyExpr.Literal.LiteralType.Binary:
                        return "0b" + IntegralImmediateToString(instruction.value, 2);
                    default:
                        return IntegralImmediateToString(instruction.value, 10);
                }
            }

            private bool SpecifyPointerSizeOperand2(AssemblyExpr.Instruction instruction)
            {
                return instruction == AssemblyExpr.Instruction.MOVSX || instruction == AssemblyExpr.Instruction.MOVZX;
            }

            private string UnescapeString(byte[] bytes)
            {
                if (bytes.Length == 0) return "\"\"";

                string escapedString = Encoding.ASCII.GetString(bytes);
                List<char> escapedChars = Lexer.stringEscapeCodes.Select(x => x.Item2).ToList();

                StringBuilder unescapedString = new StringBuilder();

                for (int i = 0; i < escapedString.Length; i++)
                {
                    if (escapedChars.Contains(escapedString[i]))
                    {
                        if (i != 0) 
                            unescapedString.Append("\", ");

                        unescapedString.Append($"0x{(byte)escapedString[i]:x}");

                        if (i != escapedString.Length-1)
                            unescapedString.Append(", \"");
                        continue;
                    }
                    if (i == 0)
                        unescapedString.Append('"');
                    unescapedString.Append(escapedString[i]);
                    if (i == escapedString.Length-1)
                        unescapedString.Append('"');
                }
                return unescapedString.ToString();
            }

            private static string IntegralImmediateToString(byte[] bytes, int _base)
            {
                byte[] number = bytes.ToArray();
                Array.Resize(ref number, 8);
                return Convert.ToString(BitConverter.ToInt64(number), _base);
            }

            private static Dictionary<(AssemblyExpr.Register.RegisterName, AssemblyExpr.Register.RegisterSize?), string> RegisterToString = new()
            {
                { (AssemblyExpr.Register.RegisterName.RAX, AssemblyExpr.Register.RegisterSize._64Bits), "RAX" }, // 64-Bits 
                { (AssemblyExpr.Register.RegisterName.RAX, AssemblyExpr.Register.RegisterSize._32Bits), "EAX" }, // Lower 32-Bits
                { (AssemblyExpr.Register.RegisterName.RAX, AssemblyExpr.Register.RegisterSize._16Bits), "AX" }, // Lower 16-Bits
                { (AssemblyExpr.Register.RegisterName.RAX, AssemblyExpr.Register.RegisterSize._8BitsUpper), "AH" }, // Upper 16-Bits
                { (AssemblyExpr.Register.RegisterName.RAX, AssemblyExpr.Register.RegisterSize._8Bits), "AL" }, // Lower 8-Bits

                { (AssemblyExpr.Register.RegisterName.RCX, AssemblyExpr.Register.RegisterSize._64Bits), "RCX" },
                { (AssemblyExpr.Register.RegisterName.RCX, AssemblyExpr.Register.RegisterSize._32Bits), "ECX" },
                { (AssemblyExpr.Register.RegisterName.RCX, AssemblyExpr.Register.RegisterSize._16Bits), "CX" },
                { (AssemblyExpr.Register.RegisterName.RCX, AssemblyExpr.Register.RegisterSize._8BitsUpper), "CH" },
                { (AssemblyExpr.Register.RegisterName.RCX, AssemblyExpr.Register.RegisterSize._8Bits), "CL" },

                { (AssemblyExpr.Register.RegisterName.RDX, AssemblyExpr.Register.RegisterSize._64Bits), "RDX" },
                { (AssemblyExpr.Register.RegisterName.RDX, AssemblyExpr.Register.RegisterSize._32Bits), "EDX" },
                { (AssemblyExpr.Register.RegisterName.RDX, AssemblyExpr.Register.RegisterSize._16Bits), "DX" },
                { (AssemblyExpr.Register.RegisterName.RDX, AssemblyExpr.Register.RegisterSize._8BitsUpper), "DH" },
                { (AssemblyExpr.Register.RegisterName.RDX, AssemblyExpr.Register.RegisterSize._8Bits), "DL" },

                { (AssemblyExpr.Register.RegisterName.RBX, AssemblyExpr.Register.RegisterSize._64Bits), "RBX" },
                { (AssemblyExpr.Register.RegisterName.RBX, AssemblyExpr.Register.RegisterSize._32Bits), "EBX" },
                { (AssemblyExpr.Register.RegisterName.RBX, AssemblyExpr.Register.RegisterSize._16Bits), "BX" },
                { (AssemblyExpr.Register.RegisterName.RBX, AssemblyExpr.Register.RegisterSize._8BitsUpper), "BH" },
                { (AssemblyExpr.Register.RegisterName.RBX, AssemblyExpr.Register.RegisterSize._8Bits), "BL" },

                { (AssemblyExpr.Register.RegisterName.RSI, AssemblyExpr.Register.RegisterSize._64Bits), "RSI" },
                { (AssemblyExpr.Register.RegisterName.RSI, AssemblyExpr.Register.RegisterSize._32Bits), "ESI" },
                { (AssemblyExpr.Register.RegisterName.RSI, AssemblyExpr.Register.RegisterSize._16Bits), "SI" },
                { (AssemblyExpr.Register.RegisterName.RSI, AssemblyExpr.Register.RegisterSize._8Bits), "SIL" },

                { (AssemblyExpr.Register.RegisterName.RDI, AssemblyExpr.Register.RegisterSize._64Bits), "RDI" },
                { (AssemblyExpr.Register.RegisterName.RDI, AssemblyExpr.Register.RegisterSize._32Bits), "EDI" },
                { (AssemblyExpr.Register.RegisterName.RDI, AssemblyExpr.Register.RegisterSize._16Bits), "DI" },
                { (AssemblyExpr.Register.RegisterName.RDI, AssemblyExpr.Register.RegisterSize._8Bits), "DIL" },

                { (AssemblyExpr.Register.RegisterName.RSP, AssemblyExpr.Register.RegisterSize._64Bits), "RSP" },
                { (AssemblyExpr.Register.RegisterName.RSP, AssemblyExpr.Register.RegisterSize._32Bits), "ESP" },
                { (AssemblyExpr.Register.RegisterName.RSP, AssemblyExpr.Register.RegisterSize._16Bits), "SP" },
                { (AssemblyExpr.Register.RegisterName.RSP, AssemblyExpr.Register.RegisterSize._8Bits), "SPL" },

                { (AssemblyExpr.Register.RegisterName.RBP, AssemblyExpr.Register.RegisterSize._64Bits), "RBP" },
                { (AssemblyExpr.Register.RegisterName.RBP, AssemblyExpr.Register.RegisterSize._32Bits), "EBP" },
                { (AssemblyExpr.Register.RegisterName.RBP, AssemblyExpr.Register.RegisterSize._16Bits), "BP" },
                { (AssemblyExpr.Register.RegisterName.RBP, AssemblyExpr.Register.RegisterSize._8Bits), "BPL" },

                { (AssemblyExpr.Register.RegisterName.R8, AssemblyExpr.Register.RegisterSize._64Bits), "R8" },
                { (AssemblyExpr.Register.RegisterName.R8, AssemblyExpr.Register.RegisterSize._32Bits), "R8D" },
                { (AssemblyExpr.Register.RegisterName.R8, AssemblyExpr.Register.RegisterSize._16Bits), "R8W" },
                { (AssemblyExpr.Register.RegisterName.R8, AssemblyExpr.Register.RegisterSize._8Bits), "R8B" },

                { (AssemblyExpr.Register.RegisterName.R9, AssemblyExpr.Register.RegisterSize._64Bits), "R9" },
                { (AssemblyExpr.Register.RegisterName.R9, AssemblyExpr.Register.RegisterSize._32Bits), "R9D" },
                { (AssemblyExpr.Register.RegisterName.R9, AssemblyExpr.Register.RegisterSize._16Bits), "R9W" },
                { (AssemblyExpr.Register.RegisterName.R9, AssemblyExpr.Register.RegisterSize._8Bits), "R9B" },

                { (AssemblyExpr.Register.RegisterName.R10, AssemblyExpr.Register.RegisterSize._64Bits), "R10" },
                { (AssemblyExpr.Register.RegisterName.R10, AssemblyExpr.Register.RegisterSize._32Bits), "R10D" },
                { (AssemblyExpr.Register.RegisterName.R10, AssemblyExpr.Register.RegisterSize._16Bits), "R10W" },
                { (AssemblyExpr.Register.RegisterName.R10, AssemblyExpr.Register.RegisterSize._8Bits), "R10B" },

                { (AssemblyExpr.Register.RegisterName.R11, AssemblyExpr.Register.RegisterSize._64Bits), "R11" },
                { (AssemblyExpr.Register.RegisterName.R11, AssemblyExpr.Register.RegisterSize._32Bits), "R11D" },
                { (AssemblyExpr.Register.RegisterName.R11, AssemblyExpr.Register.RegisterSize._16Bits), "R11W" },
                { (AssemblyExpr.Register.RegisterName.R11, AssemblyExpr.Register.RegisterSize._8Bits), "R11B" },

                { (AssemblyExpr.Register.RegisterName.R12, AssemblyExpr.Register.RegisterSize._64Bits), "R12" },
                { (AssemblyExpr.Register.RegisterName.R12, AssemblyExpr.Register.RegisterSize._32Bits), "R12D" },
                { (AssemblyExpr.Register.RegisterName.R12, AssemblyExpr.Register.RegisterSize._16Bits), "R12W" },
                { (AssemblyExpr.Register.RegisterName.R12, AssemblyExpr.Register.RegisterSize._8Bits), "R12B" },

                { (AssemblyExpr.Register.RegisterName.R13, AssemblyExpr.Register.RegisterSize._64Bits), "R13" },
                { (AssemblyExpr.Register.RegisterName.R13, AssemblyExpr.Register.RegisterSize._32Bits), "R13D" },
                { (AssemblyExpr.Register.RegisterName.R13, AssemblyExpr.Register.RegisterSize._16Bits), "R13W" },
                { (AssemblyExpr.Register.RegisterName.R13, AssemblyExpr.Register.RegisterSize._8Bits), "R13B" },

                { (AssemblyExpr.Register.RegisterName.R14, AssemblyExpr.Register.RegisterSize._64Bits), "R14" },
                { (AssemblyExpr.Register.RegisterName.R14, AssemblyExpr.Register.RegisterSize._32Bits), "R14D" },
                { (AssemblyExpr.Register.RegisterName.R14, AssemblyExpr.Register.RegisterSize._16Bits), "R14W" },
                { (AssemblyExpr.Register.RegisterName.R14, AssemblyExpr.Register.RegisterSize._8Bits), "R14B" },

                { (AssemblyExpr.Register.RegisterName.R15, AssemblyExpr.Register.RegisterSize._64Bits), "R15" },
                { (AssemblyExpr.Register.RegisterName.R15, AssemblyExpr.Register.RegisterSize._32Bits), "R15D" },
                { (AssemblyExpr.Register.RegisterName.R15, AssemblyExpr.Register.RegisterSize._16Bits), "R15W" },
                { (AssemblyExpr.Register.RegisterName.R15, AssemblyExpr.Register.RegisterSize._8Bits), "R15B" },
            };
        }

        partial class AttSyntax : ISyntaxFactory, AssemblyExpr.IVisitor<string>
        {
            public AttSyntax()
            {
                header = new AssemblyExpr.Comment("Raze Compiler Version ALPHA 0.0.0 Intel_x86-64 GAS");
            }

            public override void Run(CodeGen.ISection instructions)
            {
                foreach (var instruction in instructions)
                {
                    instruction.Accept(this);
                    Console.WriteLine();
                }
            }

            public override void Run(AssemblyExpr instruction)
            {
                instruction.Accept(this);
            }

            public string VisitBinary(AssemblyExpr.Binary instruction)
            {
                throw new NotImplementedException();
            }

            public string VisitComment(AssemblyExpr.Comment instruction)
            {
                throw new NotImplementedException();
            }

            public string VisitData(AssemblyExpr.Data instruction)
            {
                throw new NotImplementedException();
            }

            public string VisitGlobal(AssemblyExpr.Global instruction)
            {
                throw new NotImplementedException();
            }

            public string VisitLocalProcedure(AssemblyExpr.LocalProcedure instruction)
            {
                throw new NotImplementedException();
            }

            public string VisitProcedure(AssemblyExpr.Procedure instruction)
            {
                throw new NotImplementedException();
            }

            public string VisitSection(AssemblyExpr.Section instruction)
            {
                throw new NotImplementedException();
            }

            public string VisitUnary(AssemblyExpr.Unary instruction)
            {
                throw new NotImplementedException();
            }

            public string VisitZero(AssemblyExpr.Zero instruction)
            {
                throw new NotImplementedException();
            }
        }
    }
}
