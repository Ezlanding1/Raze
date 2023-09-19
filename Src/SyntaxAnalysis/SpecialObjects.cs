using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

internal partial class Analyzer
{
    internal class SpecialObjects
    {
        public class Any : Expr.Class
        {
            public Any() : base(new(Token.TokenType.IDENTIFIER, "any"), new(), new(), null)
            {
            }

            public override bool Match(Type type)
            {
                return true;
            }
            public override bool Matches(Type type)
            {
                return true;
            }
        }
    }
}
