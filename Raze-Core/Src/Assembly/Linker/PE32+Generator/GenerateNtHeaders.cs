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

            ushort numberOfSections = checked((ushort)(
                Convert.ToUInt32(assembler.text.Count != 0) +
                Convert.ToUInt32(assembler.data.Count != 0) +
                0
            ));

            var fileAlignment = PE32Plus.IMAGE_OPTIONAL_HEADER64.DefaultFileAlignment;
            uint virtualAlignment = checked((uint)systemInfo.alignment);

            uint codeSize = AlignTo(checked((uint)assembler.text.Count), fileAlignment);
            uint initializedDataSize = AlignTo(checked((uint)assembler.data.Count), fileAlignment);


            uint sizeOfHeadersRaw = (uint)(Marshal.SizeOf<PE32Plus.IMAGE_DOS_HEADER>() +
                Marshal.SizeOf<PE32Plus.MS_DOS_STUB>() +
                Marshal.SizeOf<PE32Plus.IMAGE_NT_HEADERS64>() +
                (Marshal.SizeOf<PE32Plus.IMAGE_SECTION_HEADER>() * numberOfSections));

            firstSectionAddressUnaligned = sizeOfHeadersRaw;

            // Text section is always the first section, right after the headers
            uint textSectionRVA = AlignTo(
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
                        AlignTo(sizeOfHeadersRaw, fileAlignment),
                        0,
                        PE32Plus.IMAGE_OPTIONAL_HEADER64._Subsystem.IMAGE_SUBSYSTEM_WINDOWS_CUI,
                        PE32Plus.IMAGE_OPTIONAL_HEADER64._DllCharacteristics.IMAGE_DLLCHARACTERISTICS_HIGH_ENTROPY_VA | PE32Plus.IMAGE_OPTIONAL_HEADER64._DllCharacteristics.MAGE_DLLCHARACTERISTICS_DYNAMIC_BASE | PE32Plus.IMAGE_OPTIONAL_HEADER64._DllCharacteristics.IMAGE_DLLCHARACTERISTICS_NX_COMPAT | PE32Plus.IMAGE_OPTIONAL_HEADER64._DllCharacteristics.IMAGE_DLLCHARACTERISTICS_TERMINAL_SERVER_AWARE,
                        SizeOfStackReserve,
                        SizeOfStackCommit,
                        SizeOfHeapReserve, 
                        SizeOfHeapCommit,
                        []
                    )
                );
        }

        private static uint AlignTo(uint value, uint align)
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
