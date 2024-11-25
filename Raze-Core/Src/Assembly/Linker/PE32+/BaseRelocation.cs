using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

using BYTE = byte;
using WORD = ushort;
using DWORD = uint;
using QWORD = ulong;
using LONG = ulong;
using LONGLONG = long;
using ULONGLONG = ulong;

public partial class Linker
{
    internal partial class PE32Plus
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct IMAGE_BASE_RELOCATION
        {
            DWORD VirtualAddress;
            DWORD SizeOfBlock;

            public IMAGE_BASE_RELOCATION(DWORD virtualAddress, DWORD sizeOfBlock)
            {
                this.VirtualAddress = virtualAddress;
                this.SizeOfBlock = sizeOfBlock;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct IMAGE_BASE_RELOCATION_BLOCK
        {
            WORD _data;

            public IMAGE_BASE_RELOCATION_BLOCK(BaseRelocationType baseRelocationType, WORD offset)
            {
                this._data = offset;
                this._data = (WORD)((_data & 0xF) | ((WORD)baseRelocationType << 12));
            }

            public enum BaseRelocationType : WORD
            {
                IMAGE_REL_BASED_ABSOLUTE = 0,
                IMAGE_REL_BASED_HIGH = 1,
                IMAGE_REL_BASED_LOW = 2,
                IMAGE_REL_BASED_HIGHLOW = 3,
                IMAGE_REL_BASED_HIGHADJ = 4,
                IMAGE_REL_BASED_MIPS_JMPADDR = 5,
                IMAGE_REL_BASED_ARM_MOV32 = 5,
                IMAGE_REL_BASED_RISCV_HIGH20 = 5,
                // Reserved, must be zero = 6
                IMAGE_REL_BASED_THUMB_MOV32 = 7,
                IMAGE_REL_BASED_RISCV_LOW12I = 7,
                IMAGE_REL_BASED_RISCV_LOW12S = 8,
                IMAGE_REL_BASED_LOONGARCH32_MARK_LA = 8,
                IMAGE_REL_BASED_LOONGARCH64_MARK_LA = 8,
                IMAGE_REL_BASED_MIPS_JMPADDR16 = 9,
                IMAGE_REL_BASED_DIR64 = 10,
            }
        }
    }
}
