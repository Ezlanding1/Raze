using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

internal class SaveImportData(FileInfo fileInfo) : IDisposable
{
    FileInfo fileInfo = fileInfo;

    public virtual void Dispose()
    {
        Diagnostics.file = fileInfo;
    }
}

internal class SaveImportAndSymbolTableData(List<Expr.Definition> globals, bool isImport, FileInfo fileInfo) : SaveImportData(fileInfo)
{
    private List<Expr.Definition> globals = globals;
    public bool isImport = isImport;

    public override void Dispose()
    {
        SymbolTableSingleton.SymbolTable.isImport = isImport;
        SymbolTableSingleton.SymbolTable.globals = globals;
        base.Dispose();
    }
}
