using System;
using System.Collections.Generic;
using System.Linq;
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
        internal struct MS_DOS_STUB
        {
            unsafe fixed BYTE ms_dos_stub[64];

            public MS_DOS_STUB()
            {
                unsafe
                {
                    ms_dos_stub[0] = 0x0E;
                    ms_dos_stub[1] = 0x1F;
                    ms_dos_stub[2] = 0xBA;
                    ms_dos_stub[3] = 0x0E;
                    ms_dos_stub[4] = 0x00;
                    ms_dos_stub[5] = 0xB4;
                    ms_dos_stub[6] = 0x09;
                    ms_dos_stub[7] = 0xCD;
                    ms_dos_stub[8] = 0x21;
                    ms_dos_stub[9] = 0xB8;
                    ms_dos_stub[10] = 0x01;
                    ms_dos_stub[11] = 0x4C;
                    ms_dos_stub[12] = 0xCD;
                    ms_dos_stub[13] = 0x21;
                    ms_dos_stub[14] = 0x54;
                    ms_dos_stub[15] = 0x68;
                    ms_dos_stub[16] = 0x69;
                    ms_dos_stub[17] = 0x73;
                    ms_dos_stub[18] = 0x20;
                    ms_dos_stub[19] = 0x70;
                    ms_dos_stub[20] = 0x72;
                    ms_dos_stub[21] = 0x6F;
                    ms_dos_stub[22] = 0x67;
                    ms_dos_stub[23] = 0x72;
                    ms_dos_stub[24] = 0x61;
                    ms_dos_stub[25] = 0x6D;
                    ms_dos_stub[26] = 0x20;
                    ms_dos_stub[27] = 0x63;
                    ms_dos_stub[28] = 0x61;
                    ms_dos_stub[29] = 0x6E;
                    ms_dos_stub[30] = 0x6E;
                    ms_dos_stub[31] = 0x6F;
                    ms_dos_stub[32] = 0x74;
                    ms_dos_stub[33] = 0x20;
                    ms_dos_stub[34] = 0x62;
                    ms_dos_stub[35] = 0x65;
                    ms_dos_stub[36] = 0x20;
                    ms_dos_stub[37] = 0x72;
                    ms_dos_stub[38] = 0x75;
                    ms_dos_stub[39] = 0x6E;
                    ms_dos_stub[40] = 0x20;
                    ms_dos_stub[41] = 0x69;
                    ms_dos_stub[42] = 0x6E;
                    ms_dos_stub[43] = 0x20;
                    ms_dos_stub[44] = 0x44;
                    ms_dos_stub[45] = 0x4F;
                    ms_dos_stub[46] = 0x53;
                    ms_dos_stub[47] = 0x20;
                    ms_dos_stub[48] = 0x6D;
                    ms_dos_stub[49] = 0x6F;
                    ms_dos_stub[50] = 0x64;
                    ms_dos_stub[51] = 0x65;
                    ms_dos_stub[52] = 0x2E;
                    ms_dos_stub[53] = 0x0D;
                    ms_dos_stub[54] = 0x0D;
                    ms_dos_stub[55] = 0x0A;
                    ms_dos_stub[56] = 0x24;
                    ms_dos_stub[57] = 0x00;
                    ms_dos_stub[58] = 0x00;
                    ms_dos_stub[59] = 0x00;
                    ms_dos_stub[60] = 0x00;
                    ms_dos_stub[61] = 0x00;
                    ms_dos_stub[62] = 0x00;
                    ms_dos_stub[63] = 0x00;
                }
            }
        }
    }
}
