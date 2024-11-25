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
        public static List<(PE32Plus.IMAGE_BASE_RELOCATION, PE32Plus.IMAGE_BASE_RELOCATION_BLOCK[])> GenerateRelocationTables(Assembler assembler, SystemInfo systemInfo)
        {
            List<(PE32Plus.IMAGE_BASE_RELOCATION, PE32Plus.IMAGE_BASE_RELOCATION_BLOCK[])> relocationTables = [];

            ulong textLocation = PeUtils.GetTextSectionRVA(assembler, systemInfo);
            ulong dataLocation = PeUtils.GetDataSectionRVA(assembler, systemInfo);

            var relocateRefs = PeUtils.GetRefsWithAbsoluteAddressingGroupedByVirtualPage(assembler, systemInfo);

            foreach (var refInfoGroup in relocateRefs)
            {
                var blocks = new List<PE32Plus.IMAGE_BASE_RELOCATION_BLOCK>();

                foreach (var refInfo in refInfoGroup)
                {
                    ushort location = (ushort)((ulong)refInfo.location - refInfoGroup.Key + textLocation + (ulong)refInfo.size - (ulong)refInfo.dataSize);
                    blocks.Add(new(PE32Plus.IMAGE_BASE_RELOCATION_BLOCK.BaseRelocationType.IMAGE_REL_BASED_DIR64, location));
                }

                int size = Marshal.SizeOf<PE32Plus.IMAGE_BASE_RELOCATION>() + (Marshal.SizeOf<PE32Plus.IMAGE_BASE_RELOCATION_BLOCK>() * blocks.Count);
                int paddingNeeded = (size % 4) / Marshal.SizeOf<PE32Plus.IMAGE_BASE_RELOCATION_BLOCK>();
                for (int i = 0; i < paddingNeeded; i++)
                {
                    blocks.Add(new(PE32Plus.IMAGE_BASE_RELOCATION_BLOCK.BaseRelocationType.IMAGE_REL_BASED_ABSOLUTE, 0));
                }

                var baseReloc = new PE32Plus.IMAGE_BASE_RELOCATION((uint)refInfoGroup.Key, (uint)(size + paddingNeeded));
                relocationTables.Add(new(baseReloc, blocks.ToArray()));
            }

            return relocationTables;
        }
    }
}
