using System.Runtime.InteropServices;
using System.Text;

namespace Raze;

public partial class Linker
{
    public void Link(FileStream fs, Assembler assembler, SystemInfo systemInfo)
    {
        Resolver.ResolveIncludes(assembler, systemInfo);
        Resolver.ResolveReferences(assembler, systemInfo);

        if (systemInfo.OutputElf())
        {
            OutputElf(fs, assembler, systemInfo);
        }
        else
        {
            OutputPe(fs, assembler, systemInfo);
        }
    }

    private static byte[] GetPaddingBytes(ulong segmentCount, ulong alignment)
    {
        var padding = segmentCount % alignment;

        if (padding == 0)
        {
            return new byte[0];
        }
        return new byte[alignment - padding];
    }
}
