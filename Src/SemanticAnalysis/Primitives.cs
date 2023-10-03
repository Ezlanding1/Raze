using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

internal partial class Analyzer
{
    internal class Primitives
    {
        const Token.TokenType INTEGER = Token.TokenType.INTEGER;
        const Token.TokenType FLOATING = Token.TokenType.FLOATING;
        const Token.TokenType STRING = Token.TokenType.STRING;
        const Token.TokenType BINARY = Token.TokenType.BINARY;
        const Token.TokenType HEX = Token.TokenType.HEX;
        const Token.TokenType BOOLEAN = Token.TokenType.BOOLEAN;

        // Binary Operation

        private static bool IsIntegralType(Token.TokenType literalType) => literalType switch
        {
            Token.TokenType.INTEGER => true,
            Token.TokenType.FLOATING => false,
            Token.TokenType.STRING => false,
            Token.TokenType.BINARY => true,
            Token.TokenType.HEX => true,
            Token.TokenType.BOOLEAN => false,
        };

        private static dynamic ToType(Instruction.Literal literal, Token.TokenType type) => type switch
        {
            Token.TokenType.INTEGER => int.Parse(literal.value),
            Token.TokenType.FLOATING => float.Parse(literal.value),
            Token.TokenType.STRING => literal.value,
            Token.TokenType.BINARY => Convert.ToInt32(literal.value, 2),
            Token.TokenType.HEX => Convert.ToInt32(literal.value, 16),
            Token.TokenType.BOOLEAN => byte.Parse(literal.value)
        };

        public static Token.TokenType? OperationType(Token op, sbyte type1, sbyte type2)
        {
            if (type1 == -1)
            {
                Diagnostics.errors.Push(InvalidOperation(op, "void", type2 == -1 ? "void" : (TypeCheckUtils.literalTypes[(Token.TokenType)type2].ToString())));
                return null;
            }
            else if (type2 == -1)
            {
                Diagnostics.errors.Push(InvalidOperation(op, TypeCheckUtils.literalTypes[(Token.TokenType)type1].ToString(), "void"));
                return null;
            }

            Token.TokenType t1 = (Token.TokenType)type1;
            Token.TokenType t2 = (Token.TokenType)type2;

            string pName = SymbolToPrimitiveName(op);

            // Note: All cases where an operator's return type for two given literals is not correctly expressed in the switch should be handled individually, here

            if (t1 == STRING && t2 == STRING && pName == "Add")
            {
                return Token.TokenType.STRING;
            }
            if ((t1 == FLOATING || t2 == FLOATING) && (pName == "BitwiseShiftLeft" || pName == "BitwiseShiftRight" || pName == "BitwiseAnd" || pName == "BitwiseOr" || pName == "BitwiseXor"))
            {
                Diagnostics.errors.Push(InvalidOperation(op, t1, t2));
                return null;
            }
            if (pName == "EqualTo" || pName == "NotEqualTo" || pName == "GreaterThan" || pName == "LessThan" || pName == "GreaterThanOrEqualTo" || pName == "LessThanOrEqualTo")
            {
                return Token.TokenType.BOOLEAN;
            }
            if (t1 == BOOLEAN || t2 == BOOLEAN)
            {
                if (pName == "BitwiseAnd" || pName == "BitwiseOr" || pName == "BitwiseXor")
                {
                    return Token.TokenType.BOOLEAN;
                }
                else
                {
                    Diagnostics.errors.Push(InvalidOperation(op, t1, t2));
                }
            }


            switch (t1, t2)
            {
                case (INTEGER, INTEGER): return Token.TokenType.INTEGER; // INTEGER OP INTEGER
                case (INTEGER, FLOATING): case (FLOATING, INTEGER): return Token.TokenType.FLOATING; // INTEGER OP FLOATING
                case (INTEGER, STRING): case (STRING, INTEGER): Diagnostics.errors.Push(InvalidOperation(op, t1, t2)); break;  // INTEGER OP STRING
                case (INTEGER, BINARY): case (BINARY, INTEGER): return Token.TokenType.BINARY; // INTEGER OP BINARY
                case (INTEGER, HEX): case (HEX, INTEGER): return Token.TokenType.HEX; // INTEGER OP HEX
                case (INTEGER, BOOLEAN): case (BOOLEAN, INTEGER): Diagnostics.errors.Push(InvalidOperation(op, t1, t2)); break;  // INTEGER OP BOOLEAN

                case (FLOATING, FLOATING): return Token.TokenType.FLOATING; // FLOATING OP FLOATING
                case (FLOATING, STRING): case (STRING, FLOATING): Diagnostics.errors.Push(InvalidOperation(op, t1, t2)); break; // FLOATING OP STRING
                case (FLOATING, BINARY): case (BINARY, FLOATING): Diagnostics.errors.Push(InvalidOperation(op, t1, t2)); break;  // FLOATING OP BINARY
                case (FLOATING, HEX): case (HEX, FLOATING): Diagnostics.errors.Push(InvalidOperation(op, t1, t2)); break;  // FLOATING OP HEX
                case (FLOATING, BOOLEAN): case (BOOLEAN, FLOATING): Diagnostics.errors.Push(InvalidOperation(op, t1, t2)); break;  // FLOATING OP BOOLEAN

                case (STRING, STRING): Diagnostics.errors.Push(InvalidOperation(op, t1, t2)); break; // STRING OP STRING
                case (STRING, BINARY): case (BINARY, STRING): Diagnostics.errors.Push(InvalidOperation(op, t1, t2)); break;  // STRING OP BINARY
                case (STRING, HEX): case (HEX, STRING): Diagnostics.errors.Push(InvalidOperation(op, t1, t2)); break;  // STRING OP HEX
                case (STRING, BOOLEAN): case (BOOLEAN, STRING): Diagnostics.errors.Push(InvalidOperation(op, t1, t2)); break;  // STRING OP BOOLEAN

                case (BINARY, BINARY): return Token.TokenType.BINARY; // BINARY OP BINARY
                case (BINARY, HEX): case (HEX, BINARY): Diagnostics.errors.Push(InvalidOperation(op, t1, t2)); break;  // BINARY OP HEX
                case (BINARY, BOOLEAN): case (BOOLEAN, BINARY): Diagnostics.errors.Push(InvalidOperation(op, t1, t2)); break;  // BINARY OP BOOLEAN

                case (HEX, HEX): return Token.TokenType.HEX; // HEX OP HEX
                case (HEX, BOOLEAN): case (BOOLEAN, HEX): Diagnostics.errors.Push(InvalidOperation(op, t1, t2)); break;  // HEX OP BOOLEAN

                case (BOOLEAN, BOOLEAN): return Token.TokenType.BOOLEAN; // BOOLEAN OP BOOLEAN

                default:
                    Diagnostics.errors.Push(new Error.ImpossibleError($"Unrecognized literal operation"));
                    return 0;
            }
            return null;
        }

        public static Token.TokenType? OperationType(Token op, sbyte type1)
        {
            if (type1 == -1)
            {
                Diagnostics.errors.Push(InvalidOperation(op, "void"));
                return null;
            }

            var pName = SymbolToPrimitiveName(op);

            // Note: All cases where an operator's return type for given literal is not correctly expressed in the switch should be handled individually, here

            if (pName == "Increment" || pName == "Decrement")
            {
                Diagnostics.errors.Push(new Error.AnalyzerError("Invalid Operator Argument", "Cannot assign when non-variable is passed to 'ref' parameter"));
                return null;
            }

            switch ((Token.TokenType)type1)
            {
                case INTEGER: return Token.TokenType.INTEGER; // INTEGER OP

                case FLOATING: Diagnostics.errors.Push(InvalidOperation(op, Token.TokenType.FLOATING)); break;  // FLOATING OP

                case STRING: Diagnostics.errors.Push(InvalidOperation(op, Token.TokenType.STRING)); break;  // STRING OP

                case BINARY: return Token.TokenType.BINARY; // BINARY OP

                case HEX: return Token.TokenType.HEX; // HEX OP

                case BOOLEAN: return Token.TokenType.BOOLEAN; // BOOLEAN OP

                default:
                    Diagnostics.errors.Push(new Error.ImpossibleError($"Unrecognized literal operation"));
                    return 0;
            }
            return null;
        }

        public static Instruction.Literal Operation(Token op, Instruction.Literal a, Instruction.Literal b, Assembler assembler)
        {
            string pName = SymbolToPrimitiveName(op);

            string result =
                (
                    pName switch
                    {
                        "Add" => Add(ToType(a, a.type), ToType(b, b.type), (a.type == b.type && a.type == Token.TokenType.STRING), assembler),
                        "Subtract" => ToType(a, a.type) - ToType(b, b.type),
                        "Multiply" => ToType(a, a.type) * ToType(b, b.type),
                        "Modulo" => ToType(a, a.type) % ToType(b, b.type),
                        "Divide" => ToType(a, a.type) / ToType(b, b.type),
                        "BitwiseAnd" => ToType(a, a.type) & ToType(b, b.type),
                        "BitwiseOr" => ToType(a, a.type) | ToType(b, b.type),
                        "BitwiseXor" => ToType(a, a.type) ^ ToType(b, b.type),
                        "BitwiseShiftLeft" => ToType(a, a.type) << ToType(b, b.type),
                        "BitwiseShiftRight" => ToType(a, a.type) >> ToType(b, b.type),
                        "EqualTo" => Convert.ToByte(ToType(a, a.type) == ToType(b, b.type)),
                        "NotEqualTo" => Convert.ToByte(ToType(a, a.type) != ToType(b, b.type)),
                        "GreaterThan" => Convert.ToByte(ToType(a, a.type) > ToType(b, b.type)),
                        "LessThan" => Convert.ToByte(ToType(a, a.type) < ToType(b, b.type)),
                        "GreaterThanOrEqualTo" => Convert.ToByte(ToType(a, a.type) >= ToType(b, b.type)),
                        "LessThanOrEqualTo" => Convert.ToByte(ToType(a, a.type) <= ToType(b, b.type)),
                        _ => ""
                    }
                ).ToString();

            return new Instruction.Literal((Token.TokenType)OperationType(op, (sbyte)a.type, (sbyte)b.type), result);
        }
        private static string Add(dynamic a, dynamic b, bool stringAddition, Assembler assembler)
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
                if (((Instruction.Data)assembler.data[i]).name == (string)a)
                {
                    aData = ((Instruction.Data)assembler.data[i]).value;
                }
                if (((Instruction.Data)assembler.data[i]).name == (string)b)
                {
                    bData = ((Instruction.Data)assembler.data[i]).value;
                }
                i++;
            }

            assembler.EmitData(new Instruction.Data(assembler.DataLabel, InstructionUtils.dataSize[1], aData[..^4] + bData[1..]));

            return assembler.CreateDatalLabel(assembler.dataCount++);
        }

        public static Error.AnalyzerError InvalidOperation(Token op, Token.TokenType type1, Token.TokenType type2)
        {
            return InvalidOperation(op, TypeCheckUtils.literalTypes[type1].ToString(), TypeCheckUtils.literalTypes[type2].ToString());
        }
        public static Error.AnalyzerError InvalidOperation(Token op, string type1, string type2)
        {
            return new Error.AnalyzerError("Invalid Operator", $"Types '{type1}' and '{type2}' don't have a definition for '{SymbolToPrimitiveName(op)}({type1}, {type2})'");
        }

        // Unary Operation
        public static Instruction.Value Operation(Token op, Instruction.Literal a, Assembler assembler)
        {
            string pName = SymbolToPrimitiveName(op);

            string result =
                (
                    pName switch
                    {
                        "Increment" => ToType(a, a.type) + 1,
                        "Decrement" => ToType(a, a.type) - 1,
                        "Not" => ~ToType(a, a.type),
                        "Subtract" => -ToType(a, a.type)
                    }
                ).ToString();

            return new Instruction.Literal((Token.TokenType)OperationType(op, (sbyte)a.type), result);
        }

        public static Error.AnalyzerError InvalidOperation(Token op, Token.TokenType type)
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

        public static (bool, sbyte) IsLiteralTypeOrVoid(Expr.Type type)
        {
            for (sbyte i = 0; i < Parser.Literals.Length; i++)
            {
                if (IsLiteralType(type, i))
                {
                    return (true, (sbyte)Parser.Literals[i]);
                }
            }
            if (IsVoidType(type))
            {
                return (true, -1);
            }
            return (false, -1);
        }
        public static bool IsLiteralType(Expr.Type type, sbyte literal)
        {
            return type.name.type == Parser.Literals[literal];
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
        };
    }
}
