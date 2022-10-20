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

                // Analyze Code for Syntax Violations
                Analyzer analyzer = new Analyzer(expressions);
                expressions = analyzer.Analyze();

                // Lower AST to ASM
                Assembler assembler = new(expressions);
                List<Instruction> output = assembler.Assemble();


                Console.WriteLine("global _start");
                Console.WriteLine("_start:");
                List<Instruction> header = new List<Instruction>()
                {
                    { new Instruction.Unary("CALL", "Main") },
                    { new Instruction.Binary("MOV", "RDI", "RAX") },
                    { new Instruction.Binary("MOV", "RAX", "60") },
                    { new Instruction.Zero("SYSCALL") }
                };
                PrintInstructions(header);
                PrintInstructions(output);
                static void PrintInstructions(List<Instruction> instructions)
                {
                    foreach (Instruction instruction in instructions)
                    {
                        if (instruction is Instruction.Function)
                        {
                            var ins = (Instruction.Function)instruction;
                            Console.WriteLine(ins.name + ":");
                        }
                        else if (instruction is Instruction.Binary)
                        {
                            var ins = (Instruction.Binary)instruction;
                            Console.WriteLine(ins.instruction + "\t" + ins.operand1 + ", " + ins.operand2);
                        }
                        else if (instruction is Instruction.Unary)
                        {
                            var ins = (Instruction.Unary)instruction;
                            Console.WriteLine(ins.instruction + "\t" + ins.operand);
                        }
                        else if (instruction is Instruction.Zero)
                        {
                            var ins = (Instruction.Zero)instruction;
                            Console.WriteLine(ins.instruction);
                        }
                    }
                }
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