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
        // Displacement (1, 2, or 4 bytes). Optional
        // NOT CURRENTLY SUPPORTED
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct Displacement8 : IInstruction
        {
            // 1 byte displacement
            byte _data;

            public byte ToByte()
            {
                throw new NotImplementedException();
            }
        }

        internal struct Displacement16 : IInstruction
        {
            // 2 byte displacement
            unsafe fixed byte _data[2];

            public byte ToByte()
            {
                throw new NotImplementedException();
            }
        }

        internal struct Displacement32 : IInstruction
        {
            // 4 byte displacement
            unsafe fixed byte _data[4];

            public byte ToByte()
            {
                throw new NotImplementedException();
            }
        }
    }
}
