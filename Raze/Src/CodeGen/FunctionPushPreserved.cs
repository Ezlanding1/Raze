using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

internal class FunctionPushPreserved
{
    public bool leaf = true;
    public int size;
    bool[] registers = new bool[5];
    int count;

    public FunctionPushPreserved(int size, int count)
    {
        this.size = size;
        this.count = count;
    }

    public void IncludeRegister(int idx) => registers[idx-1] = true;

    public void GenerateHeader(List<AssemblyExpr> assemblyExprs)
    {
        assemblyExprs.Insert(count++, new AssemblyExpr.Unary(AssemblyExpr.Instruction.PUSH, AssemblyExpr.Register.RegisterName.RBP));
        assemblyExprs.Insert(count++, new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, AssemblyExpr.Register.RegisterName.RBP, AssemblyExpr.Register.RegisterName.RSP));

        if (!((leaf || size == 0) && size <= 128))
        {
            assemblyExprs.Insert(count++, StackAlloc.GenerateStackAlloc((size > 128) ? size - 128 : size));
        }

        for (int i = 0; i < registers.Length; i++)
        {
            if (registers[i])
                assemblyExprs.Insert(count++, 
                    new AssemblyExpr.Unary(AssemblyExpr.Instruction.PUSH, new AssemblyExpr.Register(InstructionUtils.storageRegisters[i + 1], AssemblyExpr.Register.RegisterSize._64Bits)));
        }
    }
        
    public void GenerateFooter(List<AssemblyExpr> assemblyExprs)
    {
        for (int i = registers.Length - 1; i >= 0; i--)
        {
            if (registers[i])
                assemblyExprs.Add(new AssemblyExpr.Unary(AssemblyExpr.Instruction.POP, new AssemblyExpr.Register(InstructionUtils.storageRegisters[i + 1], AssemblyExpr.Register.RegisterSize._64Bits)));
        }

        assemblyExprs.Add(
            leaf ?
            new AssemblyExpr.Unary(AssemblyExpr.Instruction.POP, AssemblyExpr.Register.RegisterName.RBP) : 
            new AssemblyExpr.Zero(AssemblyExpr.Instruction.LEAVE)
        );

        assemblyExprs.Add(new AssemblyExpr.Zero(AssemblyExpr.Instruction.RET));
    }

    internal class StackAlloc
    {
        public static AssemblyExpr.Binary GenerateStackAlloc(int allocSize)
        {
            return new AssemblyExpr.Binary(AssemblyExpr.Instruction.SUB, new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RSP, InstructionUtils.SYS_SIZE),
                new AssemblyExpr.Literal(Parser.LiteralTokenType.INTEGER, AlignTo16(allocSize).ToString()));
        }

        private static string AlignTo16(int i)
        {
            return (((int)Math.Ceiling(i / 16f)) * 16).ToString();
        }
    }
}