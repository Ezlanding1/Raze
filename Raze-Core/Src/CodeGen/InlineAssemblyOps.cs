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
        private static void DeallocVariables(CodeGen codeGen, params (Expr.InlineAssembly.Operand operand, AssemblyExpr.IValue op)[] operands)
        {
            foreach (var (_, op) in operands.Where(x => x.operand.GetVariable() != null))
            {
                codeGen.alloc.Free(op);
            }
        }
        private static void ReturnOperand(CodeGen codeGen, Expr.InlineAssembly.Instruction instruction, Expr.InlineAssembly.Operand operand, AssemblyExpr.IValue op)
        {
            if (instruction._return && op != null)
            {
                Expr.InlineAssembly.Return.ReturnOperand(codeGen, op);
            }
        }

        private static AssemblyExpr.IValue ValidateOperand2(CodeGen codeGen, Expr.InlineAssembly.Operand operand1, ref AssemblyExpr.IValue op1, Expr.InlineAssembly.Operand operand2, AssemblyExpr.IValue op2)
        {
            if (op1.IsPointer(out var ptr1) && op2.IsPointer(out var ptr2))
            {
                if (op2.Size < AssemblyExpr.Register.RegisterSize._32Bits)
                {
                    AssemblyExpr.Register reg2 = ptr1.AsRegister(codeGen);
                    codeGen.Emit(PartialRegisterOptimize(operand1.Type(), reg2, op1));
                    op1 = reg2;

                    AssemblyExpr.Register op2Reg = ptr2.AsRegister(codeGen);
                    codeGen.Emit(PartialRegisterOptimize(operand2.Type(), op2Reg, op2));
                    op2 = op2Reg;
                }
                else
                {
                    op2 = op2.NonPointer(codeGen, operand2.Type());
                }
            }

            return op2;
        }
    }
}
