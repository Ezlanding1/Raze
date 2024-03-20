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
        (AssemblyExpr.Register.RegisterSize neededSize, int idx)[] preservedRegisterInfo = { (0, 0) };

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

        public void SavePreservedRegistersBeforeCall(CodeGen assembler)
        {
            int i = 0;
            if (registerStates[i].HasState(RegisterState.RegisterStates.Free))
            {
                return;
            }

            int newIdx = RegisterIdx;
            while (!registerStates[newIdx].HasState(RegisterState.RegisterStates.Free)) { newIdx++; }
            fncPushPreserved.IncludeRegister(newIdx);

            registers[i].Value = InstructionUtils.storageRegisters[newIdx];
            assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, GetRegister(newIdx, AssemblyExpr.Register.RegisterSize._64Bits), new AssemblyExpr.Register(InstructionUtils.storageRegisters[i], AssemblyExpr.Register.RegisterSize._64Bits)));
            registers[i] = null;

            registerStates[newIdx].SetState(registerStates[i]);
            registerStates[i].SetState(RegisterState.RegisterStates.Free);
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
                assembler.assembly.text.Insert(preservedRegisterInfo[i].idx, new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, GetRegister(newIdx, preservedRegisterInfo[i].neededSize), new AssemblyExpr.Register(InstructionUtils.storageRegisters[i], preservedRegisterInfo[i].neededSize)));
                registerStates[i].RemoveState(RegisterState.RegisterStates.Needed);
            }

            registers[newIdx] = registers[i]; 
            registers[newIdx].Value = InstructionUtils.storageRegisters[newIdx];
            fncPushPreserved.IncludeRegister(newIdx);
            registerStates[newIdx].SetState(registerStates[i]);

            Free(i, true);
        }

        public AssemblyExpr.Register AllocParam(int i, AssemblyExpr.Register.RegisterSize size, AssemblyExpr.Register?[] localParams, CodeGen assembler)
        {
            if (paramRegisters[i] != null)
            {
                AssemblyExpr.Register newReg = NextRegister(paramRegisters[i].Size);
                assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, newReg, paramRegisters[i]));
                localParams[i] = newReg;
            }
            return (paramRegisters[i] = new AssemblyExpr.Register(InstructionUtils.paramRegister[i], size));
        }

        public AssemblyExpr.Register CallAlloc(AssemblyExpr.Register.RegisterSize size, CodeGen codeGen)
        {
            (preservedRegisterInfo[0].neededSize, preservedRegisterInfo[0].idx) = (size, codeGen.assembly.text.Count);
            registerStates[0].SetState(RegisterState.RegisterStates.Needed);
            return GetRegister(0, size);
        }

        public void Lock(AssemblyExpr.Register register) => Lock(NameToIdx(register.Name));
        public void Lock(int idx) => registerStates[idx].SetState(RegisterState.RegisterStates.Used, RegisterState.RegisterStates.Locked);

        public void Unlock(AssemblyExpr.Register register) => Unlock(NameToIdx(register.Name));
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

        public int NameToIdx(AssemblyExpr.Register.RegisterName name) => 
            Array.IndexOf(InstructionUtils.storageRegisters, name);

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

        public void FreeAll(bool force = true)
        {
            for (int i = 0; i < registers.Length; i++)
            {
                Free(i, force);
            }
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
