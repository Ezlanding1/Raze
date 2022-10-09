using System;
using System.Collections.Generic;
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
            var watch = System.Diagnostics.Stopwatch.StartNew();
            // the code that you want to measure comes here

            try
            {
                Lexer lexer = new Lexer(text);
                var tokens = lexer.Tokenize();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Environment.Exit(65);
            }
            
            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
            Console.WriteLine("ELASPED MS: " + elapsedMs);
            //foreach (var item in tokens)
            //{
            //    string str = item.ToString();
            //    Console.WriteLine(str);
            //    //Thread.Sleep(500);
            //}
        }
    }
}