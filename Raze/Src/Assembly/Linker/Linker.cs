using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Linker
{
    public void Link(FileStream fs, Assembler assembler, SystemInfo systemInfo)
    {
        Resolver.ResolveReferences(assembler);

        Elf64.Elf64_Phdr[] programHeaders = Elf64Generator.GenerateProgramHeaders(assembler, systemInfo);

        fs.Write(
            Elf64.ToReadOnlySpan(Elf64Generator.GenerateFileHeader(
                Elf64.Elf64_Shdr.textVirtualAddress + (ulong)assembler.symbolTable.definitions["text." + CodeGen.ToMangledName(SymbolTableSingleton.SymbolTable.main)],
                checked((ushort)programHeaders.Length),
                systemInfo
            ))
        );

        fs.Write(
            Elf64.ToReadOnlySpan(programHeaders)
        );
        fs.Write(GetPaddingBytes((ulong)fs.Position, systemInfo.alignment));

        fs.Write(assembler.text.ToArray(), 0, assembler.text.Count);
        fs.Write(GetPaddingBytes((ulong)assembler.text.Count, systemInfo.alignment));
        fs.Write(assembler.data.ToArray(), 0, assembler.data.Count);
        fs.Write(GetPaddingBytes((ulong)assembler.data.Count, systemInfo.alignment));
    }

    private static byte[] GetPaddingBytes(ulong segmentCount, ulong alignment)
    {
        var padding = segmentCount % alignment;

        if (padding == 0)
        {
            return new byte[0];
        }
        return new byte[alignment - padding];
    }
}
