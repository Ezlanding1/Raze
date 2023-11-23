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
        internal struct Immediate8 : IInstruction
        {
            // 1 byte immediate
            sbyte _data;

            public Immediate8(sbyte _data)
            {
                this._data = _data;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct Immediate16 : IInstruction
        {
            // 2 byte immediate
            short _data;

            public Immediate16(short _data) 
            {
                this._data = _data;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct Immediate32 : IInstruction
        {
            // 4 byte immediate
            int _data;

            public Immediate32(int _data)
            {
                this._data = _data;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct Immediate64 : IInstruction
        {
            // 8 byte immediate
            long _data;

            public Immediate64(long _data)
            {
                this._data = _data;
            }
        }
    }
}
