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
    }
}
