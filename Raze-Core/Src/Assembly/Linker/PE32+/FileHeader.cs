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
        internal struct IMAGE_FILE_HEADER
        {
            WORD Machine;
            WORD NumberOfSections;
            DWORD TimeDateStamp;
            DWORD PointerToSymbolTable;
            DWORD NumberOfSymbols;
            WORD SizeOfOptionalHeader;
            WORD Characteristics;

            public IMAGE_FILE_HEADER(_Machine machine, WORD numberOfSections, DWORD timeDateStamp, WORD sizeOfOptionalHeader, _Characteristics characteristics)
            {
                this.Machine = (WORD)machine;
                this.NumberOfSections = numberOfSections;
                this.TimeDateStamp = timeDateStamp;
                this.PointerToSymbolTable = 0;
                this.NumberOfSymbols = 0;
                this.SizeOfOptionalHeader = sizeOfOptionalHeader;
                this.Characteristics = (WORD)characteristics;
            }

            public enum _Machine : WORD
            {
                IMAGE_FILE_MACHINE_UNKNOWN = 0x0,
                IMAGE_FILE_MACHINE_ALPHA = 0x184,
                IMAGE_FILE_MACHINE_ALPHA64 = 0x284,
                IMAGE_FILE_MACHINE_AM33 = 0x1d3,
                IMAGE_FILE_MACHINE_AMD64 = 0x8664,
                IMAGE_FILE_MACHINE_ARM = 0x1c0,
                IMAGE_FILE_MACHINE_ARM64 = 0xaa64,
                IMAGE_FILE_MACHINE_ARMNT = 0x1c4,
                IMAGE_FILE_MACHINE_AXP64 = 0x284,
                IMAGE_FILE_MACHINE_EBC = 0xebc,
                IMAGE_FILE_MACHINE_I386 = 0x14c,
                IMAGE_FILE_MACHINE_IA64 = 0x200,
                IMAGE_FILE_MACHINE_LOONGARCH32 = 0x6232,
                IMAGE_FILE_MACHINE_LOONGARCH64 = 0x6264,
                IMAGE_FILE_MACHINE_M32R = 0x9041,
                IMAGE_FILE_MACHINE_MIPS16 = 0x266,
                IMAGE_FILE_MACHINE_MIPSFPU = 0x366,
                IMAGE_FILE_MACHINE_MIPSFPU16 = 0x466,
                IMAGE_FILE_MACHINE_POWERPC = 0x1f0,
                IMAGE_FILE_MACHINE_POWERPCFP = 0x1f1,
                IMAGE_FILE_MACHINE_R4000 = 0x166,
                IMAGE_FILE_MACHINE_RISCV32 = 0x5032,
                IMAGE_FILE_MACHINE_RISCV64 = 0x5064,
                IMAGE_FILE_MACHINE_RISCV128 = 0x5128,
                IMAGE_FILE_MACHINE_SH3 = 0x1a2,
                IMAGE_FILE_MACHINE_SH3DSP = 0x1a3,
                IMAGE_FILE_MACHINE_SH4 = 0x1a6,
                IMAGE_FILE_MACHINE_SH5 = 0x1a8,
                IMAGE_FILE_MACHINE_THUMB = 0x1c2,
                IMAGE_FILE_MACHINE_WCEMIPSV2 = 0x169
            }

            [Flags]
            public enum _Characteristics : WORD
            {
                IMAGE_FILE_RELOCS_STRIPPED = 0x0001,
                IMAGE_FILE_EXECUTABLE_IMAGE = 0x0002,
                IMAGE_FILE_LINE_NUMS_STRIPPED = 0x0004,
                IMAGE_FILE_LOCAL_SYMS_STRIPPED = 0x0008,
                IMAGE_FILE_AGGRESSIVE_WS_TRIM = 0x0010,
                IMAGE_FILE_LARGE_ADDRESS_AWARE = 0x0020,
                // Reserved for future use = 0x0040
                IMAGE_FILE_BYTES_REVERSED_LO = 0x0080,
                IMAGE_FILE_32BIT_MACHINE = 0x0100,
                IMAGE_FILE_DEBUG_STRIPPED = 0x0200,
                IMAGE_FILE_REMOVABLE_RUN_FROM_SWAP = 0x0400,
                IMAGE_FILE_NET_RUN_FROM_SWAP = 0x0800,
                IMAGE_FILE_SYSTEM = 0x1000,
                IMAGE_FILE_DLL = 0x2000,
                IMAGE_FILE_UP_SYSTEM_ONLY = 0x4000,
                IMAGE_FILE_BYTES_REVERSED_HI = 0x8000
            }
        }
    }
}
