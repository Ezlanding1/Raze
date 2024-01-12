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
        private static int ResolveLabelCalculateNewSize(List<byte> section, LabelRefInfo reference, Assembler.Instruction instruction)
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

        internal static void ResolveProcedureRefs(List<byte> text, Assembler assembler)
        {
            assembler.nonResolvingPass = false;
            bool stablePass;

            do
            {
                stablePass = true;
                int sizeOffset = 0;

                for (int i = 0; i < assembler.symbolTable.unresolvedLabels.Count; i++)
                {
                    if (assembler.symbolTable.unresolvedLabels[i].lblRef == true)
                    {
                        LabelRefInfo lblRef = (LabelRefInfo)assembler.symbolTable.unresolvedLabels[i];

                        lblRef.location += sizeOffset;

                        int oldSize = lblRef.size;
                        int localSizeOffset = ResolveLabelCalculateNewSize(text, lblRef, lblRef.instruction.Accept(assembler)) - oldSize;

                        if (sizeOffset != 0)
                        {
                            stablePass = false;
                        }
                        sizeOffset += localSizeOffset;
                    }
                    else
                    {
                        LabelDefInfo lblDef = (LabelDefInfo)assembler.symbolTable.unresolvedLabels[i];
                        assembler.symbolTable.labels[lblDef.refName] += sizeOffset;
                    }
                }

            }
            while (!stablePass);
        }
    }
}