﻿#define DEBUG

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
            Raze.Lexer lexer = new Raze.Lexer(text);
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
            Raze.CodeGen assembler = new Raze.InlinedCodeGen(expressions);
            var output = assembler.Assemble();
            var instructions = output.Item1;
            var data = output.Item2;


            #if DEBUG
            Raze.Tools.AssemblyPrinter.PrintAssembly(instructions, data);
            #endif

            // Throw any encountered assembling errors
            Raze.Diagnostics.ThrowCompilerErrors();

            // Output Result
            //string path = Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory) + "\\out.asm";
            //using (StreamWriter sr = new(path))
            //{
            //        sr.Write(output);
            //}
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