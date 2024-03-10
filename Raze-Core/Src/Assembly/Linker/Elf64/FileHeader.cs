using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Elf64_Addr = System.UInt64;
using Elf64_Off = System.UInt64;
using Elf64_Half = System.UInt16;
using Elf64_Word = System.UInt32;
using Elf64_Sword = System.Int32;
using Elf64_Xword = System.UInt64;
using Elf64_Sxword = System.Int64;
using unsigned_char = System.Byte;

namespace Raze;

public partial class Linker
{
    internal partial class Elf64
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct Elf64_Ehdr
        {
            unsafe fixed unsigned_char e_ident[16]; /* ELF identification */
            Elf64_Half e_type; /* Object file type */
            Elf64_Half e_machine; /* Machine type */
            Elf64_Word e_version; /* Object file version */
            Elf64_Addr e_entry; /* Entry point address */
            Elf64_Off e_phoff; /* Program header offset */
            Elf64_Off e_shoff; /* Section header offset */
            Elf64_Word e_flags; /* Processor-specific flags */
            Elf64_Half e_ehsize; /* ELF header size */
            Elf64_Half e_phentsize; /* Size of program header entry */
            Elf64_Half e_phnum; /* Number of program header entries */
            Elf64_Half e_shentsize; /* Size of section header entry */
            Elf64_Half e_shnum; /* Number of section header entries */
            Elf64_Half e_shstrndx; /* Section name string table index */

            public Elf64_Ehdr(
                EI_CLASS ei_class, EI_DATA ei_data, EI_OSABI ei_osabi, E_type e_type, E_machine e_machine,
                Elf64_Addr e_entry, E_phoff e_phoff, Elf64_Addr e_shoff, E_flags e_flags, E_ehsize e_ehsize,
                E_phentsize e_phentsize, Elf64_Half e_phnum, E_shentsize e_shentsize, Elf64_Half e_shnum,
                Elf64_Half e_shstrndx
                )
            {
                unsafe
                {
                    // e_ident[EI_MAG0] - e_ident[EI_MAG3]
                    e_ident[0] = 0x7f;
                    e_ident[1] = (byte)'E';
                    e_ident[2] = (byte)'L';
                    e_ident[3] = (byte)'F';
                    // e_ident[EI_CLASS]
                    e_ident[4] = (byte)ei_class;
                    // e_ident[EI_DATA]
                    e_ident[5] = (byte)ei_data;
                    // e_ident[EI_VERSION]
                    e_ident[6] = 0x1;
                    // e_ident[EI_OSABI]
                    e_ident[7] = (byte)ei_osabi;
                    // e_ident[EI_ABIVERSION]
                    e_ident[8] = 0;
                    // e_ident[EI_PAD]
                    e_ident[9] = 0;
                    e_ident[10] = 0;
                    e_ident[11] = 0;
                    e_ident[12] = 0;
                    e_ident[13] = 0;
                    e_ident[14] = 0;
                    e_ident[15] = 0;
                }
                this.e_type = (Elf64_Half)e_type;
                this.e_machine = (Elf64_Half)e_machine;
                e_version = 1;
                this.e_entry = e_entry; 
                this.e_phoff = (Elf64_Addr)e_phoff;
                this.e_shoff = e_shoff;
                this.e_flags = (Elf64_Word)e_flags;
                this.e_ehsize = (Elf64_Half)e_ehsize;
                this.e_phentsize = (Elf64_Half)e_phentsize;
                this.e_phnum = e_phnum;
                this.e_shentsize = (Elf64_Half)e_shentsize;
                this.e_shnum = e_shnum;
                this.e_shstrndx = e_shstrndx;
            }

            public enum EI_CLASS : byte
            {
                _32BitFormat = 1,
                _64BitFormat = 2
            }

            public enum EI_DATA : byte
            {
                LittleEndian = 1,
                BigEndian = 2
            }

            public enum EI_OSABI : byte
            {
                System_V = 0x00,
                HP_UX = 0x01,
                NetBSD = 0x02,
                Linux = 0x03,
                GNU_Hurd = 0x04,
                Solaris = 0x06,
                AIX_Monterey = 0x07,
                IRIX = 0x08,
                FreeBSD = 0x09,
                Tru64 = 0x0A,
                Novell_Modesto = 0x0B,
                OpenBSD = 0x0C,
                OpenVMS = 0x0D,
                NonStop_Kernel = 0x0E,
                AROS = 0x0F,
                FenixOS = 0x10,
                Nuxi_CloudABI = 0x11,
                Stratus_Technologies_OpenVOS = 0x12
            }

            public enum E_type : Elf64_Half
            {
                ET_NONE = 0x00,
                ET_REL = 0x01,
                ET_EXEC = 0x02,
                ET_DYN = 0x03,
                ET_CORE = 0x04,
                ET_LOOS = 0xFE00,
                ET_HIOS = 0xFEFF,
                ET_LOPROC = 0xFF00,
                ET_HIPROC = 0xFFF
            }

            public enum E_machine
            {
                NoSpecificInstructionSet = 0x00,
                ATT_WE_32100 = 0x01,
                SPARC = 0x02,
                x86 = 0x03,
                Motorola_68000_M68k = 0x04,
                Motorola_88000_M88k = 0x05,
                Intel_MCU = 0x06,
                Intel_80860 = 0x07,
                MIPS = 0x08,
                IBM_System_370 = 0x09,
                MIPS_RS3000_Little_endian = 0x0A,
                // Reserved for future use = 0x0B - 0x0E
                Hewlett_Packard_PA_RISC = 0x0F,
                Intel_80960 = 0x13,
                PowerPC = 0x14,
                PowerPC_64_bit = 0x15,
                S390_including_S390x = 0x16,
                IBM_SPU_SPC = 0x17,
                // Reserved for future use = 0x18 - 0x23
                NEC_V800 = 0x24,
                Fujitsu_FR20 = 0x25,
                TRW_RH_32 = 0x26,
                Motorola_RCE = 0x27,
                Arm_up_to_Armv7_AArch32 = 0x28,
                Digital_Alpha = 0x29,
                SuperH = 0x2A,
                SPARC_Version_9 = 0x2B,
                Siemens_TriCore_embedded_processor = 0x2C,
                Argonaut_RISC_Core = 0x2D,
                Hitachi_H8_300 = 0x2E,
                Hitachi_H8_300H = 0x2F,
                Hitachi_H8S = 0x30,
                Hitachi_H8_500 = 0x31,
                IA_64 = 0x32,
                Stanford_MIPS_X = 0x33,
                Motorola_ColdFire = 0x34,
                Motorola_M68HC12 = 0x35,
                Fujitsu_MMA_Multimedia_Accelerator = 0x36,
                Siemens_PCP = 0x37,
                Sony_nCPU_embedded_RISC_processor = 0x38,
                Denso_NDR1_microprocessor = 0x39,
                Motorola_Star_Core_processor = 0x3A,
                Toyota_ME16_processor = 0x3B,
                STMicroelectronics_ST100_processor = 0x3C,
                Advanced_Logic_Corp_TinyJ_embedded_processor_family = 0x3D,
                AMD_x86_64 = 0x3E,
                Sony_DSP_Processor = 0x3F,
                Digital_Equipment_Corp_PDP_10 = 0x40,
                Digital_Equipment_Corp_PDP_11 = 0x41,
                Siemens_FX66_microcontroller = 0x42,
                STMicroelectronics_ST9_8_16_bit_microcontroller = 0x43,
                STMicroelectronics_ST7_8_bit_microcontroller = 0x44,
                Motorola_MC68HC16_Microcontroller = 0x45,
                Motorola_MC68HC11_Microcontroller = 0x46,
                Motorola_MC68HC08_Microcontroller = 0x47,
                Motorola_MC68HC05_Microcontroller = 0x48,
                Silicon_Graphics_SVx = 0x49,
                STMicroelectronics_ST19_8_bit_microcontroller = 0x4A,
                Digital_VAX = 0x4B,
                Axis_Communications_32_bit_embedded_processor = 0x4C,
                Infineon_Technologies_32_bit_embedded_processor = 0x4D,
                Element_14_64_bit_DSP_Processor = 0x4E,
                LSI_Logic_16_bit_DSP_Processor = 0x4F,
                TMS320C6000_Family = 0x8C,
                MCST_Elbrus_e2k = 0xAF,
                Arm_64_bits_Armv8_AArch64 = 0xB7,
                Zilog_Z80 = 0xDC,
                RISC_V = 0xF3,
                Berkeley_Packet_Filter = 0xF7,
                WDC_65C816 = 0x101
            }

            internal enum E_phoff : Elf64_Addr
            {
                DefualtProgramHeaderTableLocation64 = E_ehsize.DefualtFileHeaderSize64
            }

            internal enum E_flags : Elf64_Word
            {
                EF_SPARC_EXT_MASK = 0xffff00,
                EF_SPARC_32PLUS = 0x000100,
                EF_SPARC_SUN_US1 = 0x000200,
                EF_SPARC_HAL_R1 = 0x000400,
                EF_SPARC_SUN_US3 = 0x000800,
                EF_SPARCV9_MM = 0x3,
                EF_SPARCV9_TSO = 0x0,
                EF_SPARCV9_PSO = 0x1,
                EF_SPARCV9_RMO = 0x2
            }

            internal enum E_ehsize : Elf64_Half
            {
                DefualtFileHeaderSize64 = 0x40
            }
            
            internal enum E_phentsize : Elf64_Half
            {
                DefualtProgramHeaderTableEntrySize64 = 0x38
            }
            
            internal enum E_shentsize : Elf64_Half
            {
                DefualtProgramSectionTableEntrySize64 = 0x40
            }
        }
    }
}
