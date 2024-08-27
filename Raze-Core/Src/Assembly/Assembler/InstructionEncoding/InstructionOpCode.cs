using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using bit = System.Byte;

namespace Raze;

public partial class Assembler
{
    public partial struct Instruction
    {
        // Instruction Opcode (1 byte). Required
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct InstructionOpCode : IInstruction
        {
            byte[] _data;

            public InstructionOpCode(byte[] opCode)
            {
                this._data = opCode;
            }

            public byte[] GetBytes() => this._data;
        }
    }
}
