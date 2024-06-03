using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

internal class SaveImportData(Expr.Import.FileInfo currentFileInfo) : IDisposable
{
    Expr.Import.FileInfo currentFileInfo = currentFileInfo;

    public virtual void Dispose()
    {
        SymbolTableSingleton.SymbolTable.currentFileInfo = currentFileInfo;
    }
}
