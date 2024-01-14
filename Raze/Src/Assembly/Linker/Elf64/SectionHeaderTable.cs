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
        internal struct Elf64_Shdr
        {
            internal const Elf64_Off dataOffset = 0x402000;

            Elf64_Word sh_name; /* Section name */
            Elf64_Word sh_type; /* Section type */
            Elf64_Xword sh_flags; /* Section attributes */
            Elf64_Addr sh_addr; /* Virtual address in memory */
            Elf64_Off sh_offset; /* Offset in file */
            Elf64_Xword sh_size; /* Size of section */
            Elf64_Word sh_link; /* Link to other section */
            Elf64_Word sh_info; /* Miscellaneous information */
            Elf64_Xword sh_addralign; /* Address alignment boundary */
            Elf64_Xword sh_entsize; /* Size of entries, if section has table */
        }
    }
}