using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

using static AssemblyExpr.Instruction;

public abstract partial class CodeGen
{
    internal static partial class InlineAssemblyOps
    {
        public static readonly Dictionary<AssemblyExpr.Instruction, (int, Action<CodeGen, Expr.InlineAssembly.BinaryInstruction>)> supportedInstructionsBinary = new()
        {
            { MOV, (1, Binary.DefaultInstruction) },
            { ADD, (1, Binary.DefaultInstruction) },
            { SUB, (1, Binary.DefaultInstruction) },
            { AND, (1, Binary.DefaultInstruction) },
            { OR, (1, Binary.DefaultInstruction) },
            { XOR, (1, Binary.DefaultInstruction) },
            { LEA, (1, Binary.DefaultInstruction) },
            { CMP, (0, Binary.DefaultInstruction) },
            { MOVZX, (1, Binary.DefaultInstruction) },
            { MOVSX, (1, Binary.MOVSX) },
            { IMUL, (1, Binary.IMUL) },
            { IDIV, (0, Binary.MUL_IDIV_DIV) },
            { DIV, (0, Binary.MUL_IDIV_DIV) },
            { MUL, (0, Binary.MUL_IDIV_DIV) },
            { SAL, (1, Binary.SAL_SAR) },
            { SAR, (1, Binary.SAL_SAR) },
            { ADDSS, (1, Binary.DefaultFloatingInstruction) },
            { ADDSD, (1, Binary.DefaultFloatingInstruction) },
            { CVTSD2SS, (1, Binary.DefaultFloatingInstruction) },
            { CVTTSD2SI, (1, Binary.DefaultFloatingInstruction) },
            { CVTSS2SD, (1, Binary.DefaultFloatingInstruction) },
            { CVTTSS2SI, (1, Binary.DefaultFloatingInstruction) },
            { CAST, (1, Binary.CAST) },
        };

        public static readonly Dictionary<AssemblyExpr.Instruction, (int, Action<CodeGen, Expr.InlineAssembly.UnaryInstruction>)> supportedInstructionsUnary = new()
        {
            { INC, (1, Unary.DefaultInstruction) },
            { NEG, (1, Unary.DefaultInstruction) },
            { SETE, (1, Unary.DefaultInstruction) },
            { SETNE, (1, Unary.DefaultInstruction) },
            { SETA, (1, Unary.DefaultInstruction) },
            { SETAE, (1, Unary.DefaultInstruction) },
            { SETB, (1, Unary.DefaultInstruction) },
            { SETBE, (1, Unary.DefaultInstruction) },
            { SETG, (1, Unary.DefaultInstruction) },
            { SETGE, (1, Unary.DefaultInstruction) },
            { SETL, (1, Unary.DefaultInstruction) },
            { SETLE, (1, Unary.DefaultInstruction) },
            { DEC, (1, Unary.DefaultInstruction) },
        };

        public static readonly Dictionary<AssemblyExpr.Instruction, Action<CodeGen, Expr.InlineAssembly.NullaryInstruction>> supportedInstructionsNullary = new()
        {
            { SYSCALL, Nullary.SYSCALL },
        };
    }
}
