using System;
using System.Collections.Generic;
using System.Linq;
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
        public static PrimitiveType ToPrimitive(Token type, Token name, Expr.Literal value)
        {
            var primitiveType = PrimitiveTypes(type, name, value);
            return primitiveType;
        }

        internal abstract class PrimitiveType
        {
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
            public _number(int size, Token type, Token name, Expr.Literal value)
                : base(size, type, name, value)
            {

            }
        }

        class _string : PrimitiveType
        {
            public _string(int size, Token type, Token name, Expr.Literal value)
                : base(size, type, name, value)
            {

            }

            public override string Location(int size)
            {
                return name.lexeme;
            }
        }
    }
}
