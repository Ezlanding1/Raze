﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Espionage
{
    internal class Primitives
    {
        private const int BYTES_IN_NUMBER = 8;
        private const int BYTES_IN_STRING = 0;

        public static Dictionary<string, int> PrimitiveSize = new()
        {
            { "number", BYTES_IN_NUMBER },
            { "string", BYTES_IN_STRING }
        };

        private static PrimitiveType PrimitiveTypes(Token type, Token name, Expr.Literal value)
        {
            switch (type.lexeme)
            {
                case "number":
                    return new _number(PrimitiveSize["number"], type, name, value);
                case "string":
                    return new _string(PrimitiveSize["string"], type, name, value);
                default:
                    throw new Exception($"Espionage Error: '{type}' is not a primitive type (class)");
            }
        }

        public static Operators PrimitiveOps(string type)
        {
            switch (type)
            {
                case "number":
                    return _number.Operators;
                case "string":
                    return _string.Operators;
                default:
                    throw new Exception($"Espionage Error: '{type}' is not a primitive type (class)");
            }
        }

        public static PrimitiveType ToPrimitive(Token type, Token name, Expr.Literal value)
        {
            var primitiveType = PrimitiveTypes(type, name, value);
            return primitiveType;
        }

        internal abstract class PrimitiveType
        {
            public static Operators Operators {
                get { return new _number(0,null,null,null).GetOps(); }
            }
            internal abstract Operators GetOps();
            public Token type;
            public Token name;
            public Expr.Literal value;
            public int size;

            public PrimitiveType(int size, Token type, Token name, Expr.Literal value)
            {
                this.size = size;
                this.type = type;
                this.name = name;
                this.value = value;
            }

            public virtual string Location(int size)
            {
                return size.ToString();
            }
        }

        class _number : PrimitiveType
        {
            public static readonly Operators Ops = new(
                new(){ // Return Type
                    "number",
                    "number",
                    "number",
                    "number",
                    "number"
                }, 
                new(){ // Operand 1
                    "number",
                    "number",
                    "number",
                    "number",
                    "number"
                }, 
                new(){ // Operator + " BIN/UN" (unary or binary);
                    "+ BIN",
                    "- BIN",
                    "* BIN",
                    "/ BIN",
                    "% BIN"
                },
                new(){ // Operand 2
                    "number",
                    "number",
                    "number",
                    "number",
                    "number"
                }
            );
            internal override Operators GetOps()
            {
                return Ops;
            }
            public _number(int size, Token type, Token name, Expr.Literal value)
                : base(size, type, name, value)
            {

            }
        }

        class _string : PrimitiveType
        {
            public static readonly Operators Ops = new(
                new(){ // Return Type
                },
                new(){ // Operand 1
                },
                new(){ // Operator
                },
                new(){ // Operand 2
                }
            );
            internal override Operators GetOps()
            {
                return Ops;
            }
            public _string(int size, Token type, Token name, Expr.Literal value)
                : base(size, type, name, value)
            {

            }

            public override string Location(int size)
            {
                return name.lexeme;
            }
        }

        public class Operators
        {
            // (Operator, Operand 1, Operand 2), Return Type
            public Dictionary<(string, string, string), string> _operators;
            public Operators(Dictionary<(string, string, string), string> operators)
            {
                _operators = operators;
            }
            public Operators(List<string> ret, List<string> first, List<string> ops, List<string> second)
            {
                _operators = new();
                for (int i = 0; i < ops.Count; i++)
                {
                    _operators.Add((ops[i], first[i], second[i]), ret[i]);
                }
            }
        }
    }
}
