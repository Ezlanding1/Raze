using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Linker
{
    internal static class Resolver
    {
        internal static void ResolveIncludes(Assembler assembler, SystemInfo systemInfo)
        {
            foreach (var include in assembler.symbolTable.includes)
            {
                include.ValidateImportPlatform(systemInfo.osabi);

                switch (include.includeType)
                {
                    case AssemblyExpr.Include.IncludeType.DynamicLinkLibrary:
                    {
                        uint idataOffsetFromText = PeUtils.GetIDataSectionRVA(assembler, systemInfo) - PeUtils.GetTextSectionRVA(assembler, systemInfo);
                        uint iatTablesLocation = PeUtils.GetIDataSectionTablesLocations(assembler.symbolTable).iatTablesLocation;
                        assembler.symbolTable.definitions.Add("text." + include.mangledName, (int)(idataOffsetFromText + iatTablesLocation));
                    }
                    break;
                }
            }
        }

        private static int ResolveLabelCalculateNewSize(List<byte> section, ReferenceInfo reference, Assembler.Instruction instruction)
        {
            section.RemoveRange(reference.location, reference.size);
            
            reference.size = 0;

            foreach (byte[] bytes in instruction.ToBytes())
            {
                section.InsertRange(reference.location + reference.size, bytes);
                reference.size += bytes.Length;
            }
            return reference.size;
        }

        internal static void ResolveReferences(Assembler assembler, SystemInfo systemInfo)
        {
            if (systemInfo.OutputElf())
            {
                assembler.textVirtualAddress = Elf64.Elf64_Shdr.textVirtualAddress;
                assembler.dataVirtualAddress = Elf64.Elf64_Shdr.dataVirtualAddress;
            }
            else
            {
                assembler.textVirtualAddress = PE32Plus.IMAGE_OPTIONAL_HEADER64.DefaultImageBase + PeUtils.GetTextSectionRVA(assembler, systemInfo);
                assembler.dataVirtualAddress = PE32Plus.IMAGE_OPTIONAL_HEADER64.DefaultImageBase + PeUtils.GetDataSectionRVA(assembler, systemInfo);
            }

            assembler.nonResolvingPass = false;
            bool stablePass;

            do
            {
                stablePass = true;
                
                (List<byte> section, int sizeOffset)[] sectionsInfo = 
                [
                    (assembler.data, 0),
                    (assembler.text, 0)
                ];

                for (int i = 0; i < assembler.symbolTable.unresolvedReferences.Count; i++)
                {
                    if (assembler.symbolTable.unresolvedReferences[i].reference)
                    {
                        ReferenceInfo reference = (ReferenceInfo)assembler.symbolTable.unresolvedReferences[i];
                        assembler.symbolTable.sTableUnresRefIdx = i;

                        reference.location += sectionsInfo[Convert.ToInt32(reference.textSection)].sizeOffset;

                        int oldSize = reference.size;

                        int localSizeOffset = ResolveLabelCalculateNewSize(sectionsInfo[Convert.ToInt32(reference.textSection)].section, reference, reference.instruction.Accept(assembler)) - oldSize;

                        if (localSizeOffset != 0)
                        {
                            stablePass = false;
                        }

                        sectionsInfo[Convert.ToInt32(reference.textSection)].sizeOffset += localSizeOffset;
                    }
                    else
                    {
                        DefinitionInfo defintion = (DefinitionInfo)assembler.symbolTable.unresolvedReferences[i];
                        assembler.symbolTable.definitions[defintion.refName] += sectionsInfo[Convert.ToInt32(defintion.textSection)].sizeOffset;
                    }
                }

            }
            while (!stablePass);
        }
    }
}