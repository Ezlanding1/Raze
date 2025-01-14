using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public class SystemInfo
{
    public readonly CPU_Architecture architecture;
    public readonly OsAbi osabi;
    public readonly Endianness endianness;
    public readonly BitFormat bitFormat;
    public readonly int alignment;

    public SystemInfo(CPU_Architecture architecture, OsAbi osabi, Endianness endianness, BitFormat bitFormat, int alignment)
    {
        this.architecture = architecture;
        this.osabi = osabi;
        this.endianness = endianness;
        this.bitFormat = bitFormat;
        this.alignment = alignment;
    }
    public SystemInfo(CPU_Architecture architecture, OsAbi osabi, Endianness endianness, BitFormat bitFormat) : this(architecture, osabi, endianness, bitFormat, ArchitectureToAlignment(architecture))
    {
    }
    public SystemInfo(CPU_Architecture architecture, OsAbi osabi, BitFormat bitFormat, int alignment) : this(architecture, osabi, ArchitectureToEndianness(architecture), bitFormat, alignment)
    {
    }
    public SystemInfo(CPU_Architecture architecture, OsAbi osabi, BitFormat bitFormat) : this(architecture, osabi, ArchitectureToEndianness(architecture), bitFormat, ArchitectureToAlignment(architecture))
    {
    }

    public enum CPU_Architecture
    {
        AMD_x86_64 = Linker.Elf64.Elf64_Ehdr.E_machine.AMD_x86_64
    }

    public enum Endianness
    {
        LittleEndian = Linker.Elf64.Elf64_Ehdr.EI_DATA.LittleEndian,
        BigEndian = Linker.Elf64.Elf64_Ehdr.EI_DATA.BigEndian
    }

    public enum OsAbi
    {
        System_V = Linker.Elf64.Elf64_Ehdr.EI_OSABI.System_V,
        Linux = Linker.Elf64.Elf64_Ehdr.EI_OSABI.Linux,
        Windows
    }

    // Note: 32-Bit Format is not currently supported
    public enum BitFormat
    {
        _64BitFormat = Linker.Elf64.Elf64_Ehdr.EI_CLASS._64BitFormat
    }

    private static Endianness ArchitectureToEndianness(CPU_Architecture architecture)
    {
        switch (architecture)
        {
            case CPU_Architecture.AMD_x86_64:
                return Endianness.LittleEndian;
        }
        throw Diagnostics.Panic(new Diagnostic.DriverDiagnostic(Diagnostic.DiagnosticName.UnsupportedSystem_CPU_Architecture, architecture));
    }

    private static int ArchitectureToAlignment(CPU_Architecture architecture)
    {
        switch (architecture)
        {
            case CPU_Architecture.AMD_x86_64:
                return 0x1000;
        }
        throw Diagnostics.Panic(new Diagnostic.DriverDiagnostic(Diagnostic.DiagnosticName.UnsupportedSystem_CPU_Architecture, architecture));
    }

    public string GetRuntimeName()
    {
        return osabi switch
        {
            OsAbi.Linux or
            OsAbi.System_V => "x86_64_Linux_Runtime.rz",
            OsAbi.Windows => "x86_64_Windows_Runtime.rz",
            _ => throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic($"Runtime name not found for OsAbi {osabi}"))
        };
    }

    public bool IsUnix() => osabi != OsAbi.Windows;

    public bool OutputElf() => osabi != OsAbi.Windows;

    public string GetDefualtFileExtension() => OutputElf() ? ".elf" : ".exe";
}
