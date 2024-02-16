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
        // Immediate Data. (1, 2, or 4 bytes). Optional
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct Immediate : IInstruction
        {
            byte[] _data;

            public Immediate(byte[] _data, Encoder.Operand.OperandSize size)
            {
                this._data = _data;
                Array.Resize(ref this._data, (int)size);
            }

            public byte[] GetBytes() => this._data;
        }
    }
}
