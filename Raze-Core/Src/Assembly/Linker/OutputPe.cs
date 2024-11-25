using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Linker
{
    private void OutputPe(FileStream fs, Assembler assembler, SystemInfo systemInfo)
    {
        fs.Write(ToReadOnlySpan(
            new PE32Plus.IMAGE_DOS_HEADER(checked((uint)(Marshal.SizeOf<PE32Plus.IMAGE_DOS_HEADER>() + Marshal.SizeOf<PE32Plus.MS_DOS_STUB>())))
        ));
        fs.Write(ToReadOnlySpan(
            new PE32Plus.MS_DOS_STUB()
        ));

        fs.Write(ToReadOnlySpan(
            PE32PlusGenerator.GenerateNtHeaders(assembler, systemInfo, out uint firstSectionAddressUnaligned)
        ));

        fs.Write(ToReadOnlySpan(
            PE32PlusGenerator.GenerateSectionHeaders(assembler, systemInfo, firstSectionAddressUnaligned, out uint currentSectionRva)
        ));
        fs.Write(GetPaddingBytes((ulong)fs.Position, PeUtils.FileAlignment));


        fs.Write(assembler.text.ToArray(), 0, assembler.text.Count);
        fs.Write(GetPaddingBytes((ulong)assembler.text.Count, PeUtils.FileAlignment));

        fs.Write(assembler.data.ToArray(), 0, assembler.data.Count);
        fs.Write(GetPaddingBytes((ulong)assembler.data.Count, PeUtils.FileAlignment));


        (PE32Plus.IMAGE_IMPORT_DESCRIPTOR[] importDescriptorTable, PE32Plus.IMPORT_LOOKUP[] importLookupTable, byte[] imageImportByName) = 
            PE32PlusGenerator.GenerateImportTables(assembler, systemInfo, currentSectionRva);

        fs.Write(ToReadOnlySpan(importDescriptorTable));
        // ILT
        fs.Write(ToReadOnlySpan(importLookupTable));
        fs.Write(ToReadOnlySpan(imageImportByName));
        // IAT
        fs.Write(ToReadOnlySpan(importLookupTable));
        fs.Write(GetPaddingBytes((ulong)fs.Position, PeUtils.FileAlignment));

        List<(PE32Plus.IMAGE_BASE_RELOCATION, PE32Plus.IMAGE_BASE_RELOCATION_BLOCK[])> relocations =
            PE32PlusGenerator.GenerateRelocationTables(assembler, systemInfo);

        foreach (var relocation in relocations)
        {
            fs.Write(ToReadOnlySpan(in relocation.Item1));
            fs.Write(ToReadOnlySpan(relocation.Item2));
        }
        fs.Write(GetPaddingBytes((ulong)fs.Position, PeUtils.FileAlignment));
    }
}
