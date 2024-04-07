using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

internal class SaveImportData(FileInfo fileInfo) : IDisposable
{
    FileInfo fileInfo = fileInfo;
    public bool isImport = SymbolTableSingleton.SymbolTable.isImport;

    public virtual void Dispose()
    {
        Diagnostics.file = fileInfo;
        SymbolTableSingleton.SymbolTable.isImport = isImport;
    }
}

internal class SaveImportAndSymbolTableData(List<Expr.Definition> globals, FileInfo fileInfo) : SaveImportData(fileInfo)
{
    private List<Expr.Definition> globals = globals;

    public override void Dispose()
    {
        SymbolTableSingleton.SymbolTable.globals = globals;
        base.Dispose();
    }
}
