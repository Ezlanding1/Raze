using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class CodeGen : Expr.IVisitor<AssemblyExpr.IValue?>
{
    internal class FunctionPushPreserved
    {
        public bool leaf = true;
        
        bool[] registers = new bool[InstructionUtils.storageRegisters.Length - InstructionUtils.ScratchRegisterCount];
        public int size;
        int count;
        Stack<int> footers = new();

        public FunctionPushPreserved(int count)
        {
            this.count = count;
        }

        public void IncludeRegister(int idx) => registers[idx - InstructionUtils.NonSseScratchRegisterCount] = true;

        public void GenerateHeader(ISection.Text assemblyExprs)
        {
            while (footers.Count > 0)
            {
                int location = footers.Pop();
                GenerateFooter(assemblyExprs, location);
            }

            assemblyExprs.Insert(count++, new AssemblyExpr.Unary(AssemblyExpr.Instruction.PUSH, AssemblyExpr.Register.RegisterName.RBP));
            assemblyExprs.Insert(count++, new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, AssemblyExpr.Register.RegisterName.RBP, AssemblyExpr.Register.RegisterName.RSP));

            if (!((leaf || size == 0) && size <= 128))
            {
                assemblyExprs.Insert(count++, GenerateStackAlloc((size > 128) ? size - 128 : size));
            }

            for (int i = 0; i < registers.Length; i++)
            {
                if (registers[i])
                    assemblyExprs.Insert(count++,
                        new AssemblyExpr.Unary(AssemblyExpr.Instruction.PUSH, new AssemblyExpr.Register(InstructionUtils.storageRegisters[i + InstructionUtils.NonSseScratchRegisterCount].Name, AssemblyExpr.Register.RegisterSize._64Bits)));
            }
        }

        private void GenerateFooter(ISection.Text assemblyExprs, int location)
        {
            for (int i = registers.Length - 1; i >= 0; i--)
            {
                if (registers[i])
                    assemblyExprs.Insert(location++, new AssemblyExpr.Unary(AssemblyExpr.Instruction.POP, new AssemblyExpr.Register(InstructionUtils.storageRegisters[i + InstructionUtils.NonSseScratchRegisterCount].Name, AssemblyExpr.Register.RegisterSize._64Bits)));
            }

            assemblyExprs.Insert(location++,
                leaf ?
                new AssemblyExpr.Unary(AssemblyExpr.Instruction.POP, AssemblyExpr.Register.RegisterName.RBP) :
                new AssemblyExpr.Nullary(AssemblyExpr.Instruction.LEAVE)
            );

            assemblyExprs.Insert(location, new AssemblyExpr.Nullary(AssemblyExpr.Instruction.RET));
        }

        public void RegisterFooter(ISection.Text assemblyExprs) => footers.Push(assemblyExprs.Count);

        private static AssemblyExpr.Binary GenerateStackAlloc(int allocSize)
        {
            return new AssemblyExpr.Binary(AssemblyExpr.Instruction.SUB, new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RSP, InstructionUtils.SYS_SIZE),
                new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, BitConverter.GetBytes(AlignTo16(allocSize))));
        }

        private static int AlignTo16(int i)
        {
            return (((int)Math.Ceiling(i / 16f)) * 16);
        }
    }
}
