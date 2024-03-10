using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze_Driver;

internal partial class Shell
{
    static class DebugPrinter
    {
        public static void PrintInput(CompileOptions compileOptions)
        {
            if (compileOptions.DebugInputOption)
            {
                // Debug - Print Input
                Raze.Tools.InputPrinter.PrintInput(compileOptions.FileArgument);
            }
        }

        public static void PrintTokens(CompileOptions compileOptions, List<Raze.Token> tokens)
        {
            if (compileOptions.DebugTokensOption)
            {
                // Debug - Print Tokens
                Raze.Tools.TokenPrinter.PrintTokens(tokens);
            }
        }

        public static void PrintAst(CompileOptions compileOptions, List<Raze.Expr> expressions)
        {
            if (compileOptions.DebugAstOption)
            {
                // Debug - Print AST
                Raze.Tools.ASTPrinter astPrinter = new();
                astPrinter.PrintAST(expressions);
            }
        }

        public static void PrintAssembly(CompileOptions compileOptions, Raze.CodeGen.Assembly assembly)
        {
            if (compileOptions.DebugAssemblyOption)
            {
                // Debug - Print Assembly
                Raze.Tools.AssemblyPrinter.PrintAssembly(assembly);
            }
        }
    }
}
