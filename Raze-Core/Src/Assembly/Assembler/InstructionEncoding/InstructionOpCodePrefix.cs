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
        // REX Prefix (1 byte). Optional
        internal struct RexPrefix : IInstruction
        {
            private byte _data;

            internal const byte fixedPrefix = 0b0100 << 4;

            internal byte WRXB
            {
                set => _data = (byte)((_data & 0xF0) | value);
            }

            public bool W
            {
                set => _data = (byte)((_data & 0xF7) | Convert.ToByte(value) << 3);
            }
            public bool R
            {
                set => _data = (byte)((_data & 0xFB) | Convert.ToByte(value) << 2);
            }
            public bool X
            {
                set => _data = (byte)((_data & 0xFD) | Convert.ToByte(value) << 1);
            }
            public bool B
            {
                set => _data = (byte)((_data & 0xFE) | Convert.ToByte(value));
            }

            public RexPrefix(byte WRXB)
            {
                _data = fixedPrefix;
                this.WRXB = WRXB;
            }
            public RexPrefix(bool W, bool R, bool X, bool B)
            {
                _data = fixedPrefix;
                this.W = W;
                this.R = R;
                this.X = X;
                this.B = B;
            }

            public byte[] GetBytes() => new byte[] { this._data };
        }

        // Prefix (1 byte). Optional
        internal struct Prefix : IInstruction
        {
            byte _data;

            public Prefix(Encoder.Encoding.Prefix prefix)
            {
                _data = (byte)prefix;
            }

            public byte[] GetBytes() => new byte[] { this._data };
        }

    }
}
