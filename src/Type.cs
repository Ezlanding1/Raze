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
            public Token name;
            public Type parent;

            public Type(Token name, Type parent)
            {
                this.name = name;
                this.parent = parent;
            }
            public Type(Token name)
            {
                this.name = name;
                this.parent = null;
            }

            public override string ToString()
            {
                return ((parent == null || parent.name.type == "") ? "" : (parent.ToString() + ".")) + name.lexeme;
            }
        }
    }
}
