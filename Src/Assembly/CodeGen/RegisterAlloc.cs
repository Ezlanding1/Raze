using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

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
    public bool raxNeeded;
    bool[] registerLocks = new bool[6];

    Instruction.Register?[] registers = new Instruction.Register[6];

    public Instruction.Register?[] paramRegisters = new Instruction.Register[6];

    public bool[] fncPushPreserved = new bool[5];

    public Instruction.Register GetRegister(int idx, Instruction.Register.RegisterSize size)
    {
        if (registers[idx] == null)
        {
            registers[idx] = new(InstructionUtils.storageRegisters[idx], size);
        }
        else
        {
            registers[idx].size = size;
        }
        used[idx] = true;

        return registers[idx];
    }

    public Instruction.Register NextRegister(Instruction.Register.RegisterSize size)
    {
        if (registerIdx != 0)
        {
            fncPushPreserved[registerIdx - 1] = true;
        }
        return GetRegister(registerIdx, size);
    }
    
    // Makes all subsequent register requests for register[idx] pull from a new instance (while not modifying used[idx])
    public void NullReg(int idx) => registers[idx] = null;

    public Instruction.Register CurrentRegister(Instruction.Register.RegisterSize size)
    {
        if (used[registerIdx] == false)
        {
            registers[registerIdx] = new(InstructionUtils.storageRegisters[registerIdx], size);
        }
        return registers[registerIdx];
    }

    public void ReserveRegister(Assembler assembler, int i = 0)
    {
        if (!used[i])
        {
            return;
        }

        if (i == 0 && raxNeeded)
        {
            registers[i].name = InstructionUtils.storageRegisters[registerIdx];
            registerLocks[registerIdx] = registerLocks[i];
            assembler.emit(new Instruction.Binary("MOV", NextRegister(registers[i].size), new Instruction.Register(Instruction.Register.RegisterName.RAX, registers[i].size)));
            registers[i] = null;
            registerLocks[i] = false;
            used[i] = false;
        }
        else
        {
            registers[registerIdx] = registers[i];
            registerLocks[registerIdx] = registerLocks[i];
            registers[registerIdx].name = InstructionUtils.storageRegisters[registerIdx];
            fncPushPreserved[registerIdx - 1] = true;
            used[registerIdx] = true;
            registers[i] = null;
            registerLocks[i] = false;
            used[i] = false;
        }
    }

    public Instruction.Register AllocParam(int i, Instruction.Register.RegisterSize size)
    {
        if (paramRegisters[i] != null)
        {
            paramRegisters[i].name = InstructionUtils.storageRegisters[registerIdx];
            registers[registerIdx] = paramRegisters[i];
            used[registerIdx] = true;
        }

        return (paramRegisters[i] = new Instruction.Register(InstructionUtils.paramRegister[i], size));
    }

    public Instruction.Register CallAlloc(Instruction.Register.RegisterSize size)
    {
        raxNeeded = true;
        return GetRegister(0, size);
    }

    public void Lock(Instruction.Register register) => Lock(NameToIdx(register.name));
    public void Lock(int idx) => registerLocks[idx] = true;

    public void Unlock(Instruction.Register register) => Unlock(NameToIdx(register.name));
    public void Unlock(int idx) => registerLocks[idx] = false;

    public bool IsLocked(int idx) => registerLocks[idx];

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
        while (++idx < InstructionUtils.storageRegisters.Length && InstructionUtils.storageRegisters[idx] != name) ;
        return idx < InstructionUtils.storageRegisters.Length ? idx : -1;
    }

    public void FreeParameter(int i, Instruction.Register param, Assembler assembler)
    {
        var allocIdx = NameToIdx(param.name);

        if (allocIdx != -1)
        {
            assembler.emit(new Instruction.Binary("MOV", new Instruction.Register(InstructionUtils.paramRegister[i], param.size), param));
            Free(allocIdx);
        }
        else
        {
            paramRegisters[i] = null;
        }
    }


    public void Free(Instruction.Value value, bool force = false)
    {
        if (value.IsPointer() && ((Instruction.Pointer)value).register.name != Instruction.Register.RegisterName.RBP)
        {
            FreePtr((Instruction.Pointer)value, force);
        }
        else if (value.IsRegister())
        {
            FreeRegister((Instruction.Register)value, force);
        }
    }
    public void FreeRegister(Instruction.Register register, bool force = false) => Free(NameToIdx(register.name), force);
    public void FreePtr(Instruction.Pointer ptr, bool force = false) => FreeRegister(ptr.register, force);

    // Frees a register by allowing it to be alloc-ed elsewhere, and making the other uses pull from a new instance
    private void Free(int idx, bool force=false)
    {
        if (idx == -1)
        {
            return;
        }

        if (force)
        {
            registerLocks[idx] = false;
        }
        else
        {
            if (registerLocks[idx])
            {
                return;
            }
        }
        if (idx == 0) { raxNeeded = false; }
        registers[idx] = null;
        used[idx] = false;
    }

    public void FreeAll(bool force=true)
    {
        if (!registerLocks[0])
        {
            raxNeeded = false;
        }
        for (int i = 0; i < registers.Length; i++)
        {
            Free(i, force);
        }
    }
}
