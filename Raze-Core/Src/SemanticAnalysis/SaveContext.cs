using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

internal class SaveContext : IDisposable
{
    Expr.Definition? context;

    internal SaveContext()
    {
        this.context = SymbolTableSingleton.SymbolTable.Current;
    }

    public void Dispose()
    {
        SymbolTableSingleton.SymbolTable.SetContext(context);
    }
}
