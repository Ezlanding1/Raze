#define DEBUG

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

internal class Shell
{
    const string fileExtension = ".rz";

    static void Main(string[] args)
    {
        if (args.Length == 1 && args[0].EndsWith(fileExtension))
        {
            Run(File.ReadAllText(args[0]));
        }
        else
        {
            Console.WriteLine("Usage: Raze [script]" + fileExtension);
            Environment.Exit(64);
        }
    }

    static void Run(string text)
    {
        #if DEBUG
        var watch = Stopwatch.StartNew();
        #endif
        try
        {
            #if DEBUG
            Raze.Tools.InputPrinter.PrintInput(text);
            #endif

            // Pass Input Into Lexer
            Lexer lexer = new Lexer(text);
            var tokens = lexer.Tokenize();

            #if DEBUG
            Raze.Tools.TokenPrinter.PrintTokens(tokens);
            #endif

            // Parse Tokens
            Parser parser = new Parser(tokens);
            List<Expr> expressions = parser.Parse();

            #if DEBUG
            Tools.ASTPrinter astPrinter = new();
            astPrinter.PrintAST(expressions);
            #endif

            // Run Analysis on the Code 
            Analyzer analyzer = new Analyzer(expressions);
            analyzer.Analyze();

            // Throw any encountered compile errors
            Diagnostics.ThrowCompilerErrors();

            // Lower AST to ASM
            Assembler assembler = new InlinedAssembler(expressions);
            var output = assembler.Assemble();
            var instructions = output.Item1;
            var data = output.Item2;


            #if DEBUG
            Raze.Tools.AssemblyPrinter.PrintAssembly(instructions, data, SymbolTableSingleton.SymbolTable.main);
            #endif

            // Throw any encountered assembling errors
            Diagnostics.ThrowCompilerErrors();

            // Output Result
            //string path = Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory) + "\\out.asm";
            //using (StreamWriter sr = new(path))
            //{
            //        sr.Write(output);
            //}
        }
        catch (Exception e)
        {
            if (e is Error err)
            {
                if (Diagnostics.errors.Count != 0)
                {
                    Diagnostics.errors.ComposeErrorReport();
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