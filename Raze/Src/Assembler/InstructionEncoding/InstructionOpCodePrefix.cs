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

            public RexPrefix(byte WRXB)
            {
                _data = fixedPrefix;
                this.WRXB = WRXB;
            }
        }

        // Instruction Opcode Expansion Prefix (1 byte). Optional
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct InstructionOpCodeExpansionPrefix : IInstruction
        {
            byte _data = 0x0F;

            public InstructionOpCodeExpansionPrefix()
            {     
            }
        }

        // Instruction Opcode Size Prefix (1 byte). Optional
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct InstructionOpCodeSizePrefix : IInstruction
        {
            byte _data = 0x66;

            public InstructionOpCodeSizePrefix() 
            {
            }
        }

        // Address Size Override Prefix (1 byte). Optional
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct AddressSizeOverridePrefix : IInstruction
        {
            byte _data = 0x67;

            public AddressSizeOverridePrefix()
            {
            }
        }
    }
}