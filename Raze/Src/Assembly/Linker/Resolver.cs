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
                int sizeOffset = 0;

                for (int i = 0; i < assembler.symbolTable.unresolvedReferences.Count; i++)
                {
                    if (assembler.symbolTable.unresolvedReferences[i].reference)
                    {
                        ReferenceInfo reference = (ReferenceInfo)assembler.symbolTable.unresolvedReferences[i];
                        assembler.symbolTable.sTableUnresRefIdx = i;

                        reference.location += sizeOffset;

                        int oldSize = reference.size;
                        int localSizeOffset = ResolveLabelCalculateNewSize(assembler.text, reference, reference.instruction.Accept(assembler)) - oldSize;

                        if (localSizeOffset != 0)
                        {
                            stablePass = false;
                        }
                        sizeOffset += localSizeOffset;
                    }
                    else
                    {
                        DefinitionInfo defintion = (DefinitionInfo)assembler.symbolTable.unresolvedReferences[i];
                        assembler.symbolTable.definitions[defintion.refName] += sizeOffset;
                    }
                }

            }
            while (!stablePass);
        }
    }
}