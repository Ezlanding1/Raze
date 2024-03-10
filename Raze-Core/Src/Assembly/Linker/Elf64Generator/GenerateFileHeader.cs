using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Linker
{
    internal static partial class Elf64Generator
    {
        // Note: Currently, there is no generation of a Section Header Table. This will be fixed in future versions of the linker
        public static Elf64.Elf64_Ehdr GenerateFileHeader(ulong e_entry, ushort e_phnum, SystemInfo systemInfo)
        {
            return new Elf64.Elf64_Ehdr(
                (Elf64.Elf64_Ehdr.EI_CLASS)systemInfo.bitFormat,
                (Elf64.Elf64_Ehdr.EI_DATA)systemInfo.endianness,
                (Elf64.Elf64_Ehdr.EI_OSABI)systemInfo.osabi,
                Elf64.Elf64_Ehdr.E_type.ET_EXEC,
                (Elf64.Elf64_Ehdr.E_machine)systemInfo.architecture,
                e_entry,
                Elf64.Elf64_Ehdr.E_phoff.DefualtProgramHeaderTableLocation64,
                0,
                Elf64.Elf64_Ehdr.E_flags.EF_SPARCV9_TSO,
                Elf64.Elf64_Ehdr.E_ehsize.DefualtFileHeaderSize64,
                Elf64.Elf64_Ehdr.E_phentsize.DefualtProgramHeaderTableEntrySize64,
                e_phnum,
                Elf64.Elf64_Ehdr.E_shentsize.DefualtProgramSectionTableEntrySize64,
                0,
                0
            );
        }
    }
}
