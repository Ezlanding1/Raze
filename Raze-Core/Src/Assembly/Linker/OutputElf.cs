using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Linker
{
    private void OutputElf(FileStream fs, Assembler assembler, SystemInfo systemInfo)
    {
        Elf64.Elf64_Phdr[] programHeaders = Elf64Generator.GenerateProgramHeaders(assembler, systemInfo);

        Elf64.Elf64_Ehdr fileHeader = 
            Elf64Generator.GenerateFileHeader(
                Elf64.Elf64_Shdr.textVirtualAddress + (ulong)assembler.symbolTable.definitions["text._start"],
                checked((ushort)programHeaders.Length),
                systemInfo
            );
            
        fs.Write(ToReadOnlySpan(ref fileHeader));
        fs.Write(ToReadOnlySpan(programHeaders));
        fs.Write(GetPaddingBytes((ulong)fs.Position, (ulong)systemInfo.alignment));

        fs.Write(assembler.text.ToArray(), 0, assembler.text.Count);
        fs.Write(GetPaddingBytes((ulong)assembler.text.Count, (ulong)systemInfo.alignment));

        fs.Write(assembler.data.ToArray(), 0, assembler.data.Count);
        fs.Write(GetPaddingBytes((ulong)assembler.data.Count, (ulong)systemInfo.alignment));
    }
}
