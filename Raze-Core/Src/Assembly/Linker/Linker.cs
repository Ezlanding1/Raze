using System.Runtime.InteropServices;
using System.Text;

namespace Raze;

public partial class Linker
{
    public void Link(FileStream fs, Assembler assembler, SystemInfo systemInfo)
    {
        Resolver.ResolveReferences(assembler);

        if (systemInfo.OutputElf())
        {
            Elf64.Elf64_Phdr[] programHeaders = Elf64Generator.GenerateProgramHeaders(assembler, systemInfo);

            fs.Write(
                ToReadOnlySpan(Elf64Generator.GenerateFileHeader(
                    Elf64.Elf64_Shdr.textVirtualAddress + (ulong)assembler.symbolTable.definitions["text._start"],
                    checked((ushort)programHeaders.Length),
                    systemInfo
                ))
            );

            fs.Write(
                ToReadOnlySpan(programHeaders)
            );
            fs.Write(GetPaddingBytes((ulong)fs.Position, (ulong)systemInfo.alignment));

            fs.Write(assembler.text.ToArray(), 0, assembler.text.Count);
            fs.Write(GetPaddingBytes((ulong)assembler.text.Count, (ulong)systemInfo.alignment));
            fs.Write(assembler.data.ToArray(), 0, assembler.data.Count);
            fs.Write(GetPaddingBytes((ulong)assembler.data.Count, (ulong)systemInfo.alignment));
        }
        else
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
                PE32PlusGenerator.GenerateSectionHeaders(assembler, systemInfo, firstSectionAddressUnaligned)
            ));
            fs.Write(GetPaddingBytes((ulong)fs.Position, PE32Plus.IMAGE_OPTIONAL_HEADER64.DefaultFileAlignment));


            fs.Write(assembler.text.ToArray(), 0, assembler.text.Count);
            fs.Write(GetPaddingBytes((ulong)assembler.text.Count, PE32Plus.IMAGE_OPTIONAL_HEADER64.DefaultFileAlignment));

            fs.Write(assembler.data.ToArray(), 0, assembler.data.Count);
            fs.Write(GetPaddingBytes((ulong)assembler.data.Count, PE32Plus.IMAGE_OPTIONAL_HEADER64.DefaultFileAlignment));
        }
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
