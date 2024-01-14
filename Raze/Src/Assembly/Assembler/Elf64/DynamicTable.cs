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
        internal struct Elf64_Dyn_d_val
        {
            Elf64_Sxword d_tag;
            Elf64_Xword d_val;
        }
        
        internal struct Elf64_Dyn_d_ptr
        {
            Elf64_Sxword d_tag;
            Elf64_Addr d_ptr;
        }
    }
}
