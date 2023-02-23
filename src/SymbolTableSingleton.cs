using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze
{
    internal class SymbolTableSingleton
    {
        private static Analyzer.SymbolTable? symbolTable = null;
        public static Analyzer.SymbolTable SymbolTable
        {
            get
            {
                if (symbolTable == null)
                {
                    symbolTable = new();
                }
                return symbolTable;
            }
        }
    }
}
