using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze.Tools;

internal class InputPrinter
{
    internal static void PrintInput(string input)
    {
        Console.WriteLine(input);
    }
}
internal class TokenPrinter
{
    internal static void PrintTokens(List<Token> tokens)
    {
        foreach (var item in tokens)
        {
            string str = item.ToString();
            Console.WriteLine(str);
        }
    }
}
