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
        private static bool IsIntegralType(Parser.LiteralTokenType literalType) => literalType switch
        {
            INTEGER => true,
            FLOATING => false,
            STRING => false,
            REF_STRING => false,
            BINARY => true,
            HEX => true,
            BOOLEAN => false,
        };

        private static dynamic ToType(AssemblyExpr.Literal literal) => literal.type switch
        {
            INTEGER => int.Parse(literal.value),
            FLOATING => float.Parse(literal.value),
            STRING => literal.value,
            REF_STRING => literal.value,
            BINARY => Convert.ToInt32(literal.value, 2),
            HEX => Convert.ToInt32(literal.value, 16),
            BOOLEAN => byte.Parse(literal.value)
        };

        // Binary Operation
        public static Parser.LiteralTokenType OperationType(Token op, Parser.LiteralTokenType type1, Parser.LiteralTokenType type2)
        {
            if (type1 == Parser.VoidTokenType || type2 == Parser.VoidTokenType)
            {
                Diagnostics.errors.Push(InvalidOperation(op, type1, type2));
                return Parser.VoidTokenType;
            }

            string pName = SymbolToPrimitiveName(op);

            // Note: All cases where an operator's return type for two given literals is not correctly expressed in the switch should be handled individually, here

            if (type1 == STRING && type2 == STRING && pName == "Add")
            {
                return STRING;
            }
            if ((type1 == FLOATING || type2 == FLOATING) && (pName == "BitwiseShiftLeft" || pName == "BitwiseShiftRight" || pName == "BitwiseAnd" || pName == "BitwiseOr" || pName == "BitwiseXor"))
            {
                Diagnostics.errors.Push(InvalidOperation(op, type1, type2));
                return Parser.VoidTokenType;
            }
            if (pName == "EqualTo" || pName == "NotEqualTo" || pName == "GreaterThan" || pName == "LessThan" || pName == "GreaterThanOrEqualTo" || pName == "LessThanOrEqualTo")
            {
                return BOOLEAN;
            }
            if (type1 == BOOLEAN || type2 == BOOLEAN)
            {
                if (pName == "BitwiseAnd" || pName == "BitwiseOr" || pName == "BitwiseXor")
                {
                    return BOOLEAN;
                }
                else
                {
                    Diagnostics.errors.Push(InvalidOperation(op, type1, type2));
                }
            }

            switch (type1, type2)
            {
                case (INTEGER, INTEGER): return INTEGER; // INTEGER OP INTEGER
                case (INTEGER, FLOATING): case (FLOATING, INTEGER): return FLOATING; // INTEGER OP FLOATING
                case (INTEGER, STRING):
                case (STRING, INTEGER):
                case (INTEGER, REF_STRING):
                case (REF_STRING, INTEGER): Diagnostics.errors.Push(InvalidOperation(op, type1, type2)); break;  // INTEGER OP STRING
                case (INTEGER, BINARY): case (BINARY, INTEGER): return BINARY; // INTEGER OP BINARY
                case (INTEGER, HEX): case (HEX, INTEGER): return HEX; // INTEGER OP HEX
                case (INTEGER, BOOLEAN): case (BOOLEAN, INTEGER): Diagnostics.errors.Push(InvalidOperation(op, type1, type2)); break;  // INTEGER OP BOOLEAN

                case (FLOATING, FLOATING): return FLOATING; // FLOATING OP FLOATING
                case (FLOATING, STRING):
                case (STRING, FLOATING):
                case (FLOATING, REF_STRING):
                case (REF_STRING, FLOATING): Diagnostics.errors.Push(InvalidOperation(op, type1, type2)); break; // FLOATING OP STRING
                case (FLOATING, BINARY): case (BINARY, FLOATING): Diagnostics.errors.Push(InvalidOperation(op, type1, type2)); break;  // FLOATING OP BINARY
                case (FLOATING, HEX): case (HEX, FLOATING): Diagnostics.errors.Push(InvalidOperation(op, type1, type2)); break;  // FLOATING OP HEX
                case (FLOATING, BOOLEAN): case (BOOLEAN, FLOATING): Diagnostics.errors.Push(InvalidOperation(op, type1, type2)); break;  // FLOATING OP BOOLEAN

                case (STRING, STRING):
                case (REF_STRING, REF_STRING): Diagnostics.errors.Push(InvalidOperation(op, type1, type2)); break; // STRING OP STRING
                case (STRING, BINARY):
                case (BINARY, STRING):
                case (REF_STRING, BINARY):
                case (BINARY, REF_STRING): Diagnostics.errors.Push(InvalidOperation(op, type1, type2)); break;  // STRING OP BINARY
                case (STRING, HEX):
                case (HEX, STRING):
                case (REF_STRING, HEX):
                case (HEX, REF_STRING): Diagnostics.errors.Push(InvalidOperation(op, type1, type2)); break;  // STRING OP HEX
                case (STRING, BOOLEAN):
                case (BOOLEAN, STRING):
                case (REF_STRING, BOOLEAN):
                case (BOOLEAN, REF_STRING): Diagnostics.errors.Push(InvalidOperation(op, type1, type2)); break;  // STRING OP BOOLEAN

                case (BINARY, BINARY): return BINARY; // BINARY OP BINARY
                case (BINARY, HEX): case (HEX, BINARY): Diagnostics.errors.Push(InvalidOperation(op, type1, type2)); break;  // BINARY OP HEX
                case (BINARY, BOOLEAN): case (BOOLEAN, BINARY): Diagnostics.errors.Push(InvalidOperation(op, type1, type2)); break;  // BINARY OP BOOLEAN

                case (HEX, HEX): return HEX; // HEX OP HEX
                case (HEX, BOOLEAN): case (BOOLEAN, HEX): Diagnostics.errors.Push(InvalidOperation(op, type1, type2)); break;  // HEX OP BOOLEAN

                case (BOOLEAN, BOOLEAN): return BOOLEAN; // BOOLEAN OP BOOLEAN

                default:
                    Diagnostics.errors.Push(new Error.ImpossibleError($"Unrecognized literal operation"));
                    return 0;
            }
            return Parser.VoidTokenType;
        }

        public static AssemblyExpr.Literal Operation(Token op, AssemblyExpr.Literal a, AssemblyExpr.Literal b, CodeGen assembler)
        {
            string pName = SymbolToPrimitiveName(op);

            string result =
                (
                    pName switch
                    {
                        "Add" => Add(ToType(a), ToType(b), (a.type == b.type && a.type == REF_STRING), assembler),
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

            return new AssemblyExpr.Literal(OperationType(op, a.type, b.type), result);
        }

        private static string Add(dynamic a, dynamic b, bool stringAddition, CodeGen assembler)
        {
            if (!stringAddition)
            {
                return (a + b).ToString();
            }

            string aData = null;
            string bData = null;

            int i = 1;
            while (aData == null || bData == null)
            {
                if (((AssemblyExpr.Data)assembler.assembly.data[i]).name == (string)a)
                {
                    aData = ((AssemblyExpr.Data)assembler.assembly.data[i]).value.Item2;
                }
                if (((AssemblyExpr.Data)assembler.assembly.data[i]).name == (string)b)
                {
                    bData = ((AssemblyExpr.Data)assembler.assembly.data[i]).value.Item2;
                }
                i++;
            }

            assembler.EmitData(new AssemblyExpr.Data(assembler.DataLabel, AssemblyExpr.Register.RegisterSize._8Bits, (Parser.LiteralTokenType.REF_STRING, aData[..^4] + bData[1..])));

            return assembler.CreateDatalLabel(assembler.dataCount++);
        }

        public static Error.AnalyzerError InvalidOperation(Token op, Parser.LiteralTokenType type1, Parser.LiteralTokenType type2)
        {
            return InvalidOperation(op, TypeCheckUtils.literalTypes[type1].ToString(), TypeCheckUtils.literalTypes[type2].ToString());
        }
        public static Error.AnalyzerError InvalidOperation(Token op, string type1, string type2)
        {
            return new Error.AnalyzerError("Invalid Operator", $"Types '{type1}' and '{type2}' don't have a definition for '{SymbolToPrimitiveName(op)}({type1}, {type2})'");
        }

        // Unary Operation
        public static Parser.LiteralTokenType OperationType(Token op, Parser.LiteralTokenType type1)
        {
            if (type1 == Parser.VoidTokenType)
            {
                Diagnostics.errors.Push(InvalidOperation(op, type1));
                return Parser.VoidTokenType;
            }

            var pName = SymbolToPrimitiveName(op);

            // Note: All cases where an operator's return type for given literal is not correctly expressed in the switch should be handled individually, here

            if (pName == "Increment" || pName == "Decrement")
            {
                Diagnostics.errors.Push(new Error.AnalyzerError("Invalid Operator Argument", "Cannot assign when non-variable is passed to 'ref' parameter"));
                return Parser.VoidTokenType;
            }

            switch (type1)
            {
                case INTEGER: return INTEGER; // INTEGER OP

                case FLOATING: Diagnostics.errors.Push(InvalidOperation(op, FLOATING)); break;  // FLOATING OP

                case STRING: Diagnostics.errors.Push(InvalidOperation(op, STRING)); break;  // STRING OP

                case REF_STRING: Diagnostics.errors.Push(InvalidOperation(op, REF_STRING)); break;  // REF_STRING OP

                case BINARY: return BINARY; // BINARY OP

                case HEX: return HEX; // HEX OP

                case BOOLEAN: return BOOLEAN; // BOOLEAN OP

                default:
                    Diagnostics.errors.Push(new Error.ImpossibleError($"Unrecognized literal operation"));
                    return 0;
            }
            return Parser.VoidTokenType;
        }

        public static AssemblyExpr.Value Operation(Token op, AssemblyExpr.Literal a, CodeGen assembler)
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

            return new AssemblyExpr.Literal(OperationType(op, a.type), result);
        }

        public static Error.AnalyzerError InvalidOperation(Token op, Parser.LiteralTokenType type)
        {
            return InvalidOperation(op, TypeCheckUtils.literalTypes[type].ToString());
        }
        public static Error.AnalyzerError InvalidOperation(Token op, string type)
        {
            return new Error.AnalyzerError("Invalid Operator", $"Type '{type}' doesn't not have a definition for '{SymbolToPrimitiveName(op)}' ( '{op.lexeme}' )");
        }

        public static bool IsVoidType(Expr.Type type)
        {
            return type.name.lexeme == "void";
        }

        public static (bool, Parser.LiteralTokenType) IsLiteralTypeOrVoid(Expr.Type type)
        {
            if (Enum.TryParse<Parser.LiteralTokenType>(type.name.type.ToString(), out var literalTokenType))
            {
                return (true, literalTokenType);
            }
            if (IsVoidType(type))
            {
                return (true, Parser.VoidTokenType);
            }
            return (false, Parser.VoidTokenType);
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
