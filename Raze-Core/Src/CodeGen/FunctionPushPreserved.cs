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
        private static bool stackStartsAligned;
        private static int redZoneSize;

        public static void SetRedZoneSize(bool outputElf)
        {
            const int linuxRedZoneSize = 128;
            const int windowsRedZoneSize = 0;

            (stackStartsAligned, redZoneSize) = outputElf ?
                    (true, linuxRedZoneSize) :
                    (false, windowsRedZoneSize);
        }

        public bool leaf = true;

        Dictionary<AssemblyExpr.Register.RegisterName, bool> registers =
            InstructionUtils.GetCallingConvention().nonVolatileRegisters.ToDictionary(x => x, x => false);

        public readonly static List<int> totalSizes = [];
        public int size;
        int count;
        Stack<int> footers = new();

        bool StackAllocNeeded => !((leaf || size == 0) && size <= redZoneSize);

        public FunctionPushPreserved(int count, int fppIdx)
        {
            this.count = count;
            size = totalSizes[fppIdx];
        }

        private void IncludeRegister(AssemblyExpr.Register.RegisterName register) => registers[register] = true;
        public void IncludeRegister(AssemblyExpr.Register? register)
        {
            if (register == null) return;

            if (registers.ContainsKey(register.name))
                IncludeRegister(register.name);
        }

        public void GenerateHeader(ResolvedRegistersPass resolvedRegistersPass)
        {
            bool firstCall = count == 0;

            if (!firstCall)
            {
                count++;
                while (footers.Count > 0)
                {
                    int location = footers.Pop();
                    GenerateFooter(resolvedRegistersPass, location);
                }
                resolvedRegistersPass.InsertInstruction(count++, new AssemblyExpr.Unary(AssemblyExpr.Instruction.PUSH, AssemblyExpr.Register.RegisterName.RBP));
                resolvedRegistersPass.InsertInstruction(count++, new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, AssemblyExpr.Register.RegisterName.RBP, AssemblyExpr.Register.RegisterName.RSP));
            }

            if (StackAllocNeeded || StackAlignmentNeeded(firstCall, stackStartsAligned))
            {
                resolvedRegistersPass.InsertInstruction(count++, GenerateStackAlloc((size > redZoneSize) ? size - redZoneSize : size, firstCall));
            }

            if (!firstCall)
            {
                foreach (var register in InstructionUtils.GetCallingConvention().nonVolatileRegisters)
                {
                    if (registers[register])
                    {
                        resolvedRegistersPass.InsertInstruction(
                            count++,
                            new AssemblyExpr.Unary(
                                AssemblyExpr.Instruction.PUSH,
                                new AssemblyExpr.Register(register, AssemblyExpr.Register.RegisterSize._64Bits)
                            )
                        );
                    }
                }
            }
        }

        private void GenerateFooter(ResolvedRegistersPass resolvedRegistersPass, int location)
        {
            foreach (var register in InstructionUtils.GetCallingConvention().nonVolatileRegisters.Reverse())
            {
                if (registers[register])
                {
                    resolvedRegistersPass.InsertInstruction(
                        location++, 
                        new AssemblyExpr.Unary(
                            AssemblyExpr.Instruction.POP, 
                            new AssemblyExpr.Register(register, AssemblyExpr.Register.RegisterSize._64Bits)
                        )
                    );
                }
            }

            resolvedRegistersPass.InsertInstruction(location++,
                StackAllocNeeded ?
                new AssemblyExpr.Nullary(AssemblyExpr.Instruction.LEAVE) :
                new AssemblyExpr.Unary(AssemblyExpr.Instruction.POP, AssemblyExpr.Register.RegisterName.RBP)
            );
        }

        public void RegisterFooter(ISection.Text assemblyExprs) => footers.Push(assemblyExprs.Count);
        public void RegisterFooter(int idx) => footers.Push(idx);

        private static AssemblyExpr.Binary GenerateStackAlloc(int allocSize, bool firstCall)
        {
            int callOffset = StackAlignmentNeeded(firstCall, stackStartsAligned) ? 8 : 0;
            
            return new AssemblyExpr.Binary(AssemblyExpr.Instruction.SUB, new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RSP, InstructionUtils.SYS_SIZE),
                new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, BitConverter.GetBytes(AlignTo16(allocSize) + callOffset)));
        }

        private static int AlignTo16(int i)
        {
            return (((int)Math.Ceiling(i / 16f)) * 16);
        }
        
        private static bool StackAlignmentNeeded(bool firstCall, bool stackStartsAligned) =>
            firstCall && !stackStartsAligned;
    }
}
