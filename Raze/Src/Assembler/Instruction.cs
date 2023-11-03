using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Assembler
{
    internal interface IInstruction
    {
        public byte ToByte();
    }

    // Instruction (1-15 bytes)
    [StructLayout(LayoutKind.Sequential, Pack = 1)]

    internal partial struct Instruction
    {
        internal IInstruction[] Bytes { get; set; }
    }
}
