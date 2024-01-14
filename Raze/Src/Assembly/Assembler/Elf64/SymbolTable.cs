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
        internal struct Elf64_Sym
        {
            Elf64_Word st_name; /* Symbol name */
            unsigned_char st_info; /* Type and Binding attributes */
            unsigned_char st_other; /* Reserved */
            Elf64_Half st_shndx; /* Section table index */
            Elf64_Addr st_value; /* Symbol value */
            Elf64_Xword st_size; /* Size of object (e.g., common) */
        }
    }
}
