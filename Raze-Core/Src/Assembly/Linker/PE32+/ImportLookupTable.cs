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
using CHAR = char;

public partial class Linker
{
    internal partial class PE32Plus
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct IMPORT_LOOKUP
        {
            LONG Entry;

            public IMPORT_LOOKUP(LONG entry)
            {
                this.Entry = entry; 
            }

            // Import Lookup by ordinal
            public IMPORT_LOOKUP(WORD ordinal)
            {
                this.Entry = ordinal;
                this.Entry |= (1UL << 63);
            }

            public IMPORT_LOOKUP(DWORD nameTableRVA)
            {
                this.Entry = nameTableRVA;
                this.Entry &= ~(1UL << 63);
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        // Must be followed by an ASCII string representing the imported function's name
        internal struct IMAGE_IMPORT_BY_NAME
        {
            WORD Hint;

            public IMAGE_IMPORT_BY_NAME(WORD hint)
            {
                this.Hint = hint;
            }
        }
    }
}
