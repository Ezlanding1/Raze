using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

internal class RegisterAlloc
{
    private int RegisterIdx {
        get
        {
            for (int i = 0; i < registerStates.Length; i++)
            {
                if (registerStates[i].HasState(RegisterState.RegisterStates.Free)) { return i; }
            }
            throw new Errors.ImpossibleError("Requesting stack memory using the RegisterAlloc class is not implemented in this version of the compiler");
        }
    }

    RegisterState[] registerStates = { new(), new(), new(), new(), new(), new(), new(), new() };

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
        registerStates[idx].SetState(RegisterState.RegisterStates.Used);

        return registers[idx];
    }

    public Instruction.Register NextRegister(Instruction.Register.RegisterSize size)
    {
        if (RegisterIdx != 0)
        {
            fncPushPreserved[RegisterIdx - 1] = true;
        }
        return GetRegister(RegisterIdx, size);
    }

    // Makes all subsequent register requests for register[idx] pull from a new instance (while not modifying used[idx])
    public void NullReg(int idx) => registers[idx] = null;

    public Instruction.Register CurrentRegister(Instruction.Register.RegisterSize size)
    {
        if (registerStates[RegisterIdx].HasState(RegisterState.RegisterStates.Free))
        {
            registers[RegisterIdx] = new(InstructionUtils.storageRegisters[RegisterIdx], size);
        }
        return registers[RegisterIdx];
    }

    public void ReserveRegister(Assembler assembler, int i = 0)
    {
        if (registerStates[i].HasState(RegisterState.RegisterStates.Free))
        {
            return;
        }

        int newIdx = RegisterIdx;

        if (registerStates[0].HasState(RegisterState.RegisterStates.Needed))
        {
            registers[i].name = InstructionUtils.storageRegisters[newIdx];
            assembler.Emit(new Instruction.Binary("MOV", NextRegister(registers[i].size), new Instruction.Register(Instruction.Register.RegisterName.RAX, registers[i].size)));
            registers[i] = null;
        }
        else
        {
            registers[newIdx] = registers[i];
            registers[newIdx].name = InstructionUtils.storageRegisters[newIdx];
            fncPushPreserved[newIdx - 1] = true;
            registerStates[newIdx].SetState(RegisterState.RegisterStates.Used);
            registers[i] = null;
        }

        registerStates[newIdx].SetState(registerStates[i]);
        registerStates[i].SetState(RegisterState.RegisterStates.Free);
    }

    public Instruction.Register AllocParam(int i, Instruction.Register.RegisterSize size)
    {
        if (paramRegisters[i] != null)
        {
            paramRegisters[i].name = InstructionUtils.storageRegisters[RegisterIdx];
            registers[RegisterIdx] = paramRegisters[i];
            registerStates[RegisterIdx].SetState(RegisterState.RegisterStates.Used);
        }

        return (paramRegisters[i] = new Instruction.Register(InstructionUtils.paramRegister[i], size));
    }

    public Instruction.Register CallAlloc(Instruction.Register.RegisterSize size)
    {
        registerStates[0].SetState(RegisterState.RegisterStates.Needed);
        return GetRegister(0, size);
    }

    public void Lock(Instruction.Register register) => Lock(NameToIdx(register.name));
    public void Lock(int idx) => registerStates[idx].SetState(RegisterState.RegisterStates.Used, RegisterState.RegisterStates.Locked);

    public void Unlock(Instruction.Register register) => Unlock(NameToIdx(register.name));
    public void Unlock(int idx) => registerStates[idx].RemoveState(RegisterState.RegisterStates.Locked);

    public bool IsLocked(int idx) => registerStates[idx].HasState(RegisterState.RegisterStates.Locked);

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
            assembler.Emit(new Instruction.Binary("MOV", new Instruction.Register(InstructionUtils.paramRegister[i], param.size), param));
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
            registerStates[idx].RemoveState(RegisterState.RegisterStates.Locked);
        }
        else
        {
            if (registerStates[idx].HasState(RegisterState.RegisterStates.Locked))
            {
                return;
            }
        }
        registers[idx] = null;
        registerStates[idx].SetState(RegisterState.RegisterStates.Free);
    }

    public void FreeAll(bool force=true)
    {
        for (int i = 0; i < registers.Length; i++)
        {
            Free(i, force);
        }
    }
}
