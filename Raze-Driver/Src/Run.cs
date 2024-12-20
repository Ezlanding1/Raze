﻿using System;
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
        Raze.SystemInfo systemInfo = compileOptions.SystemInfo;

        DebugPrinter.PrintInput(compileOptions);

        // Pass Input Into Lexer
        Raze.Lexer lexer = new Raze.Lexer(compileOptions.FileArgument);
        var tokens = lexer.Tokenize();

        DebugPrinter.PrintTokens(compileOptions, tokens);

        // Parse Tokens
        Raze.Parser.Parse(tokens);

        DebugPrinter.PrintAst(compileOptions);

        // Run Analysis on the Code 
        Raze.Analyzer.Analyze();
        
        // Throw any encountered compile errors
        Raze.Diagnostics.ThrowCompilerErrors();

        // Lower AST to ASM
        Raze.CodeGen codeGen = new Raze.InlinedCodeGen(systemInfo);
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
