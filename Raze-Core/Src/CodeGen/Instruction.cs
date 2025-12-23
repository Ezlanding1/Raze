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
        MOVSXD,
        MOVZX,
        SAL,
        SAR,
        RET,
        POP,
        LEAVE,
        CWD,
        CDQ,
        CQO,
        CMOVNZ,
        MOVSS,
        ADDSS,
        CVTSS2SI,
        CVTTSS2SI,
        MOVD,
        MOVQ, 
        CBW,
        CWDE,
        CDQE,
        CVTSS2SD,
        MOVSD,
        ADDSD,
        CVTSD2SI,
        CVTTSD2SI,
        CVTSD2SS,

        // Custom Instructions
        CAST
    }
}