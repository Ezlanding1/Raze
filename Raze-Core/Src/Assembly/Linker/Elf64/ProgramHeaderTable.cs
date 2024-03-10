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

            public Elf64_Phdr(
                P_type p_type, P_flags p_flags, Elf64_Off p_offset, Elf64_Addr p_vaddr,
                Elf64_Addr p_paddr, Elf64_Xword p_filesz, Elf64_Xword p_memsz, Elf64_Xword p_align
            )
            {
                this.p_type = (Elf64_Word)p_type;
                this.p_flags = (Elf64_Word)p_flags;
                this.p_offset = p_offset;
                this.p_vaddr = p_vaddr;
                this.p_paddr = p_paddr;
                this.p_filesz = p_filesz;
                this.p_memsz = p_memsz;
                this.p_align = p_align;
            }

            public enum P_type : Elf64_Word
            {
                PT_NULL = 0x0,
                PT_LOAD = 0x1,
                PT_DYNAMIC = 0x2,
                PT_INTERP = 0x3,
                PT_NOTE = 0x4,
                PT_SHLIB = 0x5,
                PT_PHDR = 0x6,
                PT_TLS = 0x7,
                PT_LOOS = 0x60000000,
                PT_HIOS = 0x6FFFFFFF,
                PT_LOPROC = 0x70000000,
                PT_HIPRO = 0x7FFFFFFF
            }

            [Flags]
            public enum P_flags : Elf64_Word
            {
                PF_X = 0x1,
                PF_W = 0x2,
                PF_R = 0x4,
                PF_MASKOS = 0x00FF0000,
                PF_MASKPROC = 0xFF000000
            }
        }
    }
}
