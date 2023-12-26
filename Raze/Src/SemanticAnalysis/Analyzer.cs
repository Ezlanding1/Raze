using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Analyzer
{
    List<Expr> expressions;

    public Analyzer(List<Expr> expressions)
    {
        this.expressions = expressions;
    }

    public void Analyze()
    {
        // Semantic Analysis Pass 1 
        Pass<object?> initialPass = new InitialPass(expressions);
        initialPass.Run();

        // Semantic Analysis Pass 2 - Symbol Resolution And Type Check Analysis
        Pass<Expr.Type> mainPass = new MainPass(expressions);
        mainPass.Run();

        CheckMain(SymbolTableSingleton.SymbolTable.main);

        // AST Optimization Pass
        Pass<object?> optimizationPass = new OptimizationPass(expressions);
        optimizationPass.Run();
    }

    private void CheckMain(Expr.Function? main)
    {
        if (main == null)
        {
            Diagnostics.errors.Push(new Error.AnalyzerError("Entrypoint Not Found", "Program does not contain a Main method"));
            return;
        }

        if (!main.modifiers["static"])
        {
            Diagnostics.errors.Push(new Error.AnalyzerError("Invalid Main Function", "The Main function must be marked 'static'"));
            main.modifiers["static"] = true;
        }

        if (main._returnType.type.name.lexeme != "void" && !Analyzer.TypeCheckUtils.literalTypes[Parser.LiteralTokenType.INTEGER].Matches(main._returnType.type))
        {
            Diagnostics.errors.Push(new Error.AnalyzerError("Invalid Main Function", $"Main can only return types 'number', and 'void'. Got '{main._returnType.type}'"));
        }

        foreach (var item in main.modifiers.EnumerateTrueModifiers())
        {
            if (item != "static")
            {
                Diagnostics.errors.Push(new Error.AnalyzerError("Invalid Main Function", $"Main cannot have the '{item}' modifier"));
                main.modifiers[item] = false;
            }
        }
    }
}
