using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Espionage.tools
{
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
}
