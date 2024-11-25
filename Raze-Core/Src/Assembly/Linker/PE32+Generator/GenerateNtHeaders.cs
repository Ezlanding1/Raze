using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Linker
{
    internal static partial class PE32PlusGenerator
    {
        internal static PE32Plus.IMAGE_NT_HEADERS64 GenerateNtHeaders(Assembler assembler, SystemInfo systemInfo, out uint firstSectionAddressUnaligned)
        {
            var machine = systemInfo.architecture switch
            {
                SystemInfo.CPU_Architecture.AMD_x86_64 => PE32Plus.IMAGE_FILE_HEADER._Machine.IMAGE_FILE_MACHINE_AMD64,
                _ => throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic($"Unsupported PE32+ systemInfo architecture '{systemInfo.architecture}'"))
            };

            ushort numberOfSections = checked((ushort)PeUtils.NumberOfSections(assembler));

            uint fileAlignment = PeUtils.FileAlignment;
            uint virtualAlignment = PeUtils.VirtualAlignment(systemInfo);

            uint codeSize = PeUtils.AlignTo(checked((uint)assembler.text.Count), fileAlignment);
            uint initializedDataSize = PeUtils.AlignTo(checked((uint)(assembler.data.Count + PeUtils.GetIDataSize(assembler.symbolTable))), fileAlignment);


            uint sizeOfHeadersRaw = PeUtils.GetHeadersSizeRaw(assembler);

            firstSectionAddressUnaligned = sizeOfHeadersRaw;

            // Text section is always the first section, right after the headers
            uint textSectionRVA = PeUtils.AlignTo(
                sizeOfHeadersRaw,
                virtualAlignment
            );

            const int SizeOfStackReserve = 0x100000;
            const int SizeOfStackCommit = 0x1000;
            const int SizeOfHeapReserve = 0x100000;
            const int SizeOfHeapCommit = 0x1000;

            return
                new PE32Plus.IMAGE_NT_HEADERS64(
                    new PE32Plus.IMAGE_FILE_HEADER(
                        machine,
                        numberOfSections,
                        checked((uint)((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds()),
                        checked((ushort)Marshal.SizeOf<PE32Plus.IMAGE_OPTIONAL_HEADER64>()),
                        PE32Plus.IMAGE_FILE_HEADER._Characteristics.IMAGE_FILE_EXECUTABLE_IMAGE | PE32Plus.IMAGE_FILE_HEADER._Characteristics.IMAGE_FILE_LARGE_ADDRESS_AWARE
                    ),
                    new PE32Plus.IMAGE_OPTIONAL_HEADER64(
                        codeSize,
                        initializedDataSize,
                        0,
                        textSectionRVA + checked((uint)assembler.symbolTable.definitions["text._start"]),
                        textSectionRVA,
                        PE32Plus.IMAGE_OPTIONAL_HEADER64.DefaultImageBase,
                        checked((uint)systemInfo.alignment),
                        fileAlignment,
                        0x6,
                        0,
                        0,
                        0,
                        0x6,
                        0,
                        checked((uint)((numberOfSections + 1) * systemInfo.alignment)),
                        PeUtils.AlignTo(sizeOfHeadersRaw, fileAlignment),
                        0,
                        PE32Plus.IMAGE_OPTIONAL_HEADER64._Subsystem.IMAGE_SUBSYSTEM_WINDOWS_CUI,
                        PE32Plus.IMAGE_OPTIONAL_HEADER64._DllCharacteristics.IMAGE_DLLCHARACTERISTICS_HIGH_ENTROPY_VA | PE32Plus.IMAGE_OPTIONAL_HEADER64._DllCharacteristics.MAGE_DLLCHARACTERISTICS_DYNAMIC_BASE | PE32Plus.IMAGE_OPTIONAL_HEADER64._DllCharacteristics.IMAGE_DLLCHARACTERISTICS_NX_COMPAT | PE32Plus.IMAGE_OPTIONAL_HEADER64._DllCharacteristics.IMAGE_DLLCHARACTERISTICS_TERMINAL_SERVER_AWARE,
                        SizeOfStackReserve,
                        SizeOfStackCommit,
                        SizeOfHeapReserve, 
                        SizeOfHeapCommit,
                        new PE32Plus.IMAGE_DATA_DIRECTORY[]
                        {
                            // Export Directory
                            new(0, 0),
                            // Import Directory
                            new(PeUtils.GetIDataSectionDataDirRVA(assembler, systemInfo), PeUtils.SizeOfImageImportTable(assembler.symbolTable)),
                            // Resource Directory
                            new(0, 0),
                            // Exception Directory
                            new(0, 0),
                            // Security Directory
                            new(0, 0),
                            // Base Relocation Table
                            new(PeUtils.GetRelocSectionDataDirRVA(assembler, systemInfo), PeUtils.GetRelocSectionSize(assembler, systemInfo)),
                            // Debug Directory
                            new(0, 0),
                            // Architecture Specific Data
                            new(0, 0),
                            // RVA of GP
                            new(0, 0),
                            // TLS Directory
                            new(0, 0), 
                            // Load Configuration Directory
                            new(0, 0), 
                            // Bound Import Directory in headers
                            new(0, 0), 
                            // Import Address Table
                            new(PeUtils.GetIatLocationDataDirRVA(assembler, systemInfo), PeUtils.GetIDataSectionTablesLocations(assembler.symbolTable).iltTablesLocation),
                            // Delay Load Import Descriptors
                            new(0, 0), 
                            // COM Runtime descriptor
                            new(0, 0),
                            // Null Directory
                            new(0, 0)
                        }
                    )
                );
        }
    }
}
