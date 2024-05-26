using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Analyzer
{
    internal class SpecialObjects
    {
        public class Null() : Expr.Class(new(Token.TokenType.RESERVED, "null"), new(), new(), new(null))
        {
            public override bool Matches(Type type) => type.Matches(TypeCheckUtils.objectType);
        }
        public class Object() : Expr.Class(new(Token.TokenType.IDENTIFIER, "object"), new(), new(), new(null));

        public class Any(Token name) : Expr.Class(name, new(), new(), new(null))
        {
            public override bool Match(Type type) => true;
            public override bool Matches(Type type) => true;
        }

        public static Expr.Function GenerateAnyFunction()
        {
            Expr.Function function = new(ExprUtils.Modifiers.FunctionModifierTemplate(), false, new(null, TypeCheckUtils.anyType), new(Token.TokenType.IDENTIFIER, "any"), new(), new(new()));
            function.enclosing = TypeCheckUtils.anyType;
            return function;
        }

        public class DefaultConstructor : Expr.Function
        {
            public DefaultConstructor(Token name) : base(null, false, new(null), name, new(), new(new()))
            {
                this.modifiers = ExprUtils.Modifiers.FunctionModifierTemplate();
                this.constructor = true;
                this.enclosing = SymbolTableSingleton.SymbolTable.Current;
                this._returnType.type = TypeCheckUtils._voidType;
            }
        }

        public static Expr.Class GenerateImportToplevelWrapper(Expr.Import import, List<Expr> exprs)
        {
            string className = GetImportClassName(import.fileInfo.Name);
            return new Expr.Class(new(Token.TokenType.IDENTIFIER, className), new(), exprs.Where(x => x is Expr.Definition).Select(x => (Expr.Definition)x).ToList(), new(null));
        }
        public static string GetImportClassName(string name) =>
            name[..name.LastIndexOf(".rz")].Replace('.', '_');
    }
}
