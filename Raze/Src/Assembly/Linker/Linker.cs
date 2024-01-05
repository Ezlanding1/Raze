using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Linker
{
    public void Link(FileStream fs, Assembler assembler)
    {
        fs.Write(assembler.text.ToArray(), 0, assembler.text.Count);
        fs.Write(assembler.data.ToArray(), 0, assembler.data.Count);

        while (assembler.symbolTable.unresolvedData.Count != 0)
        {
            Resolve(fs, assembler.symbolTable.unresolvedData.Peek().Item2, assembler.symbolTable.data[assembler.symbolTable.unresolvedData.Pop().Item1] + assembler.text.Count);
        }
    }

    private static void Resolve(FileStream fs, long location, long data)
    {
        fs.Seek(location, SeekOrigin.Begin);
        fs.Write(BitConverter.GetBytes(data));
    }
    
    private static void SectionResolve(List<byte> section, int location, int data)
    {
        byte[] bytes = BitConverter.GetBytes(data);
        for (int i = 0; i < 4; i++) section[location + i] = bytes[i];
    }

    internal static void ResolveProcedureRefs(List<byte> text, SymbolTable symbolTable)
    {
        while (symbolTable.unresolvedLabels.Count != 0)
        {
            SectionResolve(text, symbolTable.unresolvedLabels.Peek().Item2, symbolTable.labels[symbolTable.unresolvedLabels.Pop().Item1]);
        }
    }
    
    internal static void ResolveLocalProcedureRefs(List<byte> text, SymbolTable symbolTable)
    {
        while (symbolTable.unresolvedLocalLabels.Count != 0)
        {
            SectionResolve(text, symbolTable.unresolvedLocalLabels.Peek().Item2, symbolTable.localLabels[symbolTable.unresolvedLocalLabels.Pop().Item1]);
        }
        symbolTable.localLabels.Clear();
    }
}
