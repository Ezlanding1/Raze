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
        internal static PE32Plus.IMAGE_SECTION_HEADER[] GenerateSectionHeaders(Assembler assembler, SystemInfo systemInfo, uint firstSectionAddressUnaligned, out uint currentSectionRva)
        {
            List<PE32Plus.IMAGE_SECTION_HEADER> sectionHeaders = [];

            uint fileAlignment = PeUtils.FileAlignment;
            uint virtualAlignment = PeUtils.VirtualAlignment(systemInfo);

            uint currentSectionRaw = PeUtils.AlignTo(firstSectionAddressUnaligned, fileAlignment); // Current Raw-Aligned (FileAlignment) address (physical size)
            currentSectionRva = PeUtils.AlignTo(firstSectionAddressUnaligned, virtualAlignment); // Current Virtual-Aligned (VirtualAlignment) address (virtual size)

            GenerateSectionHeader(
                sectionHeaders,
                ['.', 't', 'e', 'x', 't'],
                (uint)assembler.text.Count,
                PE32Plus.IMAGE_SECTION_HEADER._Characteristics.IMAGE_SCN_MEM_READ | PE32Plus.IMAGE_SECTION_HEADER._Characteristics.IMAGE_SCN_MEM_EXECUTE,
                ref currentSectionRaw,
                ref currentSectionRva,
                fileAlignment,
                virtualAlignment
            );

            GenerateSectionHeader(
                sectionHeaders,
                ['.', 'd', 'a', 't', 'a'],
                (uint)assembler.data.Count,
                PE32Plus.IMAGE_SECTION_HEADER._Characteristics.IMAGE_SCN_MEM_READ | PE32Plus.IMAGE_SECTION_HEADER._Characteristics.IMAGE_SCN_MEM_WRITE,
                ref currentSectionRaw,
                ref currentSectionRva,
                fileAlignment,
                virtualAlignment
            );

            GenerateSectionHeader(
                sectionHeaders,
                ['.', 'i', 'd', 'a', 't', 'a'],
                PeUtils.GetIDataSize(assembler.symbolTable),
                PE32Plus.IMAGE_SECTION_HEADER._Characteristics.IMAGE_SCN_MEM_READ,
                ref currentSectionRaw,
                ref currentSectionRva,
                fileAlignment,
                virtualAlignment
            );

            GenerateSectionHeader(
                sectionHeaders,
                ['.', 'r', 'e', 'l', 'o', 'c'],
                PeUtils.GetRelocSectionSize(assembler, systemInfo),
                PE32Plus.IMAGE_SECTION_HEADER._Characteristics.IMAGE_SCN_MEM_READ,
                ref currentSectionRaw,
                ref currentSectionRva,
                fileAlignment,
                virtualAlignment
            );

            return sectionHeaders.ToArray();
        }

        private static void GenerateSectionHeader(List<PE32Plus.IMAGE_SECTION_HEADER> sectionHeaders, char[] name, uint virtualSize, PE32Plus.IMAGE_SECTION_HEADER._Characteristics characteristics, ref uint currentSectionRaw, ref uint currentSectionRva, uint fileAlignment, uint virtualAlignment)
        {
            if (virtualSize != 0)
            {
                sectionHeaders.Add(
                    new PE32Plus.IMAGE_SECTION_HEADER(
                        name,
                        virtualSize,
                        currentSectionRva, // Virtual Address
                        PeUtils.AlignTo(virtualSize, fileAlignment), // Raw Size
                        currentSectionRaw, // Raw Address
                        characteristics
                    )
                );

                currentSectionRaw = PeUtils.AlignTo(currentSectionRaw + virtualSize, fileAlignment);
                currentSectionRva = PeUtils.AlignTo(currentSectionRva + virtualSize, virtualAlignment);
            }
        }
    }
}
