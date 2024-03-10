using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze_Driver;

internal partial class Shell
{
    static void Run(CompileOptions compileOptions)
    {
        // Store SytemInfo Data
        Raze.SystemInfo systemInfo = new Raze.SystemInfo(Raze.SystemInfo.CPU_Architecture.AMD_x86_64, Raze.SystemInfo.OsAbi.Linux, Raze.SystemInfo.BitFormat._64BitFormat);

        DebugPrinter.PrintInput(compileOptions);

        // Pass Input Into Lexer
        Raze.Lexer lexer = new Raze.Lexer(compileOptions.FileArgument);
        var tokens = lexer.Tokenize();

        DebugPrinter.PrintTokens(compileOptions, tokens);

        // Parse Tokens
        Raze.Parser parser = new Raze.Parser(tokens);
        List<Raze.Expr> expressions = parser.Parse();

        DebugPrinter.PrintAst(compileOptions, expressions);

        // Run Analysis on the Code 
        Raze.Analyzer analyzer = new Raze.Analyzer(expressions);
        analyzer.Analyze();

        // Throw any encountered compile errors
        Raze.Diagnostics.ThrowCompilerErrors();

        // Lower AST to ASM
        Raze.CodeGen codeGen = new Raze.InlinedCodeGen(expressions);
        var assembly = codeGen.Generate();

        DebugPrinter.PrintAssembly(compileOptions, assembly);
        
        // Assemble Assembly Expressions
        Raze.Assembler assembler = new Raze.Assembler();
        assembler.Assemble(assembly);

        if (!compileOptions.DryRunOption)
        {
            using (var fs = new FileStream(compileOptions.OutputOption, FileMode.Create, FileAccess.Write))
            {
                // Link and Output Binary
                Raze.Linker linker = new Raze.Linker();
                linker.Link(fs, assembler, systemInfo);
            }
        }

        // Throw any encountered assembling errors
        Raze.Diagnostics.ThrowCompilerErrors();
    }
}
