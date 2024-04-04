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

        if (!SymbolTableSingleton.SymbolTable.isImport)
        {
            CheckMain(SymbolTableSingleton.SymbolTable.main);
        }

        // AST Optimization Pass
        Pass<object?> optimizationPass = new OptimizationPass(expressions);
        optimizationPass.Run();
    }

    private void CheckMain(Expr.Function? main)
    {
        if (main == null)
        {
            Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.EntrypointNotFound));
            return;
        }

        if (main._returnType.type.name.lexeme != "void" && !TypeCheckUtils.literalTypes[Parser.LiteralTokenType.Integer].Matches(main._returnType.type))
        {
            Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InvalidMainFunctionReturnType, main._returnType.type));
        }

        foreach (var item in main.modifiers.EnumerateTrueModifiers())
        {
            if (item != "static")
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InvalidMainFunctionModifier_NoInclude, item));
                main.modifiers[item] = false;
            }
        }
    }
}
