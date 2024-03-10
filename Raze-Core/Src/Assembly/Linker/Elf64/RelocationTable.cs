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
        internal struct Elf64_Rel
        {
            Elf64_Addr r_offset; /* Address of reference */
            Elf64_Xword r_info; /* Symbol index and type of relocation */
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct Elf64_Rela
        {
            Elf64_Addr r_offset; /* Address of reference */
            Elf64_Xword r_info; /* Symbol index and type of relocation */
            Elf64_Sxword r_addend; /* Constant part of expression */
        }
    }
}
