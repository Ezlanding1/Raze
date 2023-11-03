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
        // Instruction Opcode Expansion Prefix (1 byte). Optional
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct InstructionOpCodeExpansionPrefix : IInstruction
        {
            const byte _data = 0x0F;

            public byte ToByte()
            {
                return _data;
            }
        }

        // Instruction Opcode Size Prefix (1 byte). Optional
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct InstructionOpCodeSizePrefix : IInstruction
        {
            const byte _data = 0x66;

            public byte ToByte()
            {
                return _data;
            }
        }
    }
}