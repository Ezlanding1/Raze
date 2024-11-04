using Raze;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Raze_Driver;

internal partial class Shell
{
    internal static class SystemInfoGenerator
    {
        // CPU: System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture
        // OsAbi: Environment.OSVersion.Platform
        // Endianness: BitConverter.IsLittleEndian
        // BitFormat: System.Environment.Is64BitOperatingSystem
        // Alignment: Environment.SystemPageSize

        public static SystemInfo.CPU_Architecture GetArchitecture => RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => SystemInfo.CPU_Architecture.AMD_x86_64,

            _ =>
                throw Diagnostics.Panic(new Diagnostic.DriverDiagnostic(
                    Diagnostic.DiagnosticName.UnsupportedSystem_CPU_Architecture,
                    RuntimeInformation.ProcessArchitecture
                ))
        };

        public static SystemInfo.OsAbi GetOsabi => Environment.OSVersion.Platform switch
        {
            PlatformID.Unix => SystemInfo.OsAbi.Linux,
            PlatformID.Win32NT => SystemInfo.OsAbi.Windows,

            _ =>
                throw Diagnostics.Panic(new Diagnostic.DriverDiagnostic(
                    Diagnostic.DiagnosticName.UnsupportedSystem_OsAbi,
                    Environment.OSVersion.Platform
                ))
        };

        public static SystemInfo.Endianness GetEndianness => BitConverter.IsLittleEndian ?
            SystemInfo.Endianness.LittleEndian :
            SystemInfo.Endianness.BigEndian;

        public static SystemInfo.BitFormat GetBitFormat = Environment.Is64BitOperatingSystem ?
            SystemInfo.BitFormat._64BitFormat :
            throw Diagnostics.Panic(new Diagnostic.DriverDiagnostic(Diagnostic.DiagnosticName.UnsupportedSystem_BitFormat, "32BitFormat"));

        public static int GetAlignment => Environment.SystemPageSize;
    }
}
