using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Assembler
{
    public partial struct Instruction
    {
        // Scaled Index Byte (1 byte). Optional
        // NOT CURRENTLY SUPPORTED
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct SIB : IInstruction
        {
            public byte _data;
        }
    }
}