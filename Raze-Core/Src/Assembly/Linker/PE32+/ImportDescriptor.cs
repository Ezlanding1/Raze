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
        internal struct IMAGE_IMPORT_DESCRIPTOR
        {
            DWORD Characteristics { get => OriginalFirstThunk; set => OriginalFirstThunk = value; }
            DWORD OriginalFirstThunk;
            DWORD TimeDateStamp;
            DWORD ForwarderChain;
            DWORD Name;
            DWORD FirstThunk;

            public IMAGE_IMPORT_DESCRIPTOR(DWORD originalFirstThunk, DWORD timeDateStamp, DWORD forwarderChain, DWORD name, DWORD firstThunk)
            {
                this.OriginalFirstThunk = originalFirstThunk;
                this.TimeDateStamp = timeDateStamp;
                this.ForwarderChain = forwarderChain;
                this.Name = name;
                this.FirstThunk = firstThunk;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct IMAGE_BOUND_IMPORT_DESCRIPTOR
        {
            DWORD TimeDateStamp;
            WORD OffsetModuleName;
            WORD NumberOfModuleForwarderRefs;

            public IMAGE_BOUND_IMPORT_DESCRIPTOR(DWORD timeDateStamp, WORD offsetModuleName, WORD numberOfModuleForwarderRefs)
            { 
                this.TimeDateStamp = timeDateStamp;
                this.OffsetModuleName = offsetModuleName;
                this.NumberOfModuleForwarderRefs = numberOfModuleForwarderRefs;
            }
        }
    }
}
