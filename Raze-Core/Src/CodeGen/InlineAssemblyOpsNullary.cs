using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;


public abstract partial class CodeGen
{
    internal static partial class InlineAssemblyOps
    {
        internal static class Nullary
        {
            public static void DefaultInstruction(CodeGen codeGen, Expr.InlineAssembly.NullaryInstruction nullary)
            {
                codeGen.Emit(new AssemblyExpr.Nullary(nullary.instruction));
            }

            public static void SYSCALL(CodeGen codeGen, Expr.InlineAssembly.NullaryInstruction nullary)
            {
                codeGen.alloc.FreeRegister(codeGen.alloc.ReserveScratchRegister(codeGen, AssemblyExpr.Register.RegisterName.RCX, AssemblyExpr.Register.RegisterSize._64Bits));
                codeGen.alloc.FreeRegister(codeGen.alloc.ReserveScratchRegister(codeGen, AssemblyExpr.Register.RegisterName.R11, AssemblyExpr.Register.RegisterSize._64Bits));

                codeGen.Emit(new AssemblyExpr.Nullary(nullary.instruction));
            }
        }
    }
}
