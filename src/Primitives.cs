using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze
{
    internal partial class Analyzer
    {
        internal class Primitives
        {
            // Binary Operation
            public static Expr.Type Operation(Token op, int t1, Expr a, int t2, Expr b)
            {
                string pName = SymbolToPrimitiveName(op);
                throw new NotImplementedException();
            }
            public static void InvalidOperation(Token op, string t1, string t2)
            {
                throw new Errors.AnalyzerError("Invalid Operator", $"Types '{t1}' and '{t2}' don't have a definition for '{SymbolToPrimitiveName(op)}' ( '{op.lexeme}' )");
            }

            // Unary Operation
            public static Expr.Type Operation(Token op, int t1, Expr a)
            {
                string pName = SymbolToPrimitiveName(op);
                throw new NotImplementedException();
            }
            public static void InvalidOperation(Token op, string t1)
            {
                throw new Errors.AnalyzerError("Invalid Operator", $"Type '{t1}' doesn't not have a definition for '{SymbolToPrimitiveName(op)}' ( '{op.lexeme}' )");
            }

            public static bool IsVoidType(Expr.Type type)
            {
                return type.name.lexeme == "void";
            }

            public static (bool, int) IsLiteralTypeOrVoid(Expr.Type type)
            {
                for (byte i = 0; i < Parser.Literals.Length; i++)
                {
                    if (IsLiteralType(type, i))
                    {
                        return (true, i);
                    }
                }
                if (IsVoidType(type))
                {
                    return (true, -1);
                }
                return (false, -1);
            }
            public static bool IsLiteralType(Expr.Type type, byte literal)
            {
                return type.name.type == Parser.Literals[literal];
            }

            public static string SymbolToPrimitiveName(Token op)
            {
                return op.type switch
                {
                    Token.TokenType.PLUS => "Add",
                    Token.TokenType.MINUS => "Subtract",
                    Token.TokenType.MULTIPLY => "Multiply",
                    Token.TokenType.MODULO => "Modulo",
                    Token.TokenType.DIVIDE => "Divide",
                    Token.TokenType.B_AND => "BitwiseAnd",
                    Token.TokenType.B_OR => "BitwiseOr",
                    Token.TokenType.B_XOR => "BitwiseXor",
                    Token.TokenType.SHIFTLEFT => "BitwiseShiftLeft",
                    Token.TokenType.SHIFTRIGHT => "BitwiseShiftRight",
                    Token.TokenType.PLUSPLUS => "Increment",
                    Token.TokenType.MINUSMINUS => "Decrement",
                    Token.TokenType.NOT => "Not"
                };
            }
        }
    }
}
