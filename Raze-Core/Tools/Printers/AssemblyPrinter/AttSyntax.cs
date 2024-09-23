using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze.Tools;

partial class Syntaxes
{
    partial class SyntaxFactory
    {
        class AttSyntax : ISyntaxFactory, AssemblyExpr.IVisitor<string>, AssemblyExpr.IUnaryOperandVisitor<string>
        {
            private Dictionary<(AssemblyExpr.Register.RegisterName, AssemblyExpr.Register.RegisterSize), string> RegisterToString =
                InstructionUtils.Registers.ToDictionary(x => x.Value, x => x.Key.ToLower());

            public AttSyntax()
            {
                header = new AssemblyExpr.Comment("Raze Compiler Version ALPHA 0.0.0 Intel_x86-64 GAS");
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
                string instructionName = instruction.instruction.ToString().ToLower();
                
                if (!NoSpecifySizeOperand1(instruction.instruction)) 
                    instructionName += SizeMnemonic(instruction.operand1.Size);

                if (SpecifySizeOperand2(instruction.instruction))
                    instructionName = instructionName[..^2] + SizeMnemonic(instruction.operand2.Size) + SizeMnemonic(instruction.operand1.Size);


                return $"{instructionName}\t{instruction.operand2.Accept(this)}, {instruction.operand1.Accept(this)}";
            }

            public string VisitComment(AssemblyExpr.Comment instruction)
            {
                return $"# {instruction.comment}";
            }

            public string VisitData(AssemblyExpr.Data instruction)
            {
                string directiveName = instruction.literal.type switch
                {
                    AssemblyExpr.Literal.LiteralType.Integer or
                    AssemblyExpr.Literal.LiteralType.UnsignedInteger or
                    AssemblyExpr.Literal.LiteralType.Floating or
                    AssemblyExpr.Literal.LiteralType.Boolean => instruction.Size switch
                        { 
                            AssemblyExpr.Register.RegisterSize._8Bits => ".byte",
                            AssemblyExpr.Register.RegisterSize._16Bits => ".word",
                            AssemblyExpr.Register.RegisterSize._32Bits => ".long",
                            AssemblyExpr.Register.RegisterSize._64Bits => ".quad",
                        },
                    AssemblyExpr.Literal.LiteralType.String when (instruction.literal.value.Last()[^1] == 0) => ".string",
                    AssemblyExpr.Literal.LiteralType.String => ".ascii",
                    AssemblyExpr.Literal.LiteralType.RefData or 
                    AssemblyExpr.Literal.LiteralType.RefProcedure or
                    AssemblyExpr.Literal.LiteralType.RefLocalProcedure => ".quad"
                };

                string result = $"{(string.IsNullOrEmpty(instruction.name) ? "" : ("." + instruction.name + ": "))}\n\t{directiveName} ";

                if (instruction.literal.type == AssemblyExpr.Literal.LiteralType.String)
                {
                    return result + UnescapeString(instruction.literal.value.SelectMany(x => x).ToArray());
                }
                else if (instruction.literal.type >= AssemblyExpr.Literal.LiteralType.RefData)
                {
                    return result + string.Join(", ", instruction.literal.value.Select(x => VisitImmediate(new AssemblyExpr.LabelLiteral(instruction.literal.type, x))[1..]));
                }
                return result + string.Join(", ", instruction.literal.value.Select(x => VisitImmediate(new(instruction.literal.type, x))[1..]));
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
                return $".global {instruction.name}";
            }

            public string VisitMemory(AssemblyExpr.Pointer instruction)
            {
                if (instruction.GetRegister()?.Name == AssemblyExpr.Register.RegisterName.TMP)
                {
                    Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("TMP Register Cannot Be Emitted"));
                }

                if (instruction.offset.value.All(x => x == 0))
                {
                    return $"({instruction.value?.Accept(this)})";
                }
                return $"{instruction.offset.Accept(this)[1..]}({instruction.value?.Accept(this) ?? "%rip"})";
            }

            public string VisitRegister(AssemblyExpr.Register instruction)
            {
                if (instruction.Name == AssemblyExpr.Register.RegisterName.TMP)
                {
                    Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("TMP Register Cannot Be Emitted"));
                }

                return $"%{RegisterToString[(instruction.Name, instruction.Size)]}";
            }

            public string VisitSection(AssemblyExpr.Section instruction)
            {
                return $"\n.{instruction.name}";
            }

            public string VisitUnary(AssemblyExpr.Unary instruction)
            {
                string instructionName = instruction.instruction.ToString().ToLower() + SizeMnemonic(instruction.operand.Size);

                if (NoOperandSizeNoRefDollarPrefix(instruction.instruction))
                { 
                    if (instruction.operand.IsLiteral(out var lbl) && ((AssemblyExpr.Literal)lbl).type >= AssemblyExpr.Literal.LiteralType.RefData)
                        return $"{instructionName[..^1]}\t{instruction.operand.Accept(this).ToString()[1..]}";
                    else
                        return $"{instructionName[..^1]}\t*{instruction.operand.Accept(this)}";
                }
                else if (LiteralMustBeResized(instruction.instruction, instruction.operand, out var newSize))
                {
                    instructionName = instructionName[..^1] + SizeMnemonic(newSize);
                }

                return $"{instructionName}\t{instruction.operand.Accept(this)}";
            }

            public string VisitZero(AssemblyExpr.Nullary instruction)
            {
                return $"{instruction.instruction.ToString().ToLower()}";
            }

            public string VisitImmediate(AssemblyExpr.Literal instruction)
            {
                switch (instruction.type)
                {
                    case AssemblyExpr.Literal.LiteralType.String:
                        {
                            if (instruction.value.Length == 0) return "$0";

                            int strAsInt = instruction.value[^1];
                            for (int i = instruction.value.Length - 2; i >= 0; i--)
                            {
                                strAsInt <<= 8;
                                strAsInt += instruction.value[i];
                            }
                            return $"${strAsInt}";
                        }
                    case AssemblyExpr.Literal.LiteralType.RefProcedure:
                        return $"${((AssemblyExpr.LabelLiteral)instruction).Name}";
                    case AssemblyExpr.Literal.LiteralType.RefData:
                    case AssemblyExpr.Literal.LiteralType.RefLocalProcedure:
                        return $"$.{((AssemblyExpr.LabelLiteral)instruction).Name}";
                    case AssemblyExpr.Literal.LiteralType.UnsignedInteger:
                        {
                            byte[] number = instruction.value.ToArray();
                            Array.Resize(ref number, 8);
                            return "$" + BitConverter.ToUInt64(number).ToString();
                        }
                    default:
                        return "$" + IntegralImmediateToString(instruction.value, 10);
                }
            }

            private string SizeMnemonic(AssemblyExpr.Register.RegisterSize size) => size switch
            { 
                AssemblyExpr.Register.RegisterSize._8Bits => "b",
                AssemblyExpr.Register.RegisterSize._16Bits => "w",
                AssemblyExpr.Register.RegisterSize._32Bits => "l",
                _ => "q",
            };

            private bool NoSpecifySizeOperand1(AssemblyExpr.Instruction instruction)
            {
                return new List<AssemblyExpr.Instruction>()
                {
                    AssemblyExpr.Instruction.MOVSS,
                    AssemblyExpr.Instruction.MOVSD,
                    AssemblyExpr.Instruction.ADDSS,
                    AssemblyExpr.Instruction.ADDSD
                }
                .Contains(instruction);
            }
            private bool SpecifySizeOperand2(AssemblyExpr.Instruction instruction)
            {
                return instruction == AssemblyExpr.Instruction.MOVSX || instruction == AssemblyExpr.Instruction.MOVZX;
            }
            private bool NoOperandSizeNoRefDollarPrefix(AssemblyExpr.Instruction instruction)
            {
                return new List<AssemblyExpr.Instruction>()
                {
                    AssemblyExpr.Instruction.CALL,
                    AssemblyExpr.Instruction.JMP,
                    AssemblyExpr.Instruction.JE,
                    AssemblyExpr.Instruction.JNE,
                    AssemblyExpr.Instruction.JG,
                    AssemblyExpr.Instruction.JL,
                    AssemblyExpr.Instruction.JGE,
                    AssemblyExpr.Instruction.JLE,
                    AssemblyExpr.Instruction.JA,
                    AssemblyExpr.Instruction.JAE,
                    AssemblyExpr.Instruction.JB,
                    AssemblyExpr.Instruction.JBE,
                }
                .Contains(instruction);
            }

            private bool LiteralMustBeResized(AssemblyExpr.Instruction instruction, AssemblyExpr.IValue operand, out AssemblyExpr.Register.RegisterSize newSize)
            {
                if (operand.IsLiteral() && instruction == AssemblyExpr.Instruction.PUSH && operand.Size == AssemblyExpr.Register.RegisterSize._32Bits)
                {
                    newSize = AssemblyExpr.Register.RegisterSize._64Bits;
                    return true;
                }

                newSize = (AssemblyExpr.Register.RegisterSize)(-1);
                return false;
            }

            private string UnescapeString(byte[] bytes)
            {
                if (bytes.Length == 0) return "\"\"";

                string escapedString = Encoding.ASCII.GetString(bytes);
                List<char> escapedChars = Lexer.stringEscapeCodes.Select(x => x.Item2).ToList();

                StringBuilder unescapedString = new StringBuilder();
                unescapedString.Append('"');

                int end = (bytes[^1] == 0) ? 
                    escapedString.Length - 1 : 
                    escapedString.Length;

                for (int i = 0; i < end; i++)
                {
                    if (escapedChars.Contains(escapedString[i]))
                    {
                        if (new List<char>{ '\b', '\f', '\n', '\r', '\t', '\\', '\"' }.Contains(escapedString[i]))
                        {
                            unescapedString.Append(Lexer.stringEscapeCodes[escapedChars.IndexOf(escapedString[i])].Item1.ToString()[1..]);
                        }
                        else
                        {
                            unescapedString.Append("\\" + Convert.ToString((byte)escapedString[i], 8).PadLeft(3, '0'));
                        }
                    }
                    else
                    {
                        unescapedString.Append(escapedString[i]);
                    }
                }

                unescapedString.Append('"');
                return unescapedString.ToString();
            }

            private static string IntegralImmediateToString(byte[] value, int _base)
            {
                var bytes = value.ToArray();
                AssemblyExpr.ImmediateGenerator.ResizeSignedInteger(ref bytes, 8);
                return Convert.ToString(BitConverter.ToInt64(bytes), _base);
            }
        }
    }
}
