using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Parser
{
    private class AssemblyParser(Parser parser)
    {
        readonly Parser parser = parser;
        bool returned = false;

        List<Expr.InlineAssembly.InlineAssemblyExpr> instructions = [];

        Dictionary<string, Expr.InlineAssembly.NamedRegister> namedRegisters = new();

        public Expr.InlineAssembly ParseInlineAssemblyBlock()
        {
            parser.Expect(Token.TokenType.LBRACE, "'{' before Assembly Block body");

            while (!parser.TypeMatch(Token.TokenType.RBRACE))
            {
                var asmExpr = ParseAssemblyStatement();
                
                if (asmExpr != null)
                {
                    parser.Expect(Token.TokenType.SEMICOLON, "after inline assembly expression");
                    instructions.Add(asmExpr);
                }
                else
                {
                    parser.Advance();
                }
            }

            foreach (var unfreedRegister in namedRegisters)
            {
                instructions.Add(new Expr.InlineAssembly.Free(unfreedRegister.Value));
            }

            return new Expr.InlineAssembly(instructions);
        }

        private Expr.InlineAssembly.InlineAssemblyExpr? ParseAssemblyStatement()
        {
            if (parser.ReservedValueMatch("return"))
            {
                if (returned)
                {
                    Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.InvalidAssemblyBlockReturn, parser.Previous().location, []));
                }
                returned = true;

                int idx = parser.index;

                var operand = ParseOperand();
                if (operand != null && parser.TypeMatch(Token.TokenType.SEMICOLON))
                {
                    parser.MovePrevious();
                    return new Expr.InlineAssembly.Return(operand);
                }
                parser.index = idx-1;
                parser.Advance();

                parser.Expect(Token.TokenType.IDENTIFIER, "after 'return'");

                var asmExpr = ParseInstruction();
                if (asmExpr != null)
                {
                    asmExpr._return = true;
                    return asmExpr;
                }

                return null;
            }

            if (parser.TypeMatch(Token.TokenType.IDENTIFIER))
            {
                parser.MovePrevious();

                if (parser.ValueMatch("alloc"))
                {
                    parser.Expect(Token.TokenType.IDENTIFIER, "after 'alloc'");

                    string name = parser.Previous().lexeme.ToUpper();

                    if (InstructionUtils.Registers.TryGetValue(name, out var reg))
                    {
                        name = reg.Item1.ToString();
                        namedRegisters[name] = new Expr.InlineAssembly.NamedRegister(null);

                        return new Expr.InlineAssembly.NamedAlloc(namedRegisters[name], reg.Item1, reg.Item2);
                    }
                    else
                    {
                        namedRegisters[name] = new Expr.InlineAssembly.NamedRegister(null);

                        (var type, var size) = ParseRegisterOptions();
                        return new Expr.InlineAssembly.UnnamedAlloc(namedRegisters[name], type, size);
                    }
                }
                else if (parser.ValueMatch("free"))
                {
                    parser.Expect(Token.TokenType.IDENTIFIER, "after 'free'");

                    string name = parser.Previous().lexeme.ToUpper();

                    if (InstructionUtils.Registers.TryGetValue(name, out var reg))
                    {
                        name = reg.Item1.ToString();
                    }
                    
                    if (namedRegisters.TryGetValue(name, out Expr.InlineAssembly.NamedRegister? value))
                    {
                        var free = new Expr.InlineAssembly.Free(value);
                        namedRegisters.Remove(name);
                        return free;
                    }
                    else
                    {
                        Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.InlineAssemblyInvalidFreeOperand, parser.Previous().location, name));
                    }
                }
                else
                {
                    parser.Advance();
                    return ParseInstruction();
                }
            }

            return null;
        }

        private Expr.InlineAssembly.Instruction ParseInstruction()
        {
            if (!Enum.TryParse(parser.Previous().lexeme, out AssemblyExpr.Instruction instruction))
            {
                Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.UnsupportedInstruction, parser.Previous().location, parser.Previous().lexeme));
            }
            
            var op1 = ParseOperand();

            if (op1 != null)
            {
                if (parser.TypeMatch(Token.TokenType.COMMA))
                {
                    var op2 = ParseOperand();

                    if (op2 != null)
                    {
                        return new Expr.InlineAssembly.BinaryInstruction(instruction, op1, op2);
                    }
                    parser.End();
                }

                return new Expr.InlineAssembly.UnaryInstruction(instruction, op1);
            }

            return new Expr.InlineAssembly.NullaryInstruction(instruction);
        }

        private Expr.InlineAssembly.Operand? ParseOperand()
        {
            if (parser.TypeMatch(Token.TokenType.DOLLAR))
            {
                return ParseVariable();
            }
            else if (parser.TypeMatch(Token.TokenType.LBRACKET))
            {
                return ParsePointer();
            }
            else if (parser.TypeMatch(Token.TokenType.IDENTIFIER))
            {
                return parser.Previous().lexeme.ToUpper() switch
                {
                    "QWORD" => ParsePointerAfterPtrSize(AssemblyExpr.Register.RegisterSize._64Bits),
                    "DWORD" => ParsePointerAfterPtrSize(AssemblyExpr.Register.RegisterSize._32Bits),
                    "WORD" => ParsePointerAfterPtrSize(AssemblyExpr.Register.RegisterSize._16Bits),
                    "BYTE" => ParsePointerAfterPtrSize(AssemblyExpr.Register.RegisterSize._8Bits),
                    _ => ParseRegister()
                };
            }
            else if (parser.TypeMatch(Token.TokenType.INTEGER, Token.TokenType.REF_STRING, Token.TokenType.FLOATING, Token.TokenType.STRING))
            {
                return new Expr.InlineAssembly.Literal(new(new((LiteralTokenType)parser.Previous().type, parser.Previous().lexeme, parser.Previous().location)));
            }
            else
            {
                return null;
            }
        }

        private Expr.InlineAssembly.Register ParseRegister()
        {
            string name = parser.Previous().lexeme.ToUpper();

            if (InstructionUtils.Registers.TryGetValue(name, out var reg))
            {
                if (namedRegisters.TryGetValue(reg.Item1.ToString(), out Expr.InlineAssembly.NamedRegister? value))
                {
                    return value;
                }
                return new Expr.InlineAssembly.NamedRegister(new(reg.Item1, reg.Item2));
            }
            else if (namedRegisters.TryGetValue(name, out Expr.InlineAssembly.NamedRegister? value))
            {
                return value;
            }

            (var type, var size) = ParseRegisterOptions();
            return new Expr.InlineAssembly.UnnamedRegister(type, size);
        }

        private Expr.InlineAssembly.Pointer ParsePointerAfterPtrSize(AssemblyExpr.Register.RegisterSize? size = null)
        {
            parser.Expect(Token.TokenType.LBRACKET, "after pointer size");
            return ParsePointer(size);
        }
        private Expr.InlineAssembly.Pointer ParsePointer(AssemblyExpr.Register.RegisterSize? size = null)
        {
            Expr.InlineAssembly.Operand value;

            if (parser.TypeMatch(Token.TokenType.DOLLAR))
            {
                value = ParseVariable();
            }
            else
            {
                parser.Expect(Token.TokenType.IDENTIFIER, "after '['");
                value = ParseRegister();
            }

            int offset = 0;
            if (parser.TypeMatch(Token.TokenType.PLUS, Token.TokenType.MINUS))
            {
                string offsetStr = parser.Previous().type == Token.TokenType.MINUS ? "-" : "+";
                parser.Expect(Token.TokenType.INTEGER, "pointer offset after sign");
                offsetStr += parser.Previous().lexeme;

                try
                {
                    offset = int.Parse(offsetStr);
                }
                catch (OverflowException)
                {
                    Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.InlineAssemblyInvalidPtrOffset, parser.Previous().location, offsetStr));
                }
                catch (FormatException)
                {
                    throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("Invalid formatting of ptr offset in inline asm"));
                }
            }

            parser.Expect(Token.TokenType.RBRACKET, "']' to close pointer");

            return new Expr.InlineAssembly.Pointer(value, offset, size);
        }

        private (LiteralTokenType, AssemblyExpr.Register.RegisterSize) ParseRegisterOptions()
        {
            var type = LiteralTokenType.Integer;
            var size = AssemblyExpr.Register.RegisterSize._32Bits;

            if (parser.TypeMatch(Token.TokenType.COLON) && parser.TypeMatch(Token.TokenType.IDENTIFIER))
            {
                LiteralTokenType FailParse()
                {
                    Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.InlineAssemblyInvalidRegisterOption, parser.Previous().location, parser.Previous().lexeme));
                    return LiteralTokenType.Integer;
                }

                type = parser.Previous().lexeme.ToUpper() switch
                {
                    "R" => LiteralTokenType.Integer,
                    "X" => LiteralTokenType.Floating,
                    _ => FailParse()
                };
            }
            if (parser.TypeMatch(Token.TokenType.COLON)
                && parser.TypeMatch(Token.TokenType.INTEGER)
                && new List<string> { "64", "32", "16", "8" }.Contains(parser.Previous().lexeme))
            {
                size = (AssemblyExpr.Register.RegisterSize)(int.Parse(parser.Previous().lexeme) / 8);
            }

            return (type, size);
        }

        private Expr.InlineAssembly.Variable ParseVariable()
        {
            if (!(parser.TypeMatch(Token.TokenType.IDENTIFIER) || parser.ReservedValueMatch("this")))
                parser.Expected("IDENTIFIER, 'this'", "after escape '$'");

            return new Expr.InlineAssembly.Variable(new Expr.AmbiguousGetReference(parser.Previous(), true));
        }
    }
}
