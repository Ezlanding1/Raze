using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Raze.Parser.LiteralTokenType;

namespace Raze;

public partial class Analyzer
{
    internal class Primitives
    {
        private static dynamic ToType(AssemblyExpr.UnresolvedLiteral literal) => literal.type switch
        {            
            AssemblyExpr.Literal.LiteralType.Integer => long.Parse(literal.value),
            AssemblyExpr.Literal.LiteralType.Floating => double.Parse(literal.value),
            AssemblyExpr.Literal.LiteralType.String => literal.value,
            AssemblyExpr.Literal.LiteralType.Binary => Convert.ToInt64(literal.value, 2),
            AssemblyExpr.Literal.LiteralType.Hex => Convert.ToInt64(literal.value, 16),
            AssemblyExpr.Literal.LiteralType.Boolean => byte.Parse(literal.value),
            AssemblyExpr.Literal.LiteralType.RefData => literal.value
        };

        // Binary Operation
        public static Parser.LiteralTokenType OperationType(Token op, Parser.LiteralTokenType type1, Parser.LiteralTokenType type2)
        {
            if (type1 == Parser.VoidTokenType || type2 == Parser.VoidTokenType)
            {
                Diagnostics.Report(InvalidOperation(op, type1, type2));
                return Parser.VoidTokenType;
            }

            string pName = SymbolToPrimitiveName(op);

            // Note: All cases where an operator's return type for two given literals is not correctly expressed in the switch should be handled individually, here

            if (type1 == Parser.LiteralTokenType.String && type2 == Parser.LiteralTokenType.String && pName == "Add")
            {
                return Parser.LiteralTokenType.String;
            }
            if ((type1 == Floating || type2 == Floating) && (pName == "BitwiseShiftLeft" || pName == "BitwiseShiftRight" || pName == "BitwiseAnd" || pName == "BitwiseOr" || pName == "BitwiseXor"))
            {
                Diagnostics.Report(InvalidOperation(op, type1, type2));
                return Parser.VoidTokenType;
            }
            if (pName == "EqualTo" || pName == "NotEqualTo" || pName == "GreaterThan" || pName == "LessThan" || pName == "GreaterThanOrEqualTo" || pName == "LessThanOrEqualTo")
            {
                return Parser.LiteralTokenType.Boolean;
            }
            if (type1 == Parser.LiteralTokenType.Boolean || type2 == Parser.LiteralTokenType.Boolean)
            {
                if (pName == "BitwiseAnd" || pName == "BitwiseOr" || pName == "BitwiseXor")
                {
                    return Parser.LiteralTokenType.Boolean;
                }
                else
                {
                    Diagnostics.Report(InvalidOperation(op, type1, type2));
                }
            }

            switch (type1, type2)
            {
                case (Integer, Integer): return Integer; // INTEGER OP INTEGER
                case (Integer, Floating): case (Floating, Integer): return Floating; // INTEGER OP FLOATING
                case (Integer, Parser.LiteralTokenType.String):
                case (Parser.LiteralTokenType.String, Integer):
                case (Integer, RefString):
                case (RefString, Integer): Diagnostics.Report(InvalidOperation(op, type1, type2)); break;  // INTEGER OP STRING
                case (Integer, Binary): case (Binary, Integer): return Integer; // INTEGER OP BINARY
                case (Integer, Hex): case (Hex, Integer): return Integer; // INTEGER OP HEX
                case (Integer, Parser.LiteralTokenType.Boolean): case (Parser.LiteralTokenType.Boolean, Integer): Diagnostics.Report(InvalidOperation(op, type1, type2)); break;  // INTEGER OP BOOLEAN

                case (Floating, Floating): return Floating; // FLOATING OP FLOATING
                case (Floating, Parser.LiteralTokenType.String):
                case (Parser.LiteralTokenType.String, Floating):
                case (Floating, RefString):
                case (RefString, Floating): Diagnostics.Report(InvalidOperation(op, type1, type2)); break; // FLOATING OP STRING
                case (Floating, Binary): case (Binary, Floating): Diagnostics.Report(InvalidOperation(op, type1, type2)); break;  // FLOATING OP BINARY
                case (Floating, Hex): case (Hex, Floating): Diagnostics.Report(InvalidOperation(op, type1, type2)); break;  // FLOATING OP HEX
                case (Floating, Parser.LiteralTokenType.Boolean): case (Parser.LiteralTokenType.Boolean, Floating): Diagnostics.Report(InvalidOperation(op, type1, type2)); break;  // FLOATING OP BOOLEAN

                case (Parser.LiteralTokenType.String, Parser.LiteralTokenType.String):
                case (RefString, RefString): Diagnostics.Report(InvalidOperation(op, type1, type2)); break; // STRING OP STRING
                case (Parser.LiteralTokenType.String, Binary):
                case (Binary, Parser.LiteralTokenType.String):
                case (RefString, Binary):
                case (Binary, RefString): Diagnostics.Report(InvalidOperation(op, type1, type2)); break;  // STRING OP BINARY
                case (Parser.LiteralTokenType.String, Hex):
                case (Hex, Parser.LiteralTokenType.String):
                case (RefString, Hex):
                case (Hex, RefString): Diagnostics.Report(InvalidOperation(op, type1, type2)); break;  // STRING OP HEX
                case (Parser.LiteralTokenType.String, Parser.LiteralTokenType.Boolean):
                case (Parser.LiteralTokenType.Boolean, Parser.LiteralTokenType.String):
                case (RefString, Parser.LiteralTokenType.Boolean):
                case (Parser.LiteralTokenType.Boolean, RefString): Diagnostics.Report(InvalidOperation(op, type1, type2)); break;  // STRING OP BOOLEAN

                case (Binary, Binary): return Integer; // BINARY OP BINARY
                case (Binary, Hex): case (Hex, Binary): Diagnostics.Report(InvalidOperation(op, type1, type2)); break;  // BINARY OP HEX
                case (Binary, Parser.LiteralTokenType.Boolean): case (Parser.LiteralTokenType.Boolean, Binary): Diagnostics.Report(InvalidOperation(op, type1, type2)); break;  // BINARY OP BOOLEAN

                case (Hex, Hex): return Integer; // HEX OP HEX
                case (Hex, Parser.LiteralTokenType.Boolean): case (Parser.LiteralTokenType.Boolean, Hex): Diagnostics.Report(InvalidOperation(op, type1, type2)); break;  // HEX OP BOOLEAN

                case (Parser.LiteralTokenType.Boolean, Parser.LiteralTokenType.Boolean): return Parser.LiteralTokenType.Boolean; // BOOLEAN OP BOOLEAN

                default:
                    throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic($"Unrecognized literal operation"));
            }
            return Parser.VoidTokenType;
        }

        public static AssemblyExpr.UnresolvedLiteral Operation(Token op, AssemblyExpr.UnresolvedLiteral a, AssemblyExpr.UnresolvedLiteral b, CodeGen assembler)
        {
            string pName = SymbolToPrimitiveName(op);

            string result =
                (
                    pName switch
                    {
                        "Add" => Add(ToType(a), ToType(b), (a.type == b.type && a.type == AssemblyExpr.Literal.LiteralType.RefData), assembler),
                        "Subtract" => ToType(a) - ToType(b),
                        "Multiply" => ToType(a) * ToType(b),
                        "Modulo" => ToType(a) % ToType(b),
                        "Divide" => ToType(a) / ToType(b),
                        "BitwiseAnd" => ToType(a) & ToType(b),
                        "BitwiseOr" => ToType(a) | ToType(b),
                        "BitwiseXor" => ToType(a) ^ ToType(b),
                        "BitwiseShiftLeft" => ToType(a) << ToType(b),
                        "BitwiseShiftRight" => ToType(a) >> ToType(b),
                        "EqualTo" => Convert.ToByte(ToType(a) == ToType(b)),
                        "NotEqualTo" => Convert.ToByte(ToType(a) != ToType(b)),
                        "GreaterThan" => Convert.ToByte(ToType(a) > ToType(b)),
                        "LessThan" => Convert.ToByte(ToType(a) < ToType(b)),
                        "GreaterThanOrEqualTo" => Convert.ToByte(ToType(a) >= ToType(b)),
                        "LessThanOrEqualTo" => Convert.ToByte(ToType(a) <= ToType(b)),
                        _ => ""
                    }
                ).ToString();

            return new AssemblyExpr.UnresolvedLiteral((AssemblyExpr.Literal.LiteralType)OperationType(op, (Parser.LiteralTokenType)a.type, (Parser.LiteralTokenType)b.type), result);
        }

        private static string Add(dynamic a, dynamic b, bool stringAddition, CodeGen assembler)
        {
            if (!stringAddition)
            {
                return (a + b).ToString();
            }

            IEnumerable<byte[]> aData = null;
            IEnumerable<byte[]> bData = null;

            int i = 1;
            while (aData == null || bData == null)
            {
                if (((AssemblyExpr.Data)assembler.assembly.data[i]).name == (string)a)
                {
                    aData = ((AssemblyExpr.Data)assembler.assembly.data[i]).literal.value;
                }
                if (((AssemblyExpr.Data)assembler.assembly.data[i]).name == (string)b)
                {
                    bData = ((AssemblyExpr.Data)assembler.assembly.data[i]).literal.value;
                }
                i++;
            }
            assembler.EmitData(new AssemblyExpr.Data(assembler.DataLabel, AssemblyExpr.Literal.LiteralType.String, aData.Concat(bData)));

            return assembler.CreateDatalLabel(assembler.dataCount++);
        }

        public static Diagnostic.AnalyzerDiagnostic InvalidOperation(Token op, Parser.LiteralTokenType type1, Parser.LiteralTokenType type2)
        {
            return InvalidOperation(op, TypeCheckUtils.literalTypes[type1].ToString(), TypeCheckUtils.literalTypes[type2].ToString());
        }
        public static Diagnostic.AnalyzerDiagnostic InvalidOperation(Token op, string type1, string type2)
        {
            return new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InvalidOperatorCall_Arity2, type1, type2, $"{SymbolToPrimitiveName(op)}({type1}, {type2})");
        }

        // Unary Operation
        public static Parser.LiteralTokenType OperationType(Token op, Parser.LiteralTokenType type1)
        {
            if (type1 == Parser.VoidTokenType)
            {
                Diagnostics.Report(InvalidOperation(op, type1));
                return Parser.VoidTokenType;
            }

            var pName = SymbolToPrimitiveName(op);

            // Note: All cases where an operator's return type for given literal is not correctly expressed in the switch should be handled individually, here

            if (pName == "Increment" || pName == "Decrement")
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InvalidFunctionModifier_Ref));
                return Parser.VoidTokenType;
            }

            switch (type1)
            {
                case Integer: return Integer; // INTEGER OP

                case Floating: Diagnostics.Report(InvalidOperation(op, Floating)); break;  // FLOATING OP

                case Parser.LiteralTokenType.String: Diagnostics.Report(InvalidOperation(op, Parser.LiteralTokenType.String)); break;  // STRING OP

                case RefString: Diagnostics.Report(InvalidOperation(op, RefString)); break;  // REF_STRING OP

                case Binary: return Integer; // BINARY OP

                case Hex: return Integer; // HEX OP

                case Parser.LiteralTokenType.Boolean: return Parser.LiteralTokenType.Boolean; // BOOLEAN OP

                default:
                    throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic($"Unrecognized literal operation"));
            }
            return Parser.VoidTokenType;
        }

        public static AssemblyExpr.UnresolvedLiteral Operation(Token op, AssemblyExpr.UnresolvedLiteral a, CodeGen assembler)
        {
            string pName = SymbolToPrimitiveName(op);

            string result =
                (
                    pName switch
                    {
                        "Increment" => ToType(a) + 1,
                        "Decrement" => ToType(a) - 1,
                        "Not" => ~ToType(a),
                        "Subtract" => -ToType(a)
                    }
                ).ToString();

            return new AssemblyExpr.UnresolvedLiteral((AssemblyExpr.Literal.LiteralType)OperationType(op, (Parser.LiteralTokenType)a.type), result);
        }

        public static Diagnostic.AnalyzerDiagnostic InvalidOperation(Token op, Parser.LiteralTokenType type)
        {
            return InvalidOperation(op, TypeCheckUtils.literalTypes[type].ToString());
        }
        public static Diagnostic.AnalyzerDiagnostic InvalidOperation(Token op, string type)
        {
            return new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InvalidOperatorCall_Arity1, type, SymbolToPrimitiveName(op));
        }

        public static bool IsVoidType(Expr.Type type)
        {
            return type == TypeCheckUtils._voidType;
        }

        public static (bool, Parser.LiteralTokenType) IsLiteralTypeOrVoid(Expr.Type type)
        {
            if (type.name.type == Token.TokenType.IDENTIFIER)
            {
                return (false, Parser.VoidTokenType);
            }
            else if (IsVoidType(type))
            {
                return (true, Parser.VoidTokenType);
            }
            return (true, (Parser.LiteralTokenType)type.name.type);
        }

        public static string SymbolToPrimitiveName(Token op) => op.type switch
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
            Token.TokenType.NOT => "Not",
            Token.TokenType.EQUALTO => "EqualTo",
            Token.TokenType.NOTEQUALTO => "NotEqualTo",
            Token.TokenType.GREATER => "GreaterThan",
            Token.TokenType.LESS => "LessThan",
            Token.TokenType.GREATEREQUAL => "GreaterThanOrEqualTo",
            Token.TokenType.LESSEQUAL => "LessThanOrEqualTo",
            Token.TokenType.LBRACKET => "Indexer"
        };
    }
}
