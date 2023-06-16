using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze
{
    internal class RegisterAlloc
    {
        public int registerIdx {  
            get 
            { 
                for (int i = 0; i < used.Length; i++)
                {
                    if (used[i] == false) { return i; } 
                }
                throw new Errors.ImpossibleError("Requesting stack memory using the RegisterAlloc class is not implemented in this version of the compiler"); 
            }
        }

        bool[] used = new bool[6];
        Instruction.Register?[] registers = new Instruction.Register[6];

        public bool raxNeeded;

        public bool[] fncPushPreserved = new bool[5];

        public Instruction.Register GetRegister(int idx, Instruction.Register.RegisterSize size)
        {
            if (used[idx] == false)
            {
                registers[idx] = new(InstructionUtils.storageRegisters[idx], size);
                used[idx] = true;
            }

            return registers[idx];
        }

        public Instruction.Register NextRegister(Instruction.Register.RegisterSize size)
        {
            if (registerIdx != 0)
            {
                fncPushPreserved[registerIdx-1] = true;
            }
            return GetRegister(registerIdx, size);
        }

        public Instruction.Register CurrentRegister(Instruction.Register.RegisterSize size)
        {
            if (used[registerIdx] == false)
            {
                registers[registerIdx] = new(InstructionUtils.storageRegisters[registerIdx], size);
            }
            return registers[registerIdx];
        }

        public void ReserveRax(Assembler assembler)
        {
            if (!used[0])
            {
                return;
            }

            if (raxNeeded)
            {
                registers[0].name = InstructionUtils.storageRegisters[registerIdx];
                assembler.emit(new Instruction.Binary("MOV", NextRegister(registers[0].size), new Instruction.Register(Instruction.Register.RegisterName.RAX, registers[0].size)));
                registers[0] = null;
                used[0] = false;
            }
            else if (used[0] != false)
            {
                registers[registerIdx] = registers[0];
                registers[registerIdx].name = InstructionUtils.storageRegisters[registerIdx];
                registers[0] = null;
                used[0] = true;
            }
        }

        public Instruction.Register CallAlloc(Instruction.Register.RegisterSize size)
        {
            raxNeeded = true;
            return GetRegister(0, size);
        }

        public void ListAccept<T, T2>(List<T> list, Expr.IVisitor<T2> visitor) where T : Expr
        {
            foreach (var expr in list)
            {
                expr.Accept(visitor);
                FreeAll();
            }
        }

        public int NameToIdx(Instruction.Register.RegisterName name)
        {
            int idx = -1;
            while (InstructionUtils.storageRegisters[++idx] != name);
            return idx;
        }

        public void FreeRegister(Instruction.Register register) => Free(NameToIdx(register.name));
        public void FreePtr(Instruction.Pointer ptr) => FreeRegister(ptr.register);

        public void Free(int idx)
        {
            registers[idx] = null;
            used[idx] = false;
        }

        public void FreeAll()
        {
            raxNeeded = false;
            for (int i = 0; i < registers.Length; i++)
            {
                Free(i);
            }
        }
    }
}
