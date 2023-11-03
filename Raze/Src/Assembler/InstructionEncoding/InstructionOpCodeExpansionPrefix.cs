using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Assembler
{
    internal partial struct Instruction
    {
        // Instruction Opcode (1 byte). Optional
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct InstructionOpCodeExpansionPrefix : IInstruction
        {
            const byte _data = 0xF;

            public byte ToByte()
            {
                return _data;
            }
        }
    }
}