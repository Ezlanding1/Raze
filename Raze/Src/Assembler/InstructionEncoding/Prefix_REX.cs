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
    internal partial struct Instruction
    {
        // REX Prefix Byte (1 byte). Optional
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct Prefix_REX : IInstruction
        {
            private byte _data;

            internal const bit fixedPrefix = 0b0100 << 4;

            internal bit WRXB
            {
                set => _data = (byte)((_data & 0xF0) | value);
            }

            public Prefix_REX(bit WRXB)
            {
                _data = fixedPrefix;
                this.WRXB = WRXB;
            }

            public byte ToByte()
            {
                return _data;
            }
        }
    }
}
