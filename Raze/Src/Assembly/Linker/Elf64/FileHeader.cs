using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Elf64_Addr = System.UInt64;
using Elf64_Off = System.UInt64;
using Elf64_Half = System.UInt16;
using Elf64_Word = System.UInt32;
using Elf64_Sword = System.Int32;
using Elf64_Xword = System.UInt64;
using Elf64_Sxword = System.Int64;
using unsigned_char = System.Byte;

namespace Raze;

public partial class Linker
{
    internal partial class Elf64
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct Elf64_Ehdr
        {
            unsafe fixed unsigned_char e_ident[16]; /* ELF identification */
            Elf64_Half e_type; /* Object file type */
            Elf64_Half e_machine; /* Machine type */
            Elf64_Word e_version; /* Object file version */
            Elf64_Addr e_entry; /* Entry point address */
            Elf64_Off e_phoff; /* Program header offset */
            Elf64_Off e_shoff; /* Section header offset */
            Elf64_Word e_flags; /* Processor-specific flags */
            Elf64_Half e_ehsize; /* ELF header size */
            Elf64_Half e_phentsize; /* Size of program header entry */
            Elf64_Half e_phnum; /* Number of program header entries */
            Elf64_Half e_shentsize; /* Size of section header entry */
            Elf64_Half e_shnum; /* Number of section header entries */
            Elf64_Half e_shstrndx; /* Section name string table index */
        }
    }
}
