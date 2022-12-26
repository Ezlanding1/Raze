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
        Expr.Assign.Function main;

        public Analyzer(List<Expr> expressions)
        {
            this.expressions = expressions;
        }

        internal (List<Expr>, Expr.Function) Analyze(){
            Pass<object?> initialPass = new InitialPass(expressions);
            expressions = initialPass.Run();

            main = ((InitialPass)initialPass).GetOutput();
            if (main == null)
            {
                throw new Errors.BackendError(ErrorType.BackendException, "Main Not Found", "No Main method for entrypoint found");
            }
            CheckMain();
            Pass<object?> mainPass = new MainPass(expressions);
            expressions = mainPass.Run();

            Pass<string> TypeChackPass = new TypeCheckPass(expressions);
            expressions = TypeChackPass.Run();

            return (expressions, main);
        }

        private void CheckMain()
        {
            if (main._returnType != "void" && main._returnType != "number")
            {
                throw new Errors.BackendError(ErrorType.BackendException, "Main Invalid Return Type", $"Main can only return types 'number', and 'void'. Got '{main._returnType}'");
            }
            foreach (var item in main.modifiers)
            {
                if (item.Key != "static" && item.Value)
                {
                    throw new Errors.BackendError(ErrorType.BackendException, "Main Invalid Modifier", $"Main cannot have the '{item.Key}' modifier");
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
                if (l.literal.type == "NUMBERDOT")
                {
                    return "number";
                }

                if (l.literal.type == "STRING")
                {
                    return "string";
                }

                if (l.literal.type == "NUMBER")
                {
                    return "number";
                }
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
                    return "bool";
                }
            }
            throw new Exception("Invalid TypeOf");
        }

        internal static int SizeOf(string type, Expr value=null)
        {
            if (value != null)
            {
                if (value is Expr.New)
                {
                    return 8;
                }
            }
            if (Primitives.PrimitiveSize.ContainsKey(type))
            {
                return Primitives.PrimitiveSize[type];
            }
            throw new Exception("Invalid sizeOf");
        }
    }

    
}
