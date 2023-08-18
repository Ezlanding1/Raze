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
            Token.TokenType.BOOLEAN => int.Parse(literal.value)
        };

        public static Token.TokenType OperationType(Token op, sbyte type1, sbyte type2)
        {
            if (type1 == -1)
            {
                throw InvalidOperation(op, "void", type2 == -1 ? "void" : (TypeCheckPass.literalTypes[(Token.TokenType)type2].ToString()));
            }
            else if (type2 == -1)
            {
                throw InvalidOperation(op, TypeCheckPass.literalTypes[(Token.TokenType)type1].ToString(), "void");
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
                throw InvalidOperation(op, t1, t2);
            }
            if ((t1 == BOOLEAN || t2 == BOOLEAN) && (pName != "BitwiseAnd" || pName != "BitwiseOr" || pName != "BitwiseXor"))
            {
                throw InvalidOperation(op, t1, t2);
            }


            switch (t1, t2)
            {
                case (INTEGER, INTEGER): return Token.TokenType.INTEGER; // INTEGER OP INTEGER
                case (INTEGER, FLOATING): case (FLOATING, INTEGER): return Token.TokenType.FLOATING; // INTEGER OP FLOATING
                case (INTEGER, STRING): case (STRING, INTEGER): throw InvalidOperation(op, t1, t2);  // INTEGER OP STRING
                case (INTEGER, BINARY): case (BINARY, INTEGER): return Token.TokenType.BINARY; // INTEGER OP BINARY
                case (INTEGER, HEX): case (HEX, INTEGER): return Token.TokenType.HEX; // INTEGER OP HEX
                case (INTEGER, BOOLEAN): case (BOOLEAN, INTEGER): throw InvalidOperation(op, t1, t2);  // INTEGER OP BOOLEAN

                case (FLOATING, FLOATING): return Token.TokenType.FLOATING; // FLOATING OP FLOATING
                case (FLOATING, STRING): case (STRING, FLOATING): throw InvalidOperation(op, t1, t2); // FLOATING OP STRING
                case (FLOATING, BINARY): case (BINARY, FLOATING): throw InvalidOperation(op, t1, t2);  // FLOATING OP BINARY
                case (FLOATING, HEX): case (HEX, FLOATING): throw InvalidOperation(op, t1, t2);  // FLOATING OP HEX
                case (FLOATING, BOOLEAN): case (BOOLEAN, FLOATING): throw InvalidOperation(op, t1, t2);  // FLOATING OP BOOLEAN

                case (STRING, STRING): throw InvalidOperation(op, t1, t2);  // STRING OP STRING
                case (STRING, BINARY): case (BINARY, STRING): throw InvalidOperation(op, t1, t2);  // STRING OP BINARY
                case (STRING, HEX): case (HEX, STRING): throw InvalidOperation(op, t1, t2);  // STRING OP HEX
                case (STRING, BOOLEAN): case (BOOLEAN, STRING): throw InvalidOperation(op, t1, t2);  // STRING OP BOOLEAN

                case (BINARY, BINARY): return Token.TokenType.BINARY; // BINARY OP BINARY
                case (BINARY, HEX): case (HEX, BINARY): throw InvalidOperation(op, t1, t2);  // BINARY OP HEX
                case (BINARY, BOOLEAN): case (BOOLEAN, BINARY): throw InvalidOperation(op, t1, t2);  // BINARY OP BOOLEAN

                case (HEX, HEX): return Token.TokenType.HEX; // HEX OP HEX
                case (HEX, BOOLEAN): case (BOOLEAN, HEX): throw InvalidOperation(op, t1, t2);  // HEX OP BOOLEAN

                case (BOOLEAN, BOOLEAN): return Token.TokenType.BOOLEAN; // BOOLEAN OP BOOLEAN

                default:
                    throw new Errors.ImpossibleError($"Unrecognized literal operation");
            }
        }

        public static Token.TokenType OperationType(Token op, sbyte type1)
        {
            if (type1 == -1)
            {
                throw InvalidOperation(op, "void");
            }

            var pName = SymbolToPrimitiveName(op);

            // Note: All cases where an operator's return type for given literal is not correctly expressed in the switch should be handled individually, here

            if (pName == "Increment" || pName == "Decrement")
            {
                throw new Errors.AnalyzerError("Invalid Operator Argument", "Cannot assign when non-variable is passed to 'ref' parameter");
            }

            switch ((Token.TokenType)type1)
            {
                case INTEGER: return Token.TokenType.INTEGER; // INTEGER OP

                case FLOATING: throw InvalidOperation(op, Token.TokenType.FLOATING);  // FLOATING OP

                case STRING: throw InvalidOperation(op, Token.TokenType.STRING);  // STRING OP

                case BINARY: return Token.TokenType.BINARY; // BINARY OP

                case HEX: return Token.TokenType.HEX; // HEX OP

                case BOOLEAN: return Token.TokenType.BOOLEAN; // BOOLEAN OP

                default:
                    throw new Errors.ImpossibleError($"Unrecognized literal operation");
            }
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
                        _ => throw InvalidOperation(op, a.type, b.type)
                    }
                ).ToString();

            return new Instruction.Literal(OperationType(op, (sbyte)a.type, (sbyte)b.type), result);
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

        public static Errors.AnalyzerError InvalidOperation(Token op, Token.TokenType type1, Token.TokenType type2)
        {
            return InvalidOperation(op, TypeCheckPass.literalTypes[type1].ToString(), TypeCheckPass.literalTypes[type2].ToString());
        }
        public static Errors.AnalyzerError InvalidOperation(Token op, string type1, string type2)
        {
            return new Errors.AnalyzerError("Invalid Operator", $"Types '{type1}' and '{type2}' don't have a definition for '{SymbolToPrimitiveName(op)}' ( '{op.lexeme}' )");
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
                        "Not" => ~ToType(a, a.type)
                    }
                ).ToString();

            return new Instruction.Literal(OperationType(op, (sbyte)a.type), result);
        }

        public static Errors.AnalyzerError InvalidOperation(Token op, Token.TokenType type)
        {
            return InvalidOperation(op, TypeCheckPass.literalTypes[type].ToString());
        }
        public static Errors.AnalyzerError InvalidOperation(Token op, string type)
        {
            return new Errors.AnalyzerError("Invalid Operator", $"Type '{type}' doesn't not have a definition for '{SymbolToPrimitiveName(op)}' ( '{op.lexeme}' )");
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
            Token.TokenType.NOT => "Not"
        };
    }
}
