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
        internal struct IMAGE_OPTIONAL_HEADER64
        {
            internal const DWORD DefaultFileAlignment = 0x200;
            internal const ULONGLONG DefaultImageBase = 0x140000000;
            private const int IMAGE_NUMBEROF_DIRECTORY_ENTRIES = 16;

            WORD Magic;
            BYTE MajorLinkerVersion;
            BYTE MinorLinkerVersion;
            DWORD SizeOfCode;
            DWORD SizeOfInitializedData;
            DWORD SizeOfUninitializedData;
            DWORD AddressOfEntryPoint;
            DWORD BaseOfCode;
            ULONGLONG ImageBase;
            DWORD SectionAlignment;
            DWORD FileAlignment;
            WORD MajorOperatingSystemVersion;
            WORD MinorOperatingSystemVersion;
            WORD MajorImageVersion;
            WORD MinorImageVersion;
            WORD MajorSubsystemVersion;
            WORD MinorSubsystemVersion;
            DWORD Win32VersionValue;
            DWORD SizeOfImage;
            DWORD SizeOfHeaders;
            DWORD CheckSum;
            WORD Subsystem;
            WORD DllCharacteristics;
            ULONGLONG SizeOfStackReserve;
            ULONGLONG SizeOfStackCommit;
            ULONGLONG SizeOfHeapReserve;
            ULONGLONG SizeOfHeapCommit;
            DWORD LoaderFlags;
            DWORD NumberOfRvaAndSizes;
            DATA_DIRECTORY_INLINE_ARRAY DataDirectory;

            public IMAGE_OPTIONAL_HEADER64(DWORD sizeOfCode, DWORD sizeOfInitializedData, DWORD sizeOfUninitializedData, DWORD addressOfEntryPoint, DWORD baseOfCode, ULONGLONG imageBase, DWORD sectionAlignment, DWORD fileAlignment, WORD majorOperatingSystemVersion, WORD minorOperatingSystemVersion, WORD majorImageVersion, WORD minorImageVersion, WORD majorSubsystemVersion, WORD minorSubsystemVersion, DWORD sizeOfImage, DWORD sizeOfHeaders, DWORD checkSum, _Subsystem subsystem, _DllCharacteristics dllCharacteristics, ULONGLONG sizeOfStackReserve, ULONGLONG sizeOfStackCommit, ULONGLONG sizeOfHeapReserve, ULONGLONG sizeOfHeapCommit, IMAGE_DATA_DIRECTORY[] dataDirectory)
            {
                Magic = 0x20B;
                MajorLinkerVersion = 0;
                MinorLinkerVersion = 0;

                this.SizeOfCode = sizeOfCode;
                this.SizeOfInitializedData = sizeOfInitializedData;
                this.SizeOfUninitializedData = sizeOfUninitializedData;
                this.AddressOfEntryPoint = addressOfEntryPoint;
                this.BaseOfCode = baseOfCode;
                this.ImageBase = imageBase;
                this.SectionAlignment = sectionAlignment;
                this.FileAlignment = fileAlignment;
                this.MajorOperatingSystemVersion = majorOperatingSystemVersion;
                this.MinorOperatingSystemVersion = minorOperatingSystemVersion;
                this.MajorImageVersion = majorImageVersion;
                this.MinorImageVersion = minorImageVersion;
                this.MajorSubsystemVersion = majorSubsystemVersion;
                this.MinorSubsystemVersion = minorSubsystemVersion;
                this.Win32VersionValue = 0;
                this.SizeOfImage = sizeOfImage;
                this.SizeOfHeaders = sizeOfHeaders;
                this.CheckSum = checkSum;
                this.Subsystem = (WORD)subsystem;
                this.DllCharacteristics = (WORD)dllCharacteristics;
                this.SizeOfStackReserve = sizeOfStackReserve;
                this.SizeOfStackCommit = sizeOfStackCommit;
                this.SizeOfHeapReserve = sizeOfHeapReserve;
                this.SizeOfHeapCommit = sizeOfHeapCommit;
                this.LoaderFlags = 0;
                this.NumberOfRvaAndSizes = checked((uint)Math.Min(dataDirectory.Length, IMAGE_NUMBEROF_DIRECTORY_ENTRIES));

                for (int i = 0; i < this.NumberOfRvaAndSizes; i++)
                {
                    this.DataDirectory[i] = dataDirectory[i];
                }
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            [System.Runtime.CompilerServices.InlineArray(IMAGE_NUMBEROF_DIRECTORY_ENTRIES)]
            internal struct DATA_DIRECTORY_INLINE_ARRAY
            {
                IMAGE_DATA_DIRECTORY DataDirectory;
            }

            public enum _Subsystem : WORD
            {
                IMAGE_SUBSYSTEM_UNKNOWN = 0,
                IMAGE_SUBSYSTEM_NATIVE = 1,
                IMAGE_SUBSYSTEM_WINDOWS_GUI = 2,
                IMAGE_SUBSYSTEM_WINDOWS_CUI = 3,
                IMAGE_SUBSYSTEM_OS2_CUI = 5,
                IMAGE_SUBSYSTEM_POSIX_CUI = 7,
                IMAGE_SUBSYSTEM_NATIVE_WINDOWS = 8,
                IMAGE_SUBSYSTEM_WINDOWS_CE_GUI = 9,
                IMAGE_SUBSYSTEM_EFI_APPLICATION = 10,
                IMAGE_SUBSYSTEM_EFI_BOOT_SERVICE_DRIVER = 11,
                IMAGE_SUBSYSTEM_EFI_RUNTIME_DRIVER = 12,
                IMAGE_SUBSYSTEM_EFI_ROM = 13,
                IMAGE_SUBSYSTEM_XBOX = 14,
                IMAGE_SUBSYSTEM_WINDOWS_BOOT_APPLICATION = 16
            }

            [Flags]
            public enum _DllCharacteristics : WORD
            {
                // Reserved, must be zero = 0x0001, 0x0002, 0x0004, 0x0008
                IMAGE_DLLCHARACTERISTICS_HIGH_ENTROPY_VA = 0x0020,
                MAGE_DLLCHARACTERISTICS_DYNAMIC_BASE = 0x0040,
                IMAGE_DLLCHARACTERISTICS_FORCE_INTEGRITY = 0x0080,
                IMAGE_DLLCHARACTERISTICS_NX_COMPAT = 0x0100,
                IMAGE_DLLCHARACTERISTICS_NO_ISOLATION = 0x0200,
                IMAGE_DLLCHARACTERISTICS_NO_SEH = 0x0400,
                IMAGE_DLLCHARACTERISTICS_NO_BIND = 0x0800,
                IMAGE_DLLCHARACTERISTICS_APPCONTAINER = 0x1000,
                IMAGE_DLLCHARACTERISTICS_WDM_DRIVER = 0x2000,
                IMAGE_DLLCHARACTERISTICS_GUARD_CF = 0x4000,
                IMAGE_DLLCHARACTERISTICS_TERMINAL_SERVER_AWARE = 0x8000
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct IMAGE_DATA_DIRECTORY
        {
            DWORD VirtualAddress;
            DWORD Size;

            public IMAGE_DATA_DIRECTORY(DWORD virtualAddress, DWORD size)
            {
                this.VirtualAddress = virtualAddress;
                this.Size = size;
            }
        }
    }
}
