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
        Resolver.ResolveReferences(assembler);
        fs.Write(assembler.text.ToArray(), 0, assembler.text.Count);
        fs.Write(assembler.data.ToArray(), 0, assembler.data.Count);
    }

    private static void FileResolve(FileStream fs, long location, long data)
    {
        fs.Seek(location, SeekOrigin.Begin);
        fs.Write(BitConverter.GetBytes(data));
    }

    private static void SectionResolve(List<byte> section, int location, ulong data)
    {
        byte[] bytes = BitConverter.GetBytes(data);
        for (int i = 0; i < 8; i++) section[location + i] = bytes[i];
    }
}
