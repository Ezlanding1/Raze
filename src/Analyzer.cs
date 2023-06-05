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

        public Analyzer(List<Expr> expressions)
        {
            this.expressions = expressions;
        }

        internal List<Expr> Analyze(){
            Pass<object?> initialPass = new InitialPass(expressions);
            expressions = initialPass.Run();

            if (SymbolTableSingleton.SymbolTable.main == null)
            {
                throw new Errors.AnalyzerError("Main Not Found", "No Main method for entrypoint found");
            }

            Pass<object?> mainPass = new MainPass(expressions);
            expressions = mainPass.Run();

            Pass<Expr.Type> TypeChackPass = new TypeCheckPass(expressions);
            expressions = TypeChackPass.Run();

            CheckMain(SymbolTableSingleton.SymbolTable.main);

            return expressions;
        }

        private void CheckMain(Expr.Function main)
        {
            if (main._returnType.type.name.lexeme != "void" && !Analyzer.TypeCheckPass.literalTypes[Token.TokenType.INTEGER].Matches(main._returnType.type))
            {
                throw new Errors.AnalyzerError("Main Invalid Return Type", $"Main can only return types 'number', and 'void'. Got '{main._returnType.type}'");
            }

            foreach (var item in main.modifiers)
            {
                if (item.Value && item.Key != "static")
                {
                    throw new Errors.AnalyzerError("Main Invalid Modifier", $"Main cannot have the '{item.Key}' modifier");
                }
            } 
        }
    }

    
}
