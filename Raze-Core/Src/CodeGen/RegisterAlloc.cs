using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Raze;
public partial class CodeGen : Expr.IVisitor<AssemblyExpr.Value?>
{
    internal partial class RegisterAlloc
    {
        private int RegisterIdx
        {
            get
            {
                for (int i = 0; i < registerStates.Length; i++)
                {
                    if (registerStates[i].HasState(RegisterState.RegisterStates.Free)) { return i; }
                }
                throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("Requesting stack memory using the RegisterAlloc class is not implemented in this version of the compiler"));
            }
        }

        RegisterState[] registerStates = InstructionUtils.storageRegisters.Select(x => new RegisterState()).ToArray();
        (AssemblyExpr.Register.RegisterSize neededSize, int idx)[] preservedRegisterInfo = 
            InstructionUtils.storageRegisters.Where(x => x.IsScratchRegister).Select(x => ((AssemblyExpr.Register.RegisterSize)0, 0)).ToArray();

        StrongBox<AssemblyExpr.Register.RegisterName>?[] registers = new StrongBox<AssemblyExpr.Register.RegisterName>[InstructionUtils.storageRegisters.Length];

        public AssemblyExpr.Register?[] paramRegisters = new AssemblyExpr.Register[InstructionUtils.paramRegister.Length];

        public FunctionPushPreserved fncPushPreserved;

        public AssemblyExpr.Register GetRegister(int idx, AssemblyExpr.Register.RegisterSize size)
        {
            if (registers[idx] == null)
            {
                registers[idx] = new(InstructionUtils.storageRegisters[idx].Name);
            }
            registerStates[idx].SetState(RegisterState.RegisterStates.Used);

            return new AssemblyExpr.Register(registers[idx], size);
        }

        public AssemblyExpr.Register NextRegister(AssemblyExpr.Register.RegisterSize size)
        {
            if (!InstructionUtils.storageRegisters[RegisterIdx].IsScratchRegister)
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
                registers[RegisterIdx] = new(InstructionUtils.storageRegisters[RegisterIdx].Name);
            }
            return new AssemblyExpr.Register(registers[RegisterIdx], size);
        }

        private int NextPreservedRegisterIdx()
        {
            int idx = InstructionUtils.ScratchRegisterCount;
            while (!registerStates[idx].HasState(RegisterState.RegisterStates.Free)) { idx++; }
            fncPushPreserved.IncludeRegister(idx);
            return idx;
        }

        public void SavePreservedRegistersBeforeCall(CodeGen assembler)
        {
            for (int i = 0; i < InstructionUtils.ScratchRegisterCount; i++)
            {
                if (registerStates[i].HasState(RegisterState.RegisterStates.Free))
                {
                    continue;
                }

                _ReserveRegister(assembler, i, NextPreservedRegisterIdx());
            }
        }

        public void ReserveRegister(CodeGen assembler, int i) =>
            _ReserveRegister(assembler, i, RegisterIdx);

        private void _ReserveRegister(CodeGen assembler, int i, int newIdx)
        {
            if (registerStates[i].HasState(RegisterState.RegisterStates.Free))
            {
                return;
            }

            registers[newIdx] = registers[i];
            if (registerStates[i].HasState(RegisterState.RegisterStates.Needed))
            {
                assembler.assembly.text.Insert(preservedRegisterInfo[i].idx, new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, GetRegister(newIdx, preservedRegisterInfo[i].neededSize), new AssemblyExpr.Register(InstructionUtils.storageRegisters[i].Name, preservedRegisterInfo[i].neededSize)));
                registerStates[i].RemoveState(RegisterState.RegisterStates.Needed);
            }
            registers[newIdx].Value = InstructionUtils.storageRegisters[newIdx].Name;
            registerStates[newIdx].SetState(registerStates[i]);

            Free(i, true);
        }

        public AssemblyExpr.Register AllocParam(int i, AssemblyExpr.Register.RegisterSize size, AssemblyExpr.Register?[] localParams, CodeGen assembler)
        {
            if (paramRegisters[i] != null)
            {
                AssemblyExpr.Register newReg = GetRegister(NextPreservedRegisterIdx(), paramRegisters[i].Size);
                assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, newReg, paramRegisters[i]));
                localParams[i] = newReg;
            }
            return (paramRegisters[i] = new AssemblyExpr.Register(InstructionUtils.paramRegister[i], size));
        }

        public AssemblyExpr.Register NeededAlloc(AssemblyExpr.Register.RegisterSize size, CodeGen codeGen, int i = 0)
        {
            (preservedRegisterInfo[i].neededSize, preservedRegisterInfo[i].idx) = (size, codeGen.assembly.text.Count);
            registerStates[i].SetState(RegisterState.RegisterStates.Needed);
            return GetRegister(i, size);
        }

        public void Lock(AssemblyExpr.Register register) => Lock(NameToIdx(register.Name));
        public void Lock(int idx) => registerStates[idx].SetState(RegisterState.RegisterStates.Used, RegisterState.RegisterStates.Locked);

        public void Unlock(AssemblyExpr.Register register) => Unlock(NameToIdx(register.Name));
        public void Unlock(int idx) => registerStates[idx].RemoveState(RegisterState.RegisterStates.Locked);

        public bool IsLocked(int idx) => registerStates[idx].HasState(RegisterState.RegisterStates.Locked);
        public bool IsNeeded(int idx) => registerStates[idx].HasState(RegisterState.RegisterStates.Needed);

        public void ListAccept<T, T2>(List<T> list, Expr.IVisitor<T2> visitor)
            where T : Expr
            where T2 : AssemblyExpr.Value
        {
            foreach (var expr in list)
            {
                Free(expr.Accept(visitor));
            }
        }

        public int NameToIdx(AssemblyExpr.Register.RegisterName name) => 
            Array.IndexOf(InstructionUtils.storageRegisters.Select(x => x.Name).ToArray(), name);

        public void FreeParameter(int i, AssemblyExpr.Register? localParam, CodeGen assembler)
        {
            if (i >= paramRegisters.Length || paramRegisters[i] == null) return;
            paramRegisters[i] = null;

            if (localParam != null)
            {
                assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, new AssemblyExpr.Register(InstructionUtils.paramRegister[i], localParam.Size), localParam));
                Free(NameToIdx(localParam.Name));
            }
        }


        public void Free(AssemblyExpr.Value? value, bool force = false)
        {
            if (value == null) return;

            if (value.IsPointer())
            {
                FreePtr((AssemblyExpr.Pointer)value, force);
            }
            else if (value.IsRegister())
            {
                FreeRegister((AssemblyExpr.Register)value, force);
            }
        }
        public void FreeRegister(AssemblyExpr.Register register, bool force = false) => Free(NameToIdx(register.Name), force);
        public void FreePtr(AssemblyExpr.Pointer ptr, bool force = false) => FreeRegister(ptr.register, force);

        // Frees a register by allowing it to be alloc-ed elsewhere, and making the other uses pull from a new instance
        private void Free(int idx, bool force = false)
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

        public RegisterState? SaveRegisterState(AssemblyExpr.RegisterPointer? registerPointer) 
        {
            int idx;

            if (registerPointer == null || (idx = NameToIdx(registerPointer.GetRegister().Name)) == -1)
            {
                return null;
            }

            var state = new RegisterState(); 
            state.SetState(registerStates[idx]);
            return state;
        }
        public void SetRegisterState(RegisterState? state,  AssemblyExpr.RegisterPointer registerPointer)
        {
            if (state == null)
                return;

            int idx = NameToIdx(registerPointer.GetRegister().Name);

            registers[idx] = registerPointer.GetRegister().nameBox;
            registerStates[idx] = (RegisterState)state;
        }
    }
}
