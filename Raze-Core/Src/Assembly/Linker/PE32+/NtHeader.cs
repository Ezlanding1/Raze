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
        internal struct IMAGE_NT_HEADERS64
        {
            DWORD Signature;
            IMAGE_FILE_HEADER FileHeader;
            IMAGE_OPTIONAL_HEADER64 OptionalHeader;

            public IMAGE_NT_HEADERS64(IMAGE_FILE_HEADER fileHeader, IMAGE_OPTIONAL_HEADER64 optionalHeader)
            {
                Signature = 0x00004550;
                this.FileHeader = fileHeader;
                this.OptionalHeader = optionalHeader;
            }
        }
    }
}
