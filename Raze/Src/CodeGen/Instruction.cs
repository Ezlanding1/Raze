using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public abstract partial class AssemblyExpr
{
    public enum Instruction
    {
        // x86-64 Instructions
        ADD,
        SUB,
        MOV,
        CALL,
        PUSH,
        LEA,
        CMP,
        SETE,
        JMP,
        JE,
        JNE,
        TEST,
        SYSCALL,
        SETNE,
        SETG,
        SETL,
        SETGE,
        SETLE,
        SETA,
        SETAE,
        SETB,
        SETBE,
        JG,
        JL,
        JGE,
        JLE,
        JA,
        JAE,
        JB,
        JBE,
        INC,
        DEC,
        NEG,
        SHR,
        SHL,
        IDIV,
        DIV,
        IMUL,
        MUL,
        NOT,
        OR,
        AND,
        XOR,
        MOVSX,
        MOVZX,
        SAL,
        SAR,
        RET,
        POP,
        LEAVE,
        CWD,
        CDQ,
        CQO,

        // Custom Instructions
        IMOD,
        MOD,
        E_CMP,
        NE_CMP,
        G_CMP,
        GE_CMP,
        L_CMP,
        LE_CMP,
        UG_CMP,
        UGE_CMP,
        UL_CMP,
        ULE_CMP,
        DEREF
    }
}