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
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
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

        // Instruction Opcode Expansion Prefix (1 byte). Optional
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct InstructionOpCodeExpansionPrefix : IInstruction
        {
            byte _data = 0x0F;

            public InstructionOpCodeExpansionPrefix()
            {
            }

            public byte[] GetBytes() => new byte[] { this._data };
        }

        // Instruction Opcode Size Prefix (1 byte). Optional
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct InstructionOpCodeSizePrefix : IInstruction
        {
            byte _data = 0x66;

            public InstructionOpCodeSizePrefix() 
            {
            }

            public byte[] GetBytes() => new byte[] { this._data };
        }

        // Address Size Override Prefix (1 byte). Optional
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct AddressSizeOverridePrefix : IInstruction
        {
            byte _data = 0x67;

            public AddressSizeOverridePrefix()
            {
            }

            public byte[] GetBytes() => new byte[] { this._data };
        }
    }
}