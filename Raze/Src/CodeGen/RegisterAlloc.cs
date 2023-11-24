using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
            Diagnostics.errors.Push(new Error.ImpossibleError("Requesting stack memory using the RegisterAlloc class is not implemented in this version of the compiler"));
            return -1;
        }
    }

    RegisterState[] registerStates = { new NeedableRegisterState(), new(), new(), new(), new(), new(), new(), new() };

    StrongBox<AssemblyExpr.Register.RegisterName>?[] registers = new StrongBox<AssemblyExpr.Register.RegisterName>[6];

    public AssemblyExpr.Register?[] paramRegisters = new AssemblyExpr.Register[6];

    public FunctionPushPreserved fncPushPreserved;

    public AssemblyExpr.Register GetRegister(int idx, AssemblyExpr.Register.RegisterSize size)
    {
        if (registers[idx] == null)
        {
            registers[idx] = new(InstructionUtils.storageRegisters[idx]);
        }
        registerStates[idx].SetState(RegisterState.RegisterStates.Used);

        return new AssemblyExpr.Register(registers[idx], size);
    }

    public AssemblyExpr.Register NextRegister(AssemblyExpr.Register.RegisterSize size)
    {
        if (RegisterIdx != 0)
        {
            fncPushPreserved.IncludeRegister(RegisterIdx);
        }
        return GetRegister(RegisterIdx, size);
    }

    // Makes all subsequent register requests for register[idx] pull from a new instance (while not modifying used[idx])
    public void NullReg() => registers[RegisterIdx] = null;
    public void NullReg(int idx) => registers[idx] = null;

    public AssemblyExpr.Register CurrentRegister(AssemblyExpr.Register.RegisterSize size)
    {
        if (registerStates[RegisterIdx].HasState(RegisterState.RegisterStates.Free))
        {
            registers[RegisterIdx] = new(InstructionUtils.storageRegisters[RegisterIdx]);
        }
        return new AssemblyExpr.Register(registers[RegisterIdx], size);
    }

    public void ReserveRegister(CodeGen assembler, int i = 0)
    {
        if (registerStates[i].HasState(RegisterState.RegisterStates.Free))
        {
            return;
        }

        int newIdx = RegisterIdx;

        if (registerStates[i].HasState(RegisterState.RegisterStates.Needed))
        {
            registers[i].Value = InstructionUtils.storageRegisters[newIdx];
            assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, NextRegister(((NeedableRegisterState)registerStates[i]).neededSize), new AssemblyExpr.Register(InstructionUtils.storageRegisters[i], ((NeedableRegisterState)registerStates[i]).neededSize)));
            registers[i] = null;
        }
        else
        {
            registers[newIdx] = registers[i];
            registers[newIdx].Value = InstructionUtils.storageRegisters[newIdx];
            fncPushPreserved.IncludeRegister(newIdx);
            registerStates[newIdx].SetState(RegisterState.RegisterStates.Used);
            registers[i] = null;
        }

        registerStates[newIdx].SetState(registerStates[i]);
        registerStates[i].SetState(RegisterState.RegisterStates.Free);
    }

    public AssemblyExpr.Register AllocParam(int i, AssemblyExpr.Register.RegisterSize size, AssemblyExpr.Register?[] localParams, CodeGen assembler)
    {
        if (paramRegisters[i] != null)
        {
            AssemblyExpr.Register newReg = NextRegister(paramRegisters[i].size);
            assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, newReg, paramRegisters[i]));
            localParams[i] = newReg;
        }
        return (paramRegisters[i] = new AssemblyExpr.Register(InstructionUtils.paramRegister[i], size));
    }

    public AssemblyExpr.Register CallAlloc(AssemblyExpr.Register.RegisterSize size)
    {
        ((NeedableRegisterState)registerStates[0]).neededSize = size;
        registerStates[0].SetState(RegisterState.RegisterStates.Needed);
        return GetRegister(0, size);
    }

    public void Lock(AssemblyExpr.Register register) => Lock(NameToIdx(register.name));
    public void Lock(int idx) => registerStates[idx].SetState(RegisterState.RegisterStates.Used, RegisterState.RegisterStates.Locked);

    public void Unlock(AssemblyExpr.Register register) => Unlock(NameToIdx(register.name));
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

    public int NameToIdx(AssemblyExpr.Register.RegisterName name)
    {
        int idx = -1;
        while (++idx < InstructionUtils.storageRegisters.Length && InstructionUtils.storageRegisters[idx] != name) ;
        return idx < InstructionUtils.storageRegisters.Length ? idx : -1;
    }

    public void FreeParameter(int i, AssemblyExpr.Register? localParam, CodeGen assembler)
    {
        if (i >= paramRegisters.Length || paramRegisters[i] == null) return;
        paramRegisters[i] = null;

        if (localParam != null)
        {
            assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, new AssemblyExpr.Register(InstructionUtils.paramRegister[i], localParam.size), localParam));
            Free(NameToIdx(localParam.name));
        }
    }


    public void Free(AssemblyExpr.Value value, bool force = false)
    {
        if (value.IsPointer())
        {
            FreePtr((AssemblyExpr.Pointer)value, force);
        }
        else if (value.IsRegister())
        {
            FreeRegister((AssemblyExpr.Register)value, force);
        }
    }
    public void FreeRegister(AssemblyExpr.Register register, bool force = false) => Free(NameToIdx(register.name), force);
    public void FreePtr(AssemblyExpr.Pointer ptr, bool force = false) => FreeRegister(ptr.register, force);

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
