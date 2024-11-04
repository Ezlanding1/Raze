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
        internal struct IMAGE_DOS_HEADER
        {      
            // DOS .EXE header
            WORD e_magic;                     // Magic number
            WORD e_cblp;                      // Bytes on last page of file
            WORD e_cp;                        // Pages in file
            WORD e_crlc;                      // Relocations
            WORD e_cparhdr;                   // Size of header in paragraphs
            WORD e_minalloc;                  // Minimum extra paragraphs needed
            WORD e_maxalloc;                  // Maximum extra paragraphs needed
            WORD e_ss;                        // Initial (relative) SS value
            WORD e_sp;                        // Initial SP value
            WORD e_csum;                      // Checksum
            WORD e_ip;                        // Initial IP value
            WORD e_cs;                        // Initial (relative) CS value
            WORD e_lfarlc;                    // File address of relocation table
            WORD e_ovno;                      // Overlay number
            unsafe fixed WORD e_res[4];       // Reserved words
            WORD e_oemid;                     // OEM identifier (for e_oeminfo)
            WORD e_oeminfo;                   // OEM information; e_oemid specific
            unsafe fixed WORD e_res2[10];     // Reserved words
            DWORD e_lfanew;                    // File address of new exe header

            public IMAGE_DOS_HEADER(DWORD e_lfanew)
            {
                e_magic = 0x5A4D;

                this.e_lfanew = e_lfanew;
            }
        }
    }
}
