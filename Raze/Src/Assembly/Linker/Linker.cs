using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Linker
{
    FileStream fs;

    public Linker(FileStream fs)
    {
        this.fs = fs;
    }

    public void Link(Assembler assembler)
    {
        while (assembler.symbolTable.unresolvedLabels.Count != 0)
        {
            if (assembler.symbolTable.labels.TryGetValue(assembler.symbolTable.unresolvedLabels.Peek().Item1, out var label))
            {
                Resolve(fs, assembler.symbolTable.unresolvedLabels.Pop().Item2, label);
            }
            else
            {
                Resolve(fs, assembler.symbolTable.unresolvedLabels.Peek().Item2, assembler.symbolTable.labels[assembler.symbolTable.unresolvedLabels.Pop().Item1]);
            }
        }
        while (assembler.symbolTable.unresolvedData.Count != 0)
        {
            if (assembler.symbolTable.data.TryGetValue(assembler.symbolTable.unresolvedData.Peek().Item1, out var data))
            {
                Resolve(fs, assembler.symbolTable.unresolvedData.Pop().Item2, data);
            }
            else
            {
                Resolve(fs, assembler.symbolTable.unresolvedData.Peek().Item2, assembler.symbolTable.data[assembler.symbolTable.unresolvedData.Pop().Item1]);
            }
        }
    }

    private static void Resolve(FileStream fs, long location, long data)
    {
        fs.Seek(location, SeekOrigin.Begin);
        fs.Write(BitConverter.GetBytes(data));
    }

    internal static void HandleLocalProcedureRefs(FileStream fs, SymbolTable symbolTable)
    {
        while (symbolTable.unresolvedLocalLabels.Count != 0)
        {
            Resolve(fs, symbolTable.unresolvedLocalLabels.Peek().Item2, symbolTable.localLabels[symbolTable.unresolvedLocalLabels.Pop().Item1]);
        }
        symbolTable.localLabels.Clear();
        fs.Seek(0, SeekOrigin.End);
    }
}
