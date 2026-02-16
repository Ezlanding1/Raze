using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class CodeGen : Expr.IVisitor<AssemblyExpr.IValue?>
{
    internal class CodeGenOptimizationPass(ISection.Text asmExprs) :
        ResolvedCodeGenPass(asmExprs),
        AssemblyExpr.IVisitor<object?>,
        AssemblyExpr.IBinaryOperandVisitor<object?>,
        AssemblyExpr.IUnaryOperandVisitor<object?>
    {
        AssemblyExpr.Instruction instruction;

        public override void Run()
        {
            for (; idx < AssemblyExprsCount(); idx++)
            {
                GetAssemblyExpr(idx).Accept(this);
            }
        }

        public object? VisitBinary(AssemblyExpr.Binary instruction)
        {
            if (instruction.instruction == AssemblyExpr.Instruction.LEA)
            {
                var op2 = (AssemblyExpr.Pointer)instruction.operand2;

                if (op2.offset.value.All(x => x == 0))
                {
                    instruction.instruction = AssemblyExpr.Instruction.MOV;
                    instruction.operand2 = op2.value!;
                }
            }

            this.instruction = instruction.instruction;
            instruction.operand2.Accept(this, instruction.operand1);
            return null;
        }

        public object? VisitComment(AssemblyExpr.Comment instruction) => null;
        public object? VisitData(AssemblyExpr.Data instruction) => null;
        public object? VisitGlobal(AssemblyExpr.Global instruction) => null;
        public object? VisitImmediate(AssemblyExpr.Literal imm) => null;
        public object? VisitSection(AssemblyExpr.Section instruction) => null;
        public object? VisitInclude(AssemblyExpr.Include instruction) => null;
        public object? VisitLocalProcedure(AssemblyExpr.LocalProcedure instruction) => null;
        public object? VisitProcedure(AssemblyExpr.Procedure instruction) => null;

        public object? VisitMemory(AssemblyExpr.Pointer ptr) => null;
        public object? VisitMemoryImmediate(AssemblyExpr.Pointer ptr1, AssemblyExpr.Literal imm2) => null;
        public object? VisitMemoryMemory(AssemblyExpr.Pointer ptr1, AssemblyExpr.Pointer ptr2) => null;
        public object? VisitMemoryRegister(AssemblyExpr.Pointer ptr1, AssemblyExpr.Register reg2) => null;
        public object? VisitRegister(AssemblyExpr.Register reg) => null;
        public object? VisitRegisterImmediate(AssemblyExpr.Register reg1, AssemblyExpr.Literal imm2) => null;
        
        public object? VisitRegisterMemory(AssemblyExpr.Register reg1, AssemblyExpr.Pointer ptr2)
        {
            if (instruction == AssemblyExpr.Instruction.LEA &&
                ptr2.offset.value.All(x => x == 0) &&
                reg1.name == ptr2.value?.name &&
                reg1.Size == ptr2.value.Size &&
                reg1.Size == InstructionUtils.SYS_SIZE)
            {
                RemoveCurrentInstruction();
            }
            return null;
        }

        public object? VisitRegisterRegister(AssemblyExpr.Register reg1, AssemblyExpr.Register reg2)
        {
            if (instruction == AssemblyExpr.Instruction.MOV &&
                reg1.name == reg2.name &&
                reg1.Size == reg2.Size)
            {
                RemoveCurrentInstruction();
            }
            else if (instruction == AssemblyExpr.Instruction.MOVSS &&
                reg1.name == reg2.name &&
                reg1.Size == reg2.Size)
            {
                RemoveCurrentInstruction();
            }
            else if (instruction == AssemblyExpr.Instruction.MOVSD &&
                reg1.name == reg2.name &&
                reg1.Size == reg2.Size)
            {
                RemoveCurrentInstruction();
            }
            return null;
        }

        public object? VisitUnary(AssemblyExpr.Unary instruction)
        {
            this.instruction = instruction.instruction;
            instruction.operand.Accept(this);
            return null;
        }
        public object? VisitZero(AssemblyExpr.Nullary instruction) => null;
    }
}
