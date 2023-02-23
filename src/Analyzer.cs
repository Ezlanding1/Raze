using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Raze
{
    internal partial class Analyzer
    {
        List<Expr> expressions;
        Dictionary<string, Expr.Primitive> primitives;

        public Analyzer(List<Expr> expressions)
        {
            this.expressions = expressions;
        }

        internal List<Expr> Analyze(){
            Pass<object?> initialPass = new InitialPass(expressions);
            expressions = initialPass.Run();

            SymbolTableSingleton.SymbolTable.TopContext();

            if (SymbolTableSingleton.SymbolTable.other.main == null)
            {
                throw new Errors.BackendError("Main Not Found", "No Main method for entrypoint found");
            }
            CheckMain(SymbolTableSingleton.SymbolTable.other.main);

            Pass<object?> mainPass = new MainPass(expressions);
            expressions = mainPass.Run();

            Pass<string> TypeChackPass = new TypeCheckPass(expressions);
            expressions = TypeChackPass.Run();

            return expressions;
        }

        private void CheckMain(Expr.Function main)
        {
            if (main._returnType != "void" && main._returnType != "number")
            {
                throw new Errors.BackendError("Main Invalid Return Type", $"Main can only return types 'number', and 'void'. Got '{main._returnType}'");
            }
            foreach (var item in main.modifiers)
            {
                if (item.Key != "static" && item.Value)
                {
                    throw new Errors.BackendError("Main Invalid Modifier", $"Main cannot have the '{item.Key}' modifier");
                }
            } 
        }

        internal static string TypeOf(Expr literal)
        {
            if (literal is Expr.Class)
            {
                return ((Expr.Class)literal).name.lexeme;
            }
            if (literal is Expr.Function)
            {
                return ((Expr.Function)literal).name.lexeme;
            }
            if (literal is Expr.Variable)
            {
                return ((Expr.Variable)literal).type.lexeme;
            }
            if (literal is Expr.Literal)
            {
                var l = (Expr.Literal)literal;
                return l.literal.type;
            }
            if (literal is Expr.Keyword)
            {
                var l = (Expr.Keyword)literal;
                if (l.keyword == "null" || l.keyword == "void")
                {
                    return l.keyword;
                }
                if (l.keyword == "true" || l.keyword == "false")
                {
                    return "BOOLEAN";
                }
            }
            throw new Exception("Invalid TypeOf");
        }
    }

    
}
