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
        public sealed class Null : Expr.Class
        {
            public Null() 
                : base(new(Token.TokenType.RESERVED, "null", Location.NoLocation), new(), new(), new(null)) { }

            public override bool Matches(Type type) => type.Matches(TypeCheckUtils.ObjectType);
        }

        public sealed class Object : Expr.Class
        {
            public Object() 
                : base(new(Token.TokenType.IDENTIFIER, "object", Location.NoLocation), new(), new(), new(null)) { }
        }

        public sealed class Any : Expr.Class
        {
            public Any(Token name) 
                : base(name, new(), new(), new(null)) { }

            public override bool Match(Type type) => true;
            public override bool Matches(Type type) => true;
        }

        public static Expr.Function GenerateAnyFunction()
        {
            var function = new Expr.Function(
                ExprUtils.Modifiers.FunctionModifierTemplate(),
                false,
                new(null, TypeCheckUtils.AnyType),
                new(Token.TokenType.IDENTIFIER, "any", Location.NoLocation),
                new(),
                new(new()),
                null
            );

            function.Enclosing = TypeCheckUtils.AnyType;
            return function;
        }

        public sealed class DefaultConstructor : Expr.Function
        {
            public DefaultConstructor(Token name) 
                : base(ExprUtils.Modifiers.FunctionModifierTemplate(), false, new(null), name, new(), new(new()), null)
            {
                Modifiers = ExprUtils.Modifiers.FunctionModifierTemplate();
                Constructor = true;
                Enclosing = SymbolTableSingleton.SymbolTable.Current;
                _returnType.Type = TypeCheckUtils.VoidType;
            }
        }

        public static Expr.Class GenerateImportToplevelWrapper(Expr.Import import)
        {
            string className = GetImportClassName(import.FileInfo.Name);
            var importClass = new Expr.Class(
                new(Token.TokenType.IDENTIFIER, className, Location.NoLocation),
                new List<Expr.Definition>(),
                new List<Expr>(),
                new(null)
            );
            return importClass;
        }

        public static void AddExprsToImportToplevelWrapper(Expr.Class importClass, List<Expr> exprs)
        {
            importClass.Definitions.AddRange(
                exprs.OfType<Expr.Definition>()
            );
        }

        public static void ParentExprsToImportTopLevelWrappers()
        {
            SymbolTableSingleton.SymbolTable.IterateImports(import =>
            {
                if (SymbolTableSingleton.SymbolTable.IsImport)
                {
                    import.ImportClass.Definitions.ForEach(x => x.Enclosing = import.ImportClass);
                }
            });
        }

        public static string GetImportClassName(string name) =>
            name[..name.LastIndexOf(".rz")].Replace('.', '_');

        public static Expr.Call GenerateRuntimeCall(List<Expr> args, Expr.Function internalFunction) =>
            new Expr.Call(new(Token.TokenType.IDENTIFIER, "", Location.NoLocation), args, null)
            {
                InternalFunction = internalFunction
            };
    }
}
