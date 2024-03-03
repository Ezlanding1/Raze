#define DEBUG

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze_Cli.Src;

internal class Shell
{
    const string fileExtension = ".rz";

    static void Main(string[] args)
    {
        if (args.Length == 1 && args[0].EndsWith(fileExtension) && File.Exists(args[0]))
        {
            Run(args[0]);
        }
        else
        {
            Console.WriteLine("Usage: Raze [script]" + fileExtension);
            Environment.Exit(64);
        }
    }

    static void Run(string fileName)
    {
        #if DEBUG
        var watch = Stopwatch.StartNew();
        #endif
        try
        {
            // Store SytemInfo Data
            Raze.SystemInfo systemInfo = new Raze.SystemInfo(Raze.SystemInfo.CPU_Architecture.AMD_x86_64, Raze.SystemInfo.OsAbi.Linux, Raze.SystemInfo.BitFormat._64BitFormat);

            #if DEBUG
            Raze.Tools.InputPrinter.PrintInput(fileName);
            #endif

            // Pass Input Into Lexer
            Raze.Lexer lexer = new Raze.Lexer(fileName);
            var tokens = lexer.Tokenize();

            #if DEBUG
            Raze.Tools.TokenPrinter.PrintTokens(tokens);
            #endif

            // Parse Tokens
            Raze.Parser parser = new Raze.Parser(tokens);
            List<Raze.Expr> expressions = parser.Parse();

            #if DEBUG
            Raze.Tools.ASTPrinter astPrinter = new();
            astPrinter.PrintAST(expressions);
            #endif

            // Run Analysis on the Code 
            Raze.Analyzer analyzer = new Raze.Analyzer(expressions);
            analyzer.Analyze();

            // Throw any encountered compile errors
            Raze.Diagnostics.ThrowCompilerErrors();

            // Lower AST to ASM
            Raze.CodeGen codeGen = new Raze.InlinedCodeGen(expressions);
            var assembly = codeGen.Generate();

            #if DEBUG
            Raze.Tools.AssemblyPrinter.PrintAssembly(assembly);
            #endif

            // Assemble Assembly Expressions
            Raze.Assembler assembler = new Raze.Assembler();
            assembler.Assemble(assembly);

            using (var fs = new FileStream("output.elf", FileMode.Create, FileAccess.Write))
            {
                // Link and Output Binary
                Raze.Linker linker = new Raze.Linker();
                linker.Link(fs, assembler, systemInfo);
            }

            // Throw any encountered assembling errors
            Raze.Diagnostics.ThrowCompilerErrors();
        }
        catch (Exception e)
        {
            if (e is Raze.Error err)
            {
                if (Raze.Diagnostics.errors.Count != 0)
                {
                    Raze.Diagnostics.errors.ComposeErrorReport();
                }
                Console.WriteLine(err.ComposeErrorMessage());
            }
            else
            {
                Console.WriteLine("INTERNAL ERROR:");
                Console.WriteLine(e.Message);
            }
            Environment.Exit(65);
        }

        #if DEBUG
        watch.Stop();
        var elapsedMs = watch.ElapsedMilliseconds;
        Console.WriteLine("ELASPED MS: " + elapsedMs);
        #endif
    }
}