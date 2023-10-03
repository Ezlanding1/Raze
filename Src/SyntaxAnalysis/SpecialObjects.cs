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
            public Any(Token name) : base(name, new(), new(), null)
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

        public static Expr.Function GenerateAnyFunction()
        {
            Expr.Function function = new(ExprUtils.Modifiers.FunctionModifierTemplate(), new(null, TypeCheckUtils.anyType), new(Token.TokenType.IDENTIFIER, "any"), new(), new());
            function.enclosing = TypeCheckUtils.anyType;
            return function;
        }

        public class DefaultConstructor : Expr.Function
        {
            public DefaultConstructor(Token name) : base(null, new(null), name, new(), new())
            {
                this.modifiers = ExprUtils.Modifiers.FunctionModifierTemplate();
                this.constructor = true;
                this.enclosing = SymbolTableSingleton.SymbolTable.Current;
                this._returnType.type = TypeCheckUtils._voidType;
            }
        }
    }
}
