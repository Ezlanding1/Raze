using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze
{
    internal partial class Analyzer
    {
        public class Type
        {
            public TypeName name;
            public Type parent;

            public Type(TypeName name, Type parent)
            {
                this.name = name;
                this.parent = parent;
            }
            public Type(TypeName name)
            {
                this.name = name;
                this.parent = null;
            }

            public bool Matches(Type type)
            {
                return type._Matches(this) || ((parent != null) ? parent.Matches(type) : false);
            }

            private protected virtual bool _Matches(Type type)
            {
                return type == this;
            }

            public override string ToString() 
            {
                return name.ToString();
            }
        }

        public class LiteralType : Type
        {
            public LiteralType(TypeName name, Type parent) : base(name, parent)
            {
            }

            public LiteralType(TypeName name) : base(name)
            {
            }

            private protected override bool _Matches(Type type)
            {
                return (type == this || type == parent);
            } 

            public override string ToString()
            {
                return name.ToString();
            }
        }

        public class TypeName
        {
            public Token name;
            public TypeName parent;

            public TypeName(Token name, TypeName parent)
            {
                this.name = name;
                this.parent = parent;
            }
            public TypeName(Token name)
            {
                this.name = name;
                this.parent = null;
            }

            public override string ToString()
            {
                return name.lexeme != ""? 
                        (parent != null? 
                            parent.ToString() + "." :
                            "") 
                            + name.lexeme : 
                        name.type.ToString();
            }
        }
    }
}
