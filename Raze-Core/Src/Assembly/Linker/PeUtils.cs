using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Linker
{
    internal static class PeUtils
    {
        public static uint FileAlignment => PE32Plus.IMAGE_OPTIONAL_HEADER64.DefaultFileAlignment;
        public static uint VirtualAlignment(SystemInfo systemInfo) => checked((uint)systemInfo.alignment);

        // Function Suffixes:
        // Unaligned or Raw = no alignment
        // FileAligned or PhysicalAligned or Physical = output aligned to file alignment
        // RVA or VirtualAligned or Virtual = output aligned to virtual alignment

        // Assumes the binary always goes in the order: Text, Data, IData, Reloc

        public static int NumberOfSections(Assembler assembler)
        {
            return
                Convert.ToInt32(assembler.text.Count != 0) +
                Convert.ToInt32(assembler.data.Count != 0) +
                Convert.ToInt32(assembler.symbolTable.includes.Count != 0) +
                Convert.ToInt32(GetRefsWithAbsoluteAddressing(assembler.symbolTable).Any());
        }

        public static uint GetHeadersSizeRaw(Assembler assembler)
        {
            return
                (uint)(Marshal.SizeOf<PE32Plus.IMAGE_DOS_HEADER>() +
                Marshal.SizeOf<PE32Plus.MS_DOS_STUB>() +
                Marshal.SizeOf<PE32Plus.IMAGE_NT_HEADERS64>() +
                (Marshal.SizeOf<PE32Plus.IMAGE_SECTION_HEADER>() * NumberOfSections(assembler)));
        }

        public static uint GetTextSectionRVA(Assembler assembler, SystemInfo systemInfo)
        {
            return
                AlignTo(GetHeadersSizeRaw(assembler), VirtualAlignment(systemInfo));
        }

        public static uint GetDataSectionRVA(Assembler assembler, SystemInfo systemInfo)
        {
            return
                GetTextSectionRVA(assembler, systemInfo) +
                AlignTo((uint)assembler.text.Count, VirtualAlignment(systemInfo));
        }

        public static uint GetIDataSectionRVA(Assembler assembler, SystemInfo systemInfo)
        {
            return
                GetDataSectionRVA(assembler, systemInfo) +
                AlignTo((uint)assembler.data.Count, VirtualAlignment(systemInfo));
        }
        public static uint GetIDataSectionDataDirRVA(Assembler assembler, SystemInfo systemInfo) =>
            assembler.symbolTable.includes.Count == 0 ? 0 : GetIDataSectionRVA(assembler, systemInfo);

        public static uint GetIatLocationDataDirRVA(Assembler assembler, SystemInfo systemInfo) =>
            assembler.symbolTable.includes.Count == 0 ? 0 : GetIDataSectionRVA(assembler, systemInfo) + GetIDataSectionTablesLocations(assembler.symbolTable).iatTablesLocation;

        public static uint GetRelocSectionRVA(Assembler assembler, SystemInfo systemInfo)
        {
            return
                GetIDataSectionRVA(assembler, systemInfo) +
                AlignTo((uint)assembler.symbolTable.includes.Count, VirtualAlignment(systemInfo));
        }
        public static uint GetRelocSectionDataDirRVA(Assembler assembler, SystemInfo systemInfo) =>
            !GetRefsWithAbsoluteAddressing(assembler.symbolTable).Any() ? 0 : GetRelocSectionRVA(assembler, systemInfo);

        public static uint GetRelocSectionSize(Assembler assembler, SystemInfo systemInfo)
        {
            return (uint)GetRefsWithAbsoluteAddressingGroupedByVirtualPage(assembler, systemInfo)
                .Sum(x => AlignTo((uint)(Marshal.SizeOf<PE32Plus.IMAGE_BASE_RELOCATION>() + (x.Count() * Marshal.SizeOf<PE32Plus.IMAGE_BASE_RELOCATION_BLOCK>())), 4));
        }

        public static (uint iltTablesLocation, uint nameTablesLocation, uint iatTablesLocation) GetIDataSectionTablesLocations(SymbolTable symbolTable)
        {
            var groupedIncludes = GroupIncludesByDllName(symbolTable);
            uint iltTablesLocation = (uint)(Marshal.SizeOf<PE32Plus.IMAGE_IMPORT_DESCRIPTOR>() * (groupedIncludes.Count() + 1));
            uint nameTablesLocation = iltTablesLocation + (uint)(Marshal.SizeOf<PE32Plus.IMPORT_LOOKUP>() * (symbolTable.includes.Count + groupedIncludes.Count()));
            uint iatTablesLocation = nameTablesLocation + (uint)groupedIncludes.Sum(x => x.Key.Length + 1 + x.Sum(x => Marshal.SizeOf<PE32Plus.IMAGE_IMPORT_BY_NAME>() + x.importedFunctionName.Length + 1));

            return (iltTablesLocation, nameTablesLocation, iatTablesLocation);
        }

        public static IEnumerable<IGrouping<string, AssemblyExpr.Include>> GroupIncludesByDllName(SymbolTable symbolTable) =>
            symbolTable.includes
                .Where(x => x.includeType == AssemblyExpr.Include.IncludeType.DynamicLinkLibrary)
                .GroupBy(x => x.importedFileName);

        public static uint IatEntrySize() => 0x8;

        public static uint GetIDataSize(SymbolTable symbolTable)
        {
            if (symbolTable.includes.Count == 0) return 0;

            var tablesLocations = GetIDataSectionTablesLocations(symbolTable);
            return tablesLocations.iatTablesLocation + tablesLocations.iltTablesLocation;
        }

        public static uint SizeOfImageImportTable(SymbolTable symbolTable) =>
            (uint)(Marshal.SizeOf<PE32Plus.IMAGE_IMPORT_DESCRIPTOR>() * (GroupIncludesByDllName(symbolTable).Count() + 1));

        public static IEnumerable<ReferenceInfo> GetRefsWithAbsoluteAddressing(SymbolTable symbolTable)
        {
            return symbolTable.unresolvedReferences
                .Where(x => x.reference)
                .Cast<ReferenceInfo>()
                .Where(x => x.absoluteAddress);
        }
        
        public static IEnumerable<IGrouping<ulong, ReferenceInfo>> GetRefsWithAbsoluteAddressingGroupedByVirtualPage(Assembler assembler, SystemInfo systemInfo)
        {
            ulong textLocation = GetTextSectionRVA(assembler, systemInfo);
            ulong dataLocation = GetDataSectionRVA(assembler, systemInfo);
            ulong virtualAlignment = VirtualAlignment(systemInfo);

            return GetRefsWithAbsoluteAddressing(assembler.symbolTable)
                .GroupBy(x =>
                {
                    ulong baseLocation = x.textSection ? textLocation : dataLocation;
                    return baseLocation + (((ulong)x.location / virtualAlignment) * virtualAlignment);
                });
        }

        public static uint AlignTo(uint value, uint align)
        {
            uint result;

            uint remainder = value % align;
            result = remainder == 0 ?
                value :
                value + align - remainder;

            return result;
        }
    }
}
