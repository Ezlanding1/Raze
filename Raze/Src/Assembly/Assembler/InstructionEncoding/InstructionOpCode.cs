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
        // Instruction Opcode (1 byte). Required
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct InstructionOpCode : IInstruction
        {
            byte _data = 0;

            // 6 bit Instruction Opcode (Non-Overriding _data)
            OpCode opCode
            {
                set => _data = (byte)((_data & 0x3) | (byte)value);
            }
            // 7 bit Instruction Opcode (Non-Overriding _data)
            OpCode opCode7Bit
            {
                set => _data = (byte)((_data & 0x1) | (byte)value);
            }
            // 8 bit Instruction Opcode
            OpCode opCode8Bit
            {
                set => _data = (byte)value;
            }
            // 1 bit destination. 0 = OpCode a <- b, 1 = OpCode b <- a
            bit d
            {
                set => _data = (byte)((_data & 0xFD) | (value << 1));
            }
            // 1 bit size. 0 = 8-bit, 1 = 16-bit/32-bit
            bit s
            {
                set => _data = (byte)((_data & 0xFE) | value);
            }

            // 8 bit OpCode
            internal enum OpCode : byte
            {
                ADD = 0x0,
                MOV = 0x88,
                INC = 0xFF,
                SYSCALL = 0x05
            }

            internal enum Destination : byte
            {
                FirstIsSource = 0,
                FirstIsDestination = 1
            }

            internal enum Size : byte
            {
                _8Bit = 0,
                _16Bit = 1,
                _32Bit = 1
            }

            public InstructionOpCode(byte opCode)
            {
                this._data = opCode;
            }
            public InstructionOpCode(byte opCode, Destination d, Size s)
            {
                this._data = opCode;
                this.d = (byte)d;
                this.s = (byte)s;
            }
            public InstructionOpCode(OpCode opCode, Destination d, Size s)
            {
                this._data = (byte)opCode;
                this.d = (byte)d;
                this.s = (byte)s;
            }
            public InstructionOpCode(OpCode opCode, Size s)
            {
                this._data = (byte)opCode;
                this.s = (byte)s;
            }
            public InstructionOpCode(OpCode opCode)
            {
                this.opCode8Bit = opCode;
            }

            public byte[] GetBytes() => new byte[] { this._data };
        }
    }
}
