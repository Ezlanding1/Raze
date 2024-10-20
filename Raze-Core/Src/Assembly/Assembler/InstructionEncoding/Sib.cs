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
        internal struct SIB : IInstruction
        {
            public byte _data;

            // 2-bit Scale
            byte SCALE
            {
                set => _data = (byte)((_data & 0x3F) | (value << 6));
            }
            // 3-bit Index
            byte INDEX
            {
                set => _data = (byte)((_data & 0xC7) | (value << 3));
            }
            // 3-bit Base
            byte BASE
            {
                set => _data = (byte)((_data & 0xF8) | value);
            }

            public enum Scale
            {
                TimesOne = 0b00,
                TimesTwo = 0b01,
                TimesFour = 0b10,
                TimesEight = 0b11
            }

            public enum Index
            {
                EAX = 0b000,
                ECX = 0b001,
                EDX = 0b010,
                EBX = 0b011,
                Illegal = 0b100,
                EBP = 0b101,
                ESI = 0b110,
                EDI = 0b111
            }

            public enum Base
            {
                EAX = 0b000,
                ECX = 0b001,
                EDX = 0b010,
                EBX = 0b011,
                ESP = 0b100,
                Special = 0b101,
                ESI = 0b110,
                EDI = 0b111
            }

            public SIB(Scale SCALE, Index INDEX, Base BASE)
            {
                this.SCALE = (byte)SCALE;
                this.INDEX = (byte)INDEX;
                this.BASE = (byte)BASE;
            }

            public byte[] GetBytes() => new byte[] { this._data };
        }
    }
}
