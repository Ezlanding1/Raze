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

        internal static void ResolveReferences(Assembler assembler)
        {
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