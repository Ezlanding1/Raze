using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public class SystemInfo
{
    internal CPU_Architecture architecture;
    internal OsAbi osabi;
    internal Endianness endianess;
    internal BitFormat bitFormat;
    internal ulong alignment;

    public SystemInfo(CPU_Architecture architecture, OsAbi osabi, Endianness endianess, BitFormat bitFormat, ulong alignment)
    {
        this.architecture = architecture;
        this.osabi = osabi;
        this.endianess = endianess;
        this.bitFormat = bitFormat;
        this.alignment = alignment;
    }
    public SystemInfo(CPU_Architecture architecture, OsAbi osabi, Endianness endianess, BitFormat bitFormat) : this(architecture, osabi, endianess, bitFormat, ArchitectureToAlignment(architecture))
    {
    }
    public SystemInfo(CPU_Architecture architecture, OsAbi osabi, BitFormat bitFormat, ulong alignment) : this(architecture, osabi, ArchitectureToEndianness(architecture), bitFormat, alignment)
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
        Linux = Linker.Elf64.Elf64_Ehdr.EI_OSABI.Linux
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
        throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic($"CPU Architecture '{architecture}' not supported"));
    }
    
    private static ulong ArchitectureToAlignment(CPU_Architecture architecture)
    {
        switch (architecture)
        {
            case CPU_Architecture.AMD_x86_64:
                return 0x1000;
        }
        throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic($"CPU Architecture '{architecture}' not supported"));
    }
}
