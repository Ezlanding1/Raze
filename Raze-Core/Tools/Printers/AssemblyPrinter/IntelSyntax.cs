using System;
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
        // "Intel" syntax refers to NASM's variant of Intel syntax. In the future, this could be split into separate Intel/MASM and NASM outputs
        class IntelSyntax : ISyntaxFactory, AssemblyExpr.IVisitor<string>, AssemblyExpr.IUnaryOperandVisitor<string>
        {
            private Dictionary<(AssemblyExpr.Register.RegisterName, AssemblyExpr.Register.RegisterSize), string> RegisterToString =
                InstructionUtils.Registers.ToDictionary(x => x.Value, x => x.Key);

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
                Output.AppendLine(instruction.Accept(this));
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
                    return $"{(string.IsNullOrEmpty(instruction.name) ? "" : (instruction.name + ": "))}{dataSize[instruction.Size]} {UnescapeString(instruction.literal.value.SelectMany(x => x).ToArray())}";
                }
                else if (instruction.literal.type >= AssemblyExpr.Literal.LiteralType.RefData)
                {
                    return $"{(string.IsNullOrEmpty(instruction.name) ? "" : (instruction.name + ": "))}{dataSize[AssemblyExpr.Register.RegisterSize._64Bits]} {string.Join(", ", instruction.literal.value.Select(x => VisitImmediate(new AssemblyExpr.LabelLiteral(instruction.literal.type, x))))}";
                }
                return $"{(string.IsNullOrEmpty(instruction.name) ? "" : (instruction.name + ": "))}{dataSize[instruction.Size]} {string.Join(", ", instruction.literal.value.Select(x => VisitImmediate(new(instruction.literal.type, x))))}";
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
                return $"{wordSize[instruction.Size]} " + PointerToString(instruction);
            }

            public string VisitRegister(AssemblyExpr.Register instruction)
            {
                if (instruction.name == AssemblyExpr.Register.RegisterName.TMP)
                {
                    Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("TMP Register Cannot Be Emitted"));
                }

                return $"{RegisterToString[(instruction.name, instruction.Size)]}";
            }

            public string VisitSection(AssemblyExpr.Section instruction)
            {
                return $"\nsection .{instruction.name}";
            }

            public string VisitUnary(AssemblyExpr.Unary instruction)
            {
                return $"{instruction.instruction}\t{instruction.operand.Accept(this)}";
            }

            public string VisitZero(AssemblyExpr.Nullary instruction)
            {
                return $"{instruction.instruction}";
            }

            public string VisitInclude(AssemblyExpr.Include instruction)
            {
                return $"extern {instruction.importedFunctionName}";
            }

            private string PointerToString(AssemblyExpr.Pointer instruction)
            {
                if (instruction.value?.name == AssemblyExpr.Register.RegisterName.TMP)
                {
                    Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("TMP Register Cannot Be Emitted"));
                }

                if (instruction.offset.value.All(x => x == 0))
                {
                    return $"[{instruction.value?.Accept(this)}]";
                }
                return $"[{instruction.value?.Accept(this)}{ImmediateToStringWithSign(instruction.offset)}]";
            }

            private string ImmediateToStringWithSign(AssemblyExpr.Literal instruction)
            {
                var value = instruction.Accept(this);
                if (instruction.type == AssemblyExpr.Literal.LiteralType.Integer && value.StartsWith('-'))
                {
                    return " - " + value[1..];
                }
                return " + " + value;
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

                        if (i != escapedString.Length - 1)
                            unescapedString.Append(", \"");
                        continue;
                    }
                    if (i == 0)
                        unescapedString.Append('"');
                    unescapedString.Append(escapedString[i]);
                    if (i == escapedString.Length - 1)
                        unescapedString.Append('"');
                }
                return unescapedString.ToString();
            }

            private static string IntegralImmediateToString(byte[] value, int _base)
            {
                var bytes = value.ToArray();
                AssemblyExpr.ImmediateGenerator.ResizeSignedInteger(ref bytes, 8);
                return Convert.ToString(BitConverter.ToInt64(bytes), _base);
            }

            internal readonly static Dictionary<AssemblyExpr.Register.RegisterSize, string> wordSize = new()
            {
                { AssemblyExpr.Register.RegisterSize._64Bits, "QWORD"}, // 64-Bits
                { AssemblyExpr.Register.RegisterSize._32Bits, "DWORD"}, // 32-Bits
                { AssemblyExpr.Register.RegisterSize._16Bits, "WORD"}, // 16-Bits
                { AssemblyExpr.Register.RegisterSize._8BitsUpper, "BYTE"}, // 8-Bits
                { AssemblyExpr.Register.RegisterSize._8Bits, "BYTE"}, // 8-Bits
            };

            internal readonly static Dictionary<AssemblyExpr.Register.RegisterSize, string> dataSize = new()
            {
                { AssemblyExpr.Register.RegisterSize._64Bits, "dq"}, // 64-Bits
                { AssemblyExpr.Register.RegisterSize._32Bits, "dd"}, // 32-Bits
                { AssemblyExpr.Register.RegisterSize._16Bits, "dw"}, // 16-Bits
                { AssemblyExpr.Register.RegisterSize._8BitsUpper, "db"}, // 8-Bits
                { AssemblyExpr.Register.RegisterSize._8Bits, "db"}, // 8-Bits
            };
        }
    }
}
