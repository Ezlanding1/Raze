﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public class InstructionUtils
{
    internal const AssemblyExpr.Register.RegisterSize SYS_SIZE = AssemblyExpr.Register.RegisterSize._64Bits;

    private readonly static Dictionary<AssemblyExpr.Instruction, (AssemblyExpr.Instruction, AssemblyExpr.Instruction)> Jumps = new()
    {
        { AssemblyExpr.Instruction.SETE, (AssemblyExpr.Instruction.JE, AssemblyExpr.Instruction.JNE) },
        { AssemblyExpr.Instruction.SETNE, (AssemblyExpr.Instruction.JNE, AssemblyExpr.Instruction.JE) },
        { AssemblyExpr.Instruction.SETG, (AssemblyExpr.Instruction.JG, AssemblyExpr.Instruction.JLE) },
        { AssemblyExpr.Instruction.SETL, (AssemblyExpr.Instruction.JL, AssemblyExpr.Instruction.JGE) },
        { AssemblyExpr.Instruction.SETGE, (AssemblyExpr.Instruction.JGE, AssemblyExpr.Instruction.JL) },
        { AssemblyExpr.Instruction.SETLE, (AssemblyExpr.Instruction.JLE, AssemblyExpr.Instruction.JG) },
        { AssemblyExpr.Instruction.SETA, (AssemblyExpr.Instruction.JA, AssemblyExpr.Instruction.JBE) },
        { AssemblyExpr.Instruction.SETB, (AssemblyExpr.Instruction.JB, AssemblyExpr.Instruction.JAE) },
        { AssemblyExpr.Instruction.SETAE, (AssemblyExpr.Instruction.JAE, AssemblyExpr.Instruction.JB) },
        { AssemblyExpr.Instruction.SETBE, (AssemblyExpr.Instruction.JBE, AssemblyExpr.Instruction.JA) },
    };
    internal static AssemblyExpr.Instruction ConditionalJump(AssemblyExpr.Instruction instruction) => Jumps[instruction].Item1;
    internal static AssemblyExpr.Instruction ConditionalJumpReversed(AssemblyExpr.Instruction instruction) => Jumps[instruction].Item2;

    internal readonly static AssemblyExpr.Register.RegisterName[] paramRegister = new AssemblyExpr.Register.RegisterName[]
    {
        AssemblyExpr.Register.RegisterName.RDI,
        AssemblyExpr.Register.RegisterName.RSI,
        AssemblyExpr.Register.RegisterName.RDX,
        AssemblyExpr.Register.RegisterName.RCX,
        AssemblyExpr.Register.RegisterName.R8,
        AssemblyExpr.Register.RegisterName.R9
    };

    internal readonly static string[] ReturnOverload = new string[]
    {
        "r15",
        "r14",
        "r13",
        "r12",
        "rbx"
    };

    internal readonly static (AssemblyExpr.Register.RegisterName Name, bool IsScratchRegister)[] storageRegisters = new (AssemblyExpr.Register.RegisterName, bool)[]
    {
        (AssemblyExpr.Register.RegisterName.RAX, true),
        (AssemblyExpr.Register.RegisterName.RCX, true),
        (AssemblyExpr.Register.RegisterName.RDX, true),
        (AssemblyExpr.Register.RegisterName.RSI, true),
        (AssemblyExpr.Register.RegisterName.RDI, true),
        (AssemblyExpr.Register.RegisterName.R8, true),
        (AssemblyExpr.Register.RegisterName.R9, true),
        (AssemblyExpr.Register.RegisterName.R10, true),
        (AssemblyExpr.Register.RegisterName.R11, true),
        (AssemblyExpr.Register.RegisterName.RBX, false),
        (AssemblyExpr.Register.RegisterName.R12, false),
        (AssemblyExpr.Register.RegisterName.R13, false),
        (AssemblyExpr.Register.RegisterName.R14, false),
        (AssemblyExpr.Register.RegisterName.R15, false),


        (AssemblyExpr.Register.RegisterName.XMM0, true),
        (AssemblyExpr.Register.RegisterName.XMM1, true),
        (AssemblyExpr.Register.RegisterName.XMM2, true),
        (AssemblyExpr.Register.RegisterName.XMM3, true),
        (AssemblyExpr.Register.RegisterName.XMM4, true),
        (AssemblyExpr.Register.RegisterName.XMM5, true),
        (AssemblyExpr.Register.RegisterName.XMM6, true),
        (AssemblyExpr.Register.RegisterName.XMM7, true),
        (AssemblyExpr.Register.RegisterName.XMM8, true),
        (AssemblyExpr.Register.RegisterName.XMM9, true),
        (AssemblyExpr.Register.RegisterName.XMM10, true),
        (AssemblyExpr.Register.RegisterName.XMM11, true),
        (AssemblyExpr.Register.RegisterName.XMM12, true),
        (AssemblyExpr.Register.RegisterName.XMM13, true),
        (AssemblyExpr.Register.RegisterName.XMM14, true),
        (AssemblyExpr.Register.RegisterName.XMM15, true),
    };
    internal readonly static int ScratchRegisterCount = storageRegisters.Where(x => x.IsScratchRegister).Count();
    internal readonly static int NonSseScratchRegisterCount = storageRegisters.Where(x => x.Name < AssemblyExpr.Register.RegisterName.XMM0 && x.IsScratchRegister).Count();
    internal readonly static int SseRegisterOffset = storageRegisters.ToList().FindIndex(x => x.Name >= AssemblyExpr.Register.RegisterName.XMM0);


    internal readonly static Dictionary<string, (AssemblyExpr.Register.RegisterName, AssemblyExpr.Register.RegisterSize)> Registers = new()
    {
        { "RAX", (AssemblyExpr.Register.RegisterName.RAX, AssemblyExpr.Register.RegisterSize._64Bits) }, // 64-Bits 
        { "EAX", (AssemblyExpr.Register.RegisterName.RAX, AssemblyExpr.Register.RegisterSize._32Bits) }, // Lower 32-Bits
        { "AX", (AssemblyExpr.Register.RegisterName.RAX, AssemblyExpr.Register.RegisterSize._16Bits) }, // Lower 16-Bits
        { "AH", (AssemblyExpr.Register.RegisterName.RAX, AssemblyExpr.Register.RegisterSize._8BitsUpper) }, // Upper 16-Bits
        { "AL", (AssemblyExpr.Register.RegisterName.RAX, AssemblyExpr.Register.RegisterSize._8Bits) }, // Lower 8-Bits

        { "RCX", (AssemblyExpr.Register.RegisterName.RCX, AssemblyExpr.Register.RegisterSize._64Bits) },
        { "ECX", (AssemblyExpr.Register.RegisterName.RCX, AssemblyExpr.Register.RegisterSize._32Bits) },
        { "CX", (AssemblyExpr.Register.RegisterName.RCX, AssemblyExpr.Register.RegisterSize._16Bits) },
        { "CH", (AssemblyExpr.Register.RegisterName.RCX, AssemblyExpr.Register.RegisterSize._8BitsUpper) },
        { "CL", (AssemblyExpr.Register.RegisterName.RCX, AssemblyExpr.Register.RegisterSize._8Bits) },

        { "RDX", (AssemblyExpr.Register.RegisterName.RDX, AssemblyExpr.Register.RegisterSize._64Bits) },
        { "EDX", (AssemblyExpr.Register.RegisterName.RDX, AssemblyExpr.Register.RegisterSize._32Bits) },
        { "DX", (AssemblyExpr.Register.RegisterName.RDX, AssemblyExpr.Register.RegisterSize._16Bits) },
        { "DH", (AssemblyExpr.Register.RegisterName.RDX, AssemblyExpr.Register.RegisterSize._8BitsUpper) },
        { "DL", (AssemblyExpr.Register.RegisterName.RDX, AssemblyExpr.Register.RegisterSize._8Bits) },

        { "RBX", (AssemblyExpr.Register.RegisterName.RBX, AssemblyExpr.Register.RegisterSize._64Bits) },
        { "EBX", (AssemblyExpr.Register.RegisterName.RBX, AssemblyExpr.Register.RegisterSize._32Bits) },
        { "BX", (AssemblyExpr.Register.RegisterName.RBX, AssemblyExpr.Register.RegisterSize._16Bits) },
        { "BH", (AssemblyExpr.Register.RegisterName.RBX, AssemblyExpr.Register.RegisterSize._8BitsUpper) },
        { "BL", (AssemblyExpr.Register.RegisterName.RBX, AssemblyExpr.Register.RegisterSize._8Bits) },

        { "RSI", (AssemblyExpr.Register.RegisterName.RSI, AssemblyExpr.Register.RegisterSize._64Bits) },
        { "ESI", (AssemblyExpr.Register.RegisterName.RSI, AssemblyExpr.Register.RegisterSize._32Bits) },
        { "SI", (AssemblyExpr.Register.RegisterName.RSI, AssemblyExpr.Register.RegisterSize._16Bits) },
        { "SIL", (AssemblyExpr.Register.RegisterName.RSI, AssemblyExpr.Register.RegisterSize._8Bits) },

        { "RDI", (AssemblyExpr.Register.RegisterName.RDI, AssemblyExpr.Register.RegisterSize._64Bits) },
        { "EDI", (AssemblyExpr.Register.RegisterName.RDI, AssemblyExpr.Register.RegisterSize._32Bits) },
        { "DI", (AssemblyExpr.Register.RegisterName.RDI, AssemblyExpr.Register.RegisterSize._16Bits) },
        { "DIL", (AssemblyExpr.Register.RegisterName.RDI, AssemblyExpr.Register.RegisterSize._8Bits) },

        { "RSP", (AssemblyExpr.Register.RegisterName.RSP, AssemblyExpr.Register.RegisterSize._64Bits) },
        { "ESP", (AssemblyExpr.Register.RegisterName.RSP, AssemblyExpr.Register.RegisterSize._32Bits) },
        { "SP", (AssemblyExpr.Register.RegisterName.RSP, AssemblyExpr.Register.RegisterSize._16Bits) },
        { "SPL", (AssemblyExpr.Register.RegisterName.RSP, AssemblyExpr.Register.RegisterSize._8Bits) },

        { "RBP", (AssemblyExpr.Register.RegisterName.RBP, AssemblyExpr.Register.RegisterSize._64Bits) },
        { "EBP", (AssemblyExpr.Register.RegisterName.RBP, AssemblyExpr.Register.RegisterSize._32Bits) },
        { "BP", (AssemblyExpr.Register.RegisterName.RBP, AssemblyExpr.Register.RegisterSize._16Bits) },
        { "BPL", (AssemblyExpr.Register.RegisterName.RBP, AssemblyExpr.Register.RegisterSize._8Bits) },

        { "R8", (AssemblyExpr.Register.RegisterName.R8, AssemblyExpr.Register.RegisterSize._64Bits) },
        { "R8D", (AssemblyExpr.Register.RegisterName.R8, AssemblyExpr.Register.RegisterSize._32Bits) },
        { "R8W", (AssemblyExpr.Register.RegisterName.R8, AssemblyExpr.Register.RegisterSize._16Bits) },
        { "R8B", (AssemblyExpr.Register.RegisterName.R8, AssemblyExpr.Register.RegisterSize._8Bits) },

        { "R9", (AssemblyExpr.Register.RegisterName.R9, AssemblyExpr.Register.RegisterSize._64Bits) },
        { "R9D", (AssemblyExpr.Register.RegisterName.R9, AssemblyExpr.Register.RegisterSize._32Bits) },
        { "R9W", (AssemblyExpr.Register.RegisterName.R9, AssemblyExpr.Register.RegisterSize._16Bits) },
        { "R9B", (AssemblyExpr.Register.RegisterName.R9, AssemblyExpr.Register.RegisterSize._8Bits) },

        { "R10", (AssemblyExpr.Register.RegisterName.R10, AssemblyExpr.Register.RegisterSize._64Bits) },
        { "R10D", (AssemblyExpr.Register.RegisterName.R10, AssemblyExpr.Register.RegisterSize._32Bits) },
        { "R10W", (AssemblyExpr.Register.RegisterName.R10, AssemblyExpr.Register.RegisterSize._16Bits) },
        { "R10B", (AssemblyExpr.Register.RegisterName.R10, AssemblyExpr.Register.RegisterSize._8Bits) },

        { "R11", (AssemblyExpr.Register.RegisterName.R11, AssemblyExpr.Register.RegisterSize._64Bits) },
        { "R11D", (AssemblyExpr.Register.RegisterName.R11, AssemblyExpr.Register.RegisterSize._32Bits) },
        { "R11W", (AssemblyExpr.Register.RegisterName.R11, AssemblyExpr.Register.RegisterSize._16Bits) },
        { "R11B", (AssemblyExpr.Register.RegisterName.R11, AssemblyExpr.Register.RegisterSize._8Bits) },

        { "R12", (AssemblyExpr.Register.RegisterName.R12, AssemblyExpr.Register.RegisterSize._64Bits) },
        { "R12D", (AssemblyExpr.Register.RegisterName.R12, AssemblyExpr.Register.RegisterSize._32Bits) },
        { "R12W", (AssemblyExpr.Register.RegisterName.R12, AssemblyExpr.Register.RegisterSize._16Bits) },
        { "R12B", (AssemblyExpr.Register.RegisterName.R12, AssemblyExpr.Register.RegisterSize._8Bits) },

        { "R13", (AssemblyExpr.Register.RegisterName.R13, AssemblyExpr.Register.RegisterSize._64Bits) },
        { "R13D", (AssemblyExpr.Register.RegisterName.R13, AssemblyExpr.Register.RegisterSize._32Bits) },
        { "R13W", (AssemblyExpr.Register.RegisterName.R13, AssemblyExpr.Register.RegisterSize._16Bits) },
        { "R13B", (AssemblyExpr.Register.RegisterName.R13, AssemblyExpr.Register.RegisterSize._8Bits) },

        { "R14", (AssemblyExpr.Register.RegisterName.R14, AssemblyExpr.Register.RegisterSize._64Bits) },
        { "R14D", (AssemblyExpr.Register.RegisterName.R14, AssemblyExpr.Register.RegisterSize._32Bits) },
        { "R14W", (AssemblyExpr.Register.RegisterName.R14, AssemblyExpr.Register.RegisterSize._16Bits) },
        { "R14B", (AssemblyExpr.Register.RegisterName.R14, AssemblyExpr.Register.RegisterSize._8Bits) },

        { "R15", (AssemblyExpr.Register.RegisterName.R15, AssemblyExpr.Register.RegisterSize._64Bits) },
        { "R15D", (AssemblyExpr.Register.RegisterName.R15, AssemblyExpr.Register.RegisterSize._32Bits) },
        { "R15W", (AssemblyExpr.Register.RegisterName.R15, AssemblyExpr.Register.RegisterSize._16Bits) },
        { "R15B", (AssemblyExpr.Register.RegisterName.R15, AssemblyExpr.Register.RegisterSize._8Bits) },

        { "XMM0", (AssemblyExpr.Register.RegisterName.XMM0, AssemblyExpr.Register.RegisterSize._128Bits) },
        { "XMM1", (AssemblyExpr.Register.RegisterName.XMM1, AssemblyExpr.Register.RegisterSize._128Bits) },
        { "XMM2", (AssemblyExpr.Register.RegisterName.XMM2, AssemblyExpr.Register.RegisterSize._128Bits) },
        { "XMM3", (AssemblyExpr.Register.RegisterName.XMM3, AssemblyExpr.Register.RegisterSize._128Bits) },
        { "XMM4", (AssemblyExpr.Register.RegisterName.XMM4, AssemblyExpr.Register.RegisterSize._128Bits) },
        { "XMM5", (AssemblyExpr.Register.RegisterName.XMM5, AssemblyExpr.Register.RegisterSize._128Bits) },
        { "XMM6", (AssemblyExpr.Register.RegisterName.XMM6, AssemblyExpr.Register.RegisterSize._128Bits) },
        { "XMM7", (AssemblyExpr.Register.RegisterName.XMM7, AssemblyExpr.Register.RegisterSize._128Bits) },
        { "XMM8", (AssemblyExpr.Register.RegisterName.XMM8, AssemblyExpr.Register.RegisterSize._128Bits) },
        { "XMM9", (AssemblyExpr.Register.RegisterName.XMM9, AssemblyExpr.Register.RegisterSize._128Bits) },
        { "XMM10", (AssemblyExpr.Register.RegisterName.XMM10, AssemblyExpr.Register.RegisterSize._128Bits) },
        { "XMM11", (AssemblyExpr.Register.RegisterName.XMM11, AssemblyExpr.Register.RegisterSize._128Bits) },
        { "XMM12", (AssemblyExpr.Register.RegisterName.XMM12, AssemblyExpr.Register.RegisterSize._128Bits) },
        { "XMM13", (AssemblyExpr.Register.RegisterName.XMM13, AssemblyExpr.Register.RegisterSize._128Bits) },
        { "XMM14", (AssemblyExpr.Register.RegisterName.XMM14, AssemblyExpr.Register.RegisterSize._128Bits) },
        { "XMM15", (AssemblyExpr.Register.RegisterName.XMM15, AssemblyExpr.Register.RegisterSize._128Bits) },
    };

    internal static AssemblyExpr.Register.RegisterSize ToRegisterSize(int size)
    {
        if (Enum.IsDefined(typeof(AssemblyExpr.Register.RegisterSize), size))
        {
            return (AssemblyExpr.Register.RegisterSize)size;
        }

        throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic($"Invalid Register Size ({size})"));
    }
}
