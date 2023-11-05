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
        // Instruction Opcode For Immediate Operand (1 byte). Required
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct InstructionOpCodeImmediate : IInstruction
        {
            byte _data = 0;

            // 6 bit Instruction Opcode with Immediate operand (Non-Overriding _data)
            OpCode opCode
            {
                set => _data = (byte)((_data & 0x3) | (byte)value);
            }
            // 1 bit sign-extend mode. 0 = b is size of a, 1 = 8-bit b sign-extended to a
            bit x
            {
                set => _data = (byte)((_data & 0xFD) | (value << 1));
            }
            // 1 bit size. 0 = 8-bit, 1 = 16-bit/32-bit
            bit s
            {
                set => _data = (byte)((_data & 0xFE) | value);
            }

            // 8 bit OpCode for immediate
            internal enum OpCode : byte
            {
                ADD = 0x80,
                MOV = 0xC6
            }

            internal enum SignExtend : byte
            {
                // Immediate is same size as operand 1
                SameSize = 0,
                // 1 byte operand sign-extended
                _8BitSignExtended = 1
            }

            internal enum Size : byte
            {
                _8Bit = 0,
                _16Bit = 1,
                _32Bit = 1
            }

            public InstructionOpCodeImmediate(OpCode opCode, SignExtend x, Size s)
            {
                this._data = (byte)opCode;
                this.x = (byte)x;
                this.s = (byte)s;
            }

            public byte ToByte()
            {
                return _data;
            }
        }
    }
}
