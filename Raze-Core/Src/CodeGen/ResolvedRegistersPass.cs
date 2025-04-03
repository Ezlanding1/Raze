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

            if (instruction.instruction == AssemblyExpr.Instruction.CAST)
            {
                instruction.instruction = AssemblyExpr.Instruction.MOV;
                if (instruction.operand2.IsRegister(out var reg))
                {
                    instruction.operand2 = new AssemblyExpr.Register(reg.name, instruction.operand1.Size);
                }
                else if (instruction.operand2.IsPointer(out var ptr))
                {
                    instruction.operand2 = new AssemblyExpr.Pointer(ptr.value, ptr.offset, instruction.operand1.Size);
                }
            }
            // MOVZX R64, R/M32 is not encodable, and instead MOV R32, R/M32 zero-extends
            // To handle this in inline asm, instructions of the former type are legal and converted into the latter here
            else if (instruction.instruction == AssemblyExpr.Instruction.MOVZX &&
                instruction.operand1.Size == AssemblyExpr.Register.RegisterSize._64Bits &&
                instruction.operand2.Size == AssemblyExpr.Register.RegisterSize._32Bits)
            {
                instruction.instruction = AssemblyExpr.Instruction.MOV;
                // We can safely assume that operand1 is a register
                var reg1 = (AssemblyExpr.Register)instruction.operand1;

                instruction.operand1 = new AssemblyExpr.Register(reg1.name, instruction.operand2.Size);
            }

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