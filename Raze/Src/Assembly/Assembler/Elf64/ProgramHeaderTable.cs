using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        internal struct Elf64_Phdr
        {
            Elf64_Word p_type; /* Type of segment */
            Elf64_Word p_flags; /* Segment attributes */
            Elf64_Off p_offset; /* Offset in file */
            Elf64_Addr p_vaddr; /* Virtual address in memory */
            Elf64_Addr p_paddr; /* Reserved */
            Elf64_Xword p_filesz; /* Size of segment in file */
            Elf64_Xword p_memsz; /* Size of segment in memory */
            Elf64_Xword p_align; /* Alignment of segment */
        }
    }
}
