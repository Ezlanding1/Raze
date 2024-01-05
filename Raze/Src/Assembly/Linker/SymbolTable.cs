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
        internal Stack<(string, int)> unresolvedLabels = new();
        internal Stack<(string, int)> unresolvedLocalLabels = new();
        internal Stack<(string, int)> unresolvedData = new();

        internal Dictionary<string, int> localLabels = new();
        internal Dictionary<string, int> labels = new();
        internal Dictionary<string, int> data = new();
    }
}
