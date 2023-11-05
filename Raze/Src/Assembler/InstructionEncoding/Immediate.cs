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
        // Immediate Data. (1, 2, or 4 bytes). Optional
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct Immediate8 : IInstruction
        {
            // 1 byte immediate
            byte _data;

            public Immediate8(byte _data)
            {
                this._data = _data;
            }

            public byte ToByte()
            {
                return _data;
            }
        }

        internal struct Immediate16 : IInstruction
        {
            // 2 byte immediate
            unsafe fixed byte _data[2];

            public Immediate16(params byte[] _data) 
            {
                unsafe
                {
                    this._data[0] = _data[0];
                    this._data[1] = _data[1];
                }
            }

            public byte ToByte()
            {
                throw new NotImplementedException();
            }
        }

        internal struct Immediate32 : IInstruction
        {
            // 4 byte immediate
            unsafe fixed byte _data[4];

            public Immediate32(params byte[] _data)
            {
                unsafe
                {
                    this._data[0] = _data[0];
                    this._data[1] = _data[1];
                    this._data[2] = _data[2];
                    this._data[3] = _data[3];
                }
            }

            public byte ToByte()
            {
                throw new NotImplementedException();
            }
        }
    }
}
