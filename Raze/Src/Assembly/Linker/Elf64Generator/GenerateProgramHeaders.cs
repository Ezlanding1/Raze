using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Linker
{
    internal static partial class Elf64Generator
    {
        public unsafe static Elf64.Elf64_Phdr[] GenerateProgramHeaders(Assembler assembler, SystemInfo systemInfo)
        {
            int textSegments = (assembler.text.Count / (int)systemInfo.alignment);
            int textRemainder = (assembler.text.Count % (int)systemInfo.alignment);

            int dataSegments = (assembler.text.Count / (int)systemInfo.alignment);
            int dataRemainder = (assembler.text.Count % (int)systemInfo.alignment);

            int headerCount = sizeof(Elf64.Elf64_Ehdr) +
                ((textSegments + Convert.ToInt32(textRemainder != 0)) * sizeof(Elf64.Elf64_Phdr)) +
                ((dataSegments + Convert.ToInt32(dataRemainder != 0)) * sizeof(Elf64.Elf64_Phdr));
            headerCount += ((headerCount / (int)systemInfo.alignment) + Convert.ToInt32((headerCount % (int)systemInfo.alignment) != 0)) * sizeof(Elf64.Elf64_Phdr);
            int headerSegments = (headerCount / (int)systemInfo.alignment);
            int headerRemainder = (headerCount % (int)systemInfo.alignment);

            List<Elf64.Elf64_Phdr> programHeaders = new List<Elf64.Elf64_Phdr>();

            GenerateProgramHeadersForSection(headerSegments, Elf64.Elf64_Phdr.P_flags.PF_R, Elf64.Elf64_Shdr.headerVirtualAddress, headerRemainder, programHeaders, systemInfo);
            GenerateProgramHeadersForSection(textSegments, Elf64.Elf64_Phdr.P_flags.PF_R | Elf64.Elf64_Phdr.P_flags.PF_X, Elf64.Elf64_Shdr.textVirtualAddress, textRemainder, programHeaders, systemInfo);
            GenerateProgramHeadersForSection(dataSegments, Elf64.Elf64_Phdr.P_flags.PF_R | Elf64.Elf64_Phdr.P_flags.PF_W, Elf64.Elf64_Shdr.dataVirtualAddress, dataRemainder, programHeaders, systemInfo);

            return programHeaders.ToArray();
        }

        private static void GenerateProgramHeadersForSection(
            int sectionSegments, Elf64.Elf64_Phdr.P_flags sectionFlags, ulong sectionVirtualAddress, int sectionRemainder, 
            List<Elf64.Elf64_Phdr> programHeaders, SystemInfo systemInfo
            )
        {
            for (int i = 0; i < sectionSegments; i++)
            {
                programHeaders.Add(
                    new Elf64.Elf64_Phdr(
                        Elf64.Elf64_Phdr.P_type.PT_LOAD,
                        sectionFlags,
                        systemInfo.alignment * (ulong)programHeaders.Count,
                        (sectionVirtualAddress + (systemInfo.alignment * (ulong)i)),
                        0,
                        systemInfo.alignment,
                        systemInfo.alignment,
                        systemInfo.alignment
                    )
                );
            }
            if (sectionRemainder != 0)
            {
                programHeaders.Add(
                   new Elf64.Elf64_Phdr(
                       Elf64.Elf64_Phdr.P_type.PT_LOAD,
                       sectionFlags,
                       systemInfo.alignment * (ulong)programHeaders.Count,
                       sectionVirtualAddress + (systemInfo.alignment * (ulong)sectionSegments),
                       0,
                       (ulong)sectionRemainder,
                       (ulong)sectionRemainder,
                       systemInfo.alignment
                   )
               );
            }
        }
    }
}
