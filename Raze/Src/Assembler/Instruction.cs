using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using bit = System.Byte;

namespace Raze;

public partial class Assembler
{
    internal interface IInstruction
    {
        public byte ToByte();
    }

    // Instruction (1-15 bytes)
    [StructLayout(LayoutKind.Sequential, Pack = 1)]

    internal struct Instruction
    {
        internal IInstruction[] Bytes { get; set; }
    }
    
    // REX Prefix Byte (1 byte). Optional
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct Prefix_REX : IInstruction
    {
        private byte _data;

        internal const bit fixedPrefix = 0b0100 << 4;

        internal bit WRXB
        {
            set => _data = (byte)((_data & 0xF) | (value << 4));
        }

        public Prefix_REX(bit WRXB)
        {
            _data = fixedPrefix;
            this.WRXB = WRXB;
        }

        public byte ToByte()
        {
            return _data;
        }
    }


    // Instruction Opcode (1 byte). Optional
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct InstructionOpCodeExpansionPrefix
    {
        const byte _data = 0xF;
    }

    // Instruction Opcode (1 byte). Required
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct InstructionOpCode : IInstruction
    {
        byte _data = 0;

        // 6 bit Instruction Opcode
        OpCode opCode
        {
            set => _data = (byte)((_data & 0x3) | ((byte)value << 2));
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

        internal enum OpCode : byte
        {
            ADD = 0x0,
            MOV = 0x88
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

        public InstructionOpCode(OpCode opCode, bit d, bit s)
        {
            this.opCode = opCode;
            this.d = d;
            this.s = s;
        }
        public InstructionOpCode(OpCode opCode, Destination d, Size s) : this(opCode, (byte)d, (byte)s)
        {
        }

        public byte ToByte()
        {
            return _data;
        }
    }

    // MOD_Reg_R/M byte (1 byte). Optional
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct ModRegRm : IInstruction
    {
        byte _data = 0;
        // 2-bit MOD
        bit MOD
        {
            set => _data = (byte)((_data & 0x3F) | (value << 6));
        }
        // 3-bit REG
        bit REG
        {
            set => _data = (byte)((_data & 0xC7) | (value << 3));
        }
        // 3-bit R/M
        bit RM
        {
            set => _data = (byte)((_data & 0xF8) | value);
        }

        internal enum Mod
        {
            RegisterIndirectAdressingMode = 0b00,
            SibNoDisplacement = 0b00,
            DisplacementAdressingMode = 0b00,
            OneByteDisplacement = 0b01,
            FourByteDisplacement = 0b10,
            RegisterAdressingMode = 0b11
        }

        internal enum RegisterCode
        {
            // al     | ax | eax
            AL = 0b000, AX = 0b000, EAX = 0b000,
            // cl     | cd | ecx
            CL = 0b001, CD = 0b001, ECX = 0b001,
            // dl     | dx | edx
            DL = 0b010, DX = 0b010, EDX = 0b010,
            // bl     | bx | ebx
            BL = 0b011, BX = 0b011, EBX = 0b011,
            // ah/spl | sp | esp
            AP = 0b100, SPL = 0b100, SP = 0b100, ESP = 0b100,
            // ch/bpl | bp | ebp
            CH = 0b101, BPL = 0b101, BP = 0b101, EBP = 0b101,
            // dh/sil | si | esi
            DH = 0b110, SIL = 0b110, SI = 0b110, ESI = 0b110,
            // bh/dil | di | edi
            BH = 0b111, DIL = 0b111, DI = 0b111, EDI = 0b111
        };

        internal enum OpCodeExtension
        {
            ADD = 0x0,
            MOV = 0x0
        }

        public ModRegRm(Mod MOD, RegisterCode REG, bit RM)
        {
            this.MOD = (byte)MOD;
            this.REG = (byte)REG;
            this.RM = RM;
        }
        public ModRegRm(Mod MOD, RegisterCode REG, RegisterCode RM)
        {
            this.MOD = (byte)MOD;
            this.REG = (byte)REG;
            this.RM = (byte)RM;
        }
        
        public ModRegRm(Mod MOD, OpCodeExtension REG, bit RM)
        {
            this.MOD = (byte)MOD;
            this.REG = (byte)REG;
            this.RM = RM;
        }

        public byte ToByte()
        {
            return _data;
        }
    }

    // Scaled Index Byte (1 byte). Optional
    // NOT CURRENTLY SUPPORTED
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct SIB : IInstruction
    {
        public byte _data;
        
        public byte ToByte()
        {
            throw new NotImplementedException();
        }
    }

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
