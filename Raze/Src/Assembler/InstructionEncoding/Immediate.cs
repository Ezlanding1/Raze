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
        // NOT CURRENTLY SUPPORTED
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct Immediate8 : IInstruction
        {
            // 1 byte immediate
            byte _data;

            public byte ToByte()
            {
                throw new NotImplementedException();
            }
        }

        internal struct Immediate16 : IInstruction
        {
            // 2 byte immediate
            unsafe fixed byte _data[2];

            public byte ToByte()
            {
                throw new NotImplementedException();
            }
        }

        internal struct Immediate32 : IInstruction
        {
            // 4 byte immediate
            unsafe fixed byte _data[4];

            public byte ToByte()
            {
                throw new NotImplementedException();
            }
        }
    }
}
