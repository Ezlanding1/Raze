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
            }
            catch (Exception e)
            {
                if (e is Errors.LexError || e is Errors.ParseError)
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