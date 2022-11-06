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

        public static Dictionary<string, PrimitiveType> PrimitiveTypes = new()
        {
            { "number", new _number(PrimitiveSize["number"]) },
            { "string", new _string(PrimitiveSize["string"]) }
        };
        public static PrimitiveType ToPrimitive(Token type, Token name, Expr.Literal value)
        {
            if (PrimitiveTypes.ContainsKey(type.lexeme))
            {
                var primitiveType = PrimitiveTypes[type.lexeme];
                primitiveType.Add(type, name, value);
                return primitiveType;
            }
            else
            {
                throw new Exception($"Espionage Error: Internal Type Not Implemented (class) for type of '{type}'");
            }
        }

        internal abstract class PrimitiveType
        {
            public Token type;
            public Token name;
            public Expr.Literal value;
            public int size;

            public PrimitiveType(int size)
            {
                this.size = size;
            }

            public void Add(Token type, Token name, Expr.Literal value)
            {
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
            public _number(int size)
                : base(size)
            {

            }
        }

        class _string : PrimitiveType
        {
            public _string(int size)
                : base(size)
            {

            }

            public override string Location(int size)
            {
                return name.lexeme;
            }
        }
    }
}
