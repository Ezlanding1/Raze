using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class CodeGen : Expr.IVisitor<AssemblyExpr.IValue?>
{
    internal class ResolvedRegistersPass(ISection.Text asmExprs, Dictionary<AssemblyExpr.IValue, AssemblyExpr.Pointer?> stackSpill) :
        ResolvedCodeGenPass(asmExprs),
        AssemblyExpr.IVisitor<object?>,
        AssemblyExpr.IUnaryOperandVisitor<object?>
    {
        int fppIdx = 0;
        FunctionPushPreserved functionPushPreserved = new(0, 0);
        Dictionary<AssemblyExpr.IValue, AssemblyExpr.Pointer?> stackSpill = stackSpill;

        public override void Run()
        {
            for (; idx < AssemblyExprsCount(); idx++)
            {
                GetAssemblyExpr(idx).Accept(this);
            }
            functionPushPreserved.GenerateHeader(this);
        }

        private void CheckIValueForStackSpill(ref AssemblyExpr.IValue value)
        {
            if (stackSpill.ContainsKey(value))
            {
                value = stackSpill[value] ??=
                    new AssemblyExpr.Pointer(AssemblyExpr.Register.RegisterName.RBP, functionPushPreserved.size, value.Size);

                functionPushPreserved.size += (int)value.Size;
            }
        }

        public object? VisitComment(AssemblyExpr.Comment instruction) => null;
        public object? VisitData(AssemblyExpr.Data instruction) => null;
        public object? VisitGlobal(AssemblyExpr.Global instruction) => null;
        public object? VisitInclude(AssemblyExpr.Include instruction) => null;
        public object? VisitLocalProcedure(AssemblyExpr.LocalProcedure instruction) => null;
        public object? VisitSection(AssemblyExpr.Section instruction) => null;

        public object? VisitBinary(AssemblyExpr.Binary instruction)
        {
            CheckIValueForStackSpill(ref instruction.operand1);
            CheckIValueForStackSpill(ref instruction.operand2);

            instruction.operand1.Accept(this);
            instruction.operand2.Accept(this);
            return null;
        }

        public object? VisitImmediate(AssemblyExpr.Literal imm) => null;

        public object? VisitMemory(AssemblyExpr.Pointer ptr)
        {
            functionPushPreserved.IncludeRegister(ptr.value);
            return null;
        }

        public object? VisitProcedure(AssemblyExpr.Procedure instruction)
        {
            functionPushPreserved.GenerateHeader(this);
            functionPushPreserved = new(idx, ++fppIdx);
            return null;
        }

        public object? VisitRegister(AssemblyExpr.Register reg)
        {
            functionPushPreserved.IncludeRegister(reg);
            return null;
        }

        public object? VisitUnary(AssemblyExpr.Unary instruction)
        {
            CheckIValueForStackSpill(ref instruction.operand);

            if (instruction.instruction == AssemblyExpr.Instruction.CALL)
            {
                functionPushPreserved.leaf = false;
            }
            instruction.operand.Accept(this);
            return null;
        }

        public object? VisitZero(AssemblyExpr.Nullary instruction)
        {
            if (instruction.instruction == AssemblyExpr.Instruction.RET)
            {
                functionPushPreserved.RegisterFooter(idx);
            }
            return null;
        }
    }
}