using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Linker
{
    internal class SymbolTable
    {
        internal Stack<(string, long)> unresolvedLabels = new();
        internal Stack<(string, long)> unresolvedLocalLabels = new();
        internal Stack<(string, long)> unresolvedData = new();

        internal Dictionary<string, long> localLabels = new();
        internal Dictionary<string, long> labels = new();
        internal Dictionary<string, long> data = new();

        internal static Dictionary<string, long> globalLabels = new();
        internal static Dictionary<string, long> globalData = new();
    }
}
