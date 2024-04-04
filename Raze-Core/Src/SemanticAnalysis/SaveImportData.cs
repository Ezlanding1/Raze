using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

internal class SaveImportData(List<Expr.Definition> globals, bool isImport, FileInfo fileInfo) : IDisposable
{
    private List<Expr.Definition> globals = globals;
    public bool isImport = isImport;
    FileInfo fileInfo = fileInfo;

    public void Dispose()
    {
        SymbolTableSingleton.SymbolTable.globals = globals;
        SymbolTableSingleton.SymbolTable.isImport = isImport;
        Diagnostics.file = fileInfo;
    }
}
