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
                MOV = 0x0,
                INC = 0x0
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

            public ModRegRm(Mod MOD, OpCodeExtension REG, RegisterCode RM)
            {
                this.MOD = (byte)MOD;
                this.REG = (byte)REG;
                this.RM = (byte)RM;
            }

            public byte ToByte()
            {
                return _data;
            }
        }
    }
}
