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
        internal static class Unary
        {
            public static void DefaultInstruction(CodeGen codeGen, Expr.InlineAssembly.UnaryInstruction unary)
            {
                var op = unary.operand.ToOperand(codeGen, AssemblyExpr.Register.RegisterSize._32Bits);

                codeGen.Emit(new AssemblyExpr.Unary(unary.instruction, op));

                ReturnOperand(codeGen, unary, unary.operand, op);
                DeallocVariables(codeGen, (unary.operand, op));
            }
        }
    }
}
