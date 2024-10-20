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
        // Raw Instruction Byte Data. (1-15 bytes). Required
        // For instructions that do not match any existing pattern, and must be encoded as raw bits
        internal struct RawInstruction : IInstruction
        {
            byte[] _data;

            public RawInstruction(byte[] _data)
            {
                this._data = _data;
            }

            public byte[] GetBytes() => this._data;
        }
    }
}