#define DEBUG

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Espionage
{
    internal class Shell
    {
        static void Main(string[] args)
        {
            if (args.Length == 1 && args[0].EndsWith(".es"))
            {
                Run(File.ReadAllText(args[0]));
            }
            else
            {
                Console.WriteLine("Usage: Espionage [script].es");
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
                Espionage.tools.InputPrinter.PrintInput(text);
                #endif
                // Pass Input Into Lexer
                Lexer lexer = new Lexer(text);
                var tokens = lexer.Tokenize();

                #if DEBUG
                Espionage.tools.TokenPrinter.PrintTokens(tokens);
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
                (expressions, Expr.Function main) = analyzer.Analyze();

                // Lower AST to ASM
                Assembler assembler = new(expressions);
                var output = assembler.Assemble();
                var instructions = output.Item1;
                var data = output.Item2;


                #if DEBUG
                Espionage.tools.AssemblyPrinter.PrintAssembly(instructions, data, main);
                #endif
                // Output Result
                //string path = Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory) + "\\out.asm";
                //using (StreamWriter sr = new(path))
                //{
                //        sr.Write(output);
                //}
                // Open the stream and read it back.    

            }
            catch (Exception e)
            {
                if (e is Errors.LexError || e is Errors.ParseError || e is Errors.BackendError)
                {
                    Console.WriteLine(e.Message);
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
}