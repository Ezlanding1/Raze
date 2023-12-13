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
        //while (assembler.symbolTable.unresolvedLabels.Count != 0)
        //{

        //}
    }

    private void Resolve(FileStream fs, long location, long data)
    {
        fs.Seek(location, SeekOrigin.Begin);
        fs.Write(BitConverter.GetBytes(data));
    }
}
