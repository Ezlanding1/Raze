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
        // Displacement. (1 or 4 bytes). Optional
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct Displacement8 : IInstruction
        {
            // 1 byte Displacement
            sbyte _data;

            public Displacement8(sbyte _data)
            {
                this._data = _data;
            }

            public byte[] GetBytes() => new byte[] { (byte)this._data };
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct Displacement32 : IInstruction
        {
            // 4 byte Displacement
            int _data;

            public Displacement32(int _data)
            {
                this._data = _data;
            }

            public byte[] GetBytes() => BitConverter.GetBytes(this._data);
        }
    }
}
