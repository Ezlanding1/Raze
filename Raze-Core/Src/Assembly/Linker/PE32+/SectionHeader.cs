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
        internal struct IMAGE_SECTION_HEADER
        {
            private const int IMAGE_SIZEOF_SHORT_NAME = 8;

            unsafe fixed BYTE Name[IMAGE_SIZEOF_SHORT_NAME];
            DWORD PhysicalAddress { get => VirtualSize; set => VirtualSize = value; }
            DWORD VirtualSize;
            DWORD VirtualAddress;
            DWORD SizeOfRawData;
            DWORD PointerToRawData;
            DWORD PointerToRelocations;
            DWORD PointerToLinenumbers;
            WORD NumberOfRelocations;
            WORD NumberOfLinenumbers;
            DWORD Characteristics;

            public IMAGE_SECTION_HEADER(char[] name, DWORD virtualSize, DWORD virtualAddress, DWORD sizeOfRawData, DWORD pointerToRawData, _Characteristics characteristics)
            {
                unsafe
                {
                    for (int i = 0; i < Math.Min(IMAGE_SIZEOF_SHORT_NAME, name.Length); i++)
                    {
                        this.Name[i] = (BYTE)name[i];
                    }
                }
                
                this.VirtualSize = virtualSize;
                this.VirtualAddress = virtualAddress;
                this.SizeOfRawData = sizeOfRawData;
                this.PointerToRawData = pointerToRawData;
                PointerToRelocations = 0;
                PointerToLinenumbers = 0;
                NumberOfRelocations = 0;
                NumberOfLinenumbers = 0;
                this.Characteristics = (DWORD)characteristics;
            }

            [Flags]
            public enum _Characteristics : DWORD
            {
                // Reserved for future use = 0x00000000, 0x00000001, 0x00000002, 0x00000004
                IMAGE_SCN_TYPE_NO_PAD = 0x00000008,
	            // Reserved for future use = 0x00000010
                IMAGE_SCN_CNT_CODE = 0x00000020,
                IMAGE_SCN_CNT_INITIALIZED_DATA = 0x00000040,
                IMAGE_SCN_CNT_UNINITIALIZED_DATA = 0x00000080,
                IMAGE_SCN_LNK_OTHER = 0x00000100,
                IMAGE_SCN_LNK_INFO = 0x00000200,
	            // Reserved for future use = 0x00000400
                IMAGE_SCN_LNK_REMOVE = 0x00000800,
                IMAGE_SCN_LNK_COMDAT = 0x00001000,
                IMAGE_SCN_GPREL = 0x00008000,
                IMAGE_SCN_MEM_PURGEABLE = 0x00020000,
                IMAGE_SCN_MEM_16BIT = 0x00020000,
                IMAGE_SCN_MEM_LOCKED = 0x00040000,
                IMAGE_SCN_MEM_PRELOAD = 0x00080000,
                IMAGE_SCN_ALIGN_1BYTES = 0x00100000,
                IMAGE_SCN_ALIGN_2BYTES = 0x00200000,
                IMAGE_SCN_ALIGN_4BYTES = 0x00300000,
                IMAGE_SCN_ALIGN_8BYTES = 0x00400000,
                IMAGE_SCN_ALIGN_16BYTES = 0x00500000,
                IMAGE_SCN_ALIGN_32BYTES = 0x00600000,
                IMAGE_SCN_ALIGN_64BYTES = 0x00700000,
                IMAGE_SCN_ALIGN_128BYTES = 0x00800000,
                IMAGE_SCN_ALIGN_256BYTES = 0x00900000,
                IMAGE_SCN_ALIGN_512BYTES = 0x00A00000,
                IMAGE_SCN_ALIGN_1024BYTES = 0x00B00000,
                IMAGE_SCN_ALIGN_2048BYTES = 0x00C00000,
                IMAGE_SCN_ALIGN_4096BYTES = 0x00D00000,
                IMAGE_SCN_ALIGN_8192BYTES = 0x00E00000,
                IMAGE_SCN_LNK_NRELOC_OVFL = 0x01000000,
                IMAGE_SCN_MEM_DISCARDABLE = 0x02000000,
                IMAGE_SCN_MEM_NOT_CACHED = 0x04000000,
                IMAGE_SCN_MEM_NOT_PAGED = 0x08000000,
                IMAGE_SCN_MEM_SHARED = 0x10000000,
                IMAGE_SCN_MEM_EXECUTE = 0x20000000,
                IMAGE_SCN_MEM_READ = 0x40000000,
                IMAGE_SCN_MEM_WRITE = 0x80000000
            }
        }
    }
}
