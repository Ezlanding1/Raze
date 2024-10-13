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
    public static void Analyze()
    {
        SymbolTableSingleton.SymbolTable.IterateImports(
            x => {
                // Semantic Analysis Pass 1 
                Pass<object?> initialPass = new InitialPass(x.expressions);
                initialPass.Run();
            },
            x => {
                // Semantic Analysis Pass 2 - Symbol Resolution And Type Check Analysis
                Pass<Expr.Type> mainPass = new MainPass(x.expressions);
                mainPass.Run();
            }
        );

        // AST Optimization Pass
        Pass<object?> optimizationPass = new OptimizationPass(SymbolTableSingleton.SymbolTable.GetMainImportData().expressions);
        optimizationPass.Run();

        SymbolTableSingleton.SymbolTable.currentFileInfo = new(Diagnostics.mainFile);
        CheckMain(SymbolTableSingleton.SymbolTable.main);
    }

    private static void CheckMain(Expr.Function? main)
    {
        if (main == null)
        {
            Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.EntrypointNotFound));
            return;
        }

        if (!Primitives.IsVoidType(main._returnType.type) &&
            !TypeCheckUtils.literalTypes[Parser.LiteralTokenType.Integer].Matches(main._returnType.type) &&
            !TypeCheckUtils.literalTypes[Parser.LiteralTokenType.UnsignedInteger].Matches(main._returnType.type))
        {
            Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InvalidMainFunctionReturnType, main.name.location, main._returnType.type));
        }

        foreach (var item in main.modifiers.EnumerateTrueModifiers())
        {
            if (item != "static")
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InvalidMainFunctionModifier_NoInclude, main.name.location, item));
                main.modifiers[item] = false;
            }
        }
    }
}
