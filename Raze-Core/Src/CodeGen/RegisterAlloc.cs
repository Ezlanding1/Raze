using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Raze;
public partial class CodeGen : Expr.IVisitor<AssemblyExpr.IValue?>
{
    internal partial class RegisterAlloc
    {
        private int GetRegisterIdx(int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                if (registerStates[i].HasState(RegisterState.RegisterStates.Free)) 
                    return i;
            }
            throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("Requesting stack memory using the RegisterAlloc class is not implemented in this version of the compiler"));
        }
        private int RegisterIdx => GetRegisterIdx(0, InstructionUtils.SseRegisterOffset);
        private int PreservedRegisterIdx => GetRegisterIdx(InstructionUtils.NonSseScratchRegisterCount, InstructionUtils.SseRegisterOffset);
        private int SseRegisterIdx => GetRegisterIdx(InstructionUtils.SseRegisterOffset, InstructionUtils.storageRegisters.Length);

        readonly RegisterState[] registerStates = InstructionUtils.storageRegisters.Select(x => new RegisterState()).ToArray();

        readonly (AssemblyExpr.Register.RegisterSize neededSize, int idx)[] reservedRegisterInfo =
           InstructionUtils.storageRegisters.Select(x => ((AssemblyExpr.Register.RegisterSize)0, 0)).ToArray();

        readonly StrongBox<AssemblyExpr.Register.RegisterName>?[] registers = new StrongBox<AssemblyExpr.Register.RegisterName>[InstructionUtils.storageRegisters.Length];

        public FunctionPushPreserved fncPushPreserved;

        public AssemblyExpr.Register GetRegister(int idx, AssemblyExpr.Register.RegisterSize size)
        {
            registers[idx] ??= new(InstructionUtils.storageRegisters[idx].Name);
            registerStates[idx].SetState(RegisterState.RegisterStates.Used);

            return new AssemblyExpr.Register(registers[idx], AssemblyExpr.Register.IsSseRegister(registers[idx].Value) ? AssemblyExpr.Register.RegisterSize._128Bits : size);
        }

        public AssemblyExpr.Register NextRegister(AssemblyExpr.Register.RegisterSize size, Expr.Type? type) => 
            IsFloatingType(type) ? NextSseRegister() : NextRegister(size);

        public AssemblyExpr.Register NextRegister(AssemblyExpr.Register.RegisterSize size)
        {
            if (!InstructionUtils.storageRegisters[RegisterIdx].IsScratchRegister)
            {
                fncPushPreserved.IncludeRegister(RegisterIdx);
            }
            return GetRegister(RegisterIdx, size);
        }

        public AssemblyExpr.Register NextSseRegister()
        {
            return GetRegister(SseRegisterIdx, AssemblyExpr.Register.RegisterSize._128Bits);
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
            int idx = PreservedRegisterIdx;
            fncPushPreserved.IncludeRegister(idx);
            return idx;
        }

        public void SaveScratchRegistersBeforeCall(CodeGen codeGen, int arity)
        {
            for (int i = 0; i < InstructionUtils.NonSseScratchRegisterCount; i++)
            {
                if (registerStates[i].HasState(RegisterState.RegisterStates.Free) || 
                    InstructionUtils.paramRegister[..Math.Min(arity, InstructionUtils.paramRegister.Length)].Contains(InstructionUtils.storageRegisters[i].Name))
                {
                    continue;
                }

                int idx = NextPreservedRegisterIdx();
                _ReserveRegister(codeGen, i, idx);
                registerStates[idx].SetState(RegisterState.RegisterStates.NeededPreserved);
            }
        }

        public AssemblyExpr.Register ReserveScratchRegister(CodeGen codeGen, int i, AssemblyExpr.Register.RegisterSize size)
        {
            int idx = NextPreservedRegisterIdx();
            _ReserveRegister(codeGen, i, idx);
            registerStates[idx].SetState(RegisterState.RegisterStates.NeededPreserved);
            return GetRegister(idx, size);
        }

        public void ReserveRegister(CodeGen codeGen, int i) =>
            _ReserveRegister(codeGen, i, RegisterIdx);

        private void _ReserveRegister(CodeGen assembler, int i, int newIdx)
        {
            if (registerStates[i].HasState(RegisterState.RegisterStates.Free))
            {
                return;
            }

            if (i >= InstructionUtils.SseRegisterOffset)
            {
                throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("Requesting stack memory using the RegisterAlloc class is not implemented in this version of the compiler"));
            }

            registers[newIdx] = registers[i];
            if (registerStates[i].HasState(RegisterState.RegisterStates.Needed))
            {
                assembler.assembly.text.Insert(reservedRegisterInfo[i].idx, new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, GetRegister(newIdx, reservedRegisterInfo[i].neededSize), new AssemblyExpr.Register(InstructionUtils.storageRegisters[i].Name, reservedRegisterInfo[i].neededSize)));
                registerStates[i].RemoveState(RegisterState.RegisterStates.Needed);
            }
            registers[newIdx].Value = InstructionUtils.storageRegisters[newIdx].Name;
            registerStates[newIdx].SetState(registerStates[i]);

            Free(i, true);
        }

        public AssemblyExpr.Register AllocParam(int i, AssemblyExpr.Register.RegisterSize size, AssemblyExpr.Register[] localParams, Expr.Type? type, CodeGen codeGen)
        {
            int idx = NameToIdx(IsFloatingType(type) ? InstructionUtils.storageRegisters[i + InstructionUtils.SseRegisterOffset].Name : InstructionUtils.paramRegister[i]);
            if (!registerStates[idx].HasState(RegisterState.RegisterStates.Free))
            {
                int newIdx = NextPreservedRegisterIdx();
                _ReserveRegister(codeGen, idx, newIdx);
                registerStates[newIdx].SetState(RegisterState.RegisterStates.NeededPreserved);
            }
            return localParams[i] = GetRegister(idx, size);
        }

        public AssemblyExpr.Register NeededAlloc(AssemblyExpr.Register.RegisterSize size, CodeGen codeGen, int i = 0)
        {
            (reservedRegisterInfo[i].neededSize, reservedRegisterInfo[i].idx) = (size, codeGen.assembly.text.Count);
            registerStates[i].SetState(RegisterState.RegisterStates.Needed);
            return GetRegister(i, size);
        }

        public AssemblyExpr.Register ReAllocConstructorReturnRegister(int idx)
        {
            registerStates[idx].SetState(RegisterState.RegisterStates.NeededPreserved);
            return GetRegister(idx, AssemblyExpr.Register.RegisterSize._64Bits);
        }

        public void Lock(AssemblyExpr.Register register) => Lock(NameToIdx(register.Name));
        public void Lock(int idx) => registerStates[idx].SetState(RegisterState.RegisterStates.Used, RegisterState.RegisterStates.Locked);

        public void Unlock(AssemblyExpr.Register register) => Unlock(NameToIdx(register.Name));
        public void Unlock(int idx) => registerStates[idx].RemoveState(RegisterState.RegisterStates.Locked);

        public bool IsLocked(int idx) => registerStates[idx].HasState(RegisterState.RegisterStates.Locked);
        public bool IsNeededOrNeededPreserved(int idx) =>
            registerStates[idx].HasState(RegisterState.RegisterStates.Needed) ||
            registerStates[idx].HasState(RegisterState.RegisterStates.NeededPreserved);

        public void ListAccept<T, T2>(List<T> list, Expr.IVisitor<T2> visitor)
            where T : Expr
            where T2 : AssemblyExpr.IValue
        {
            foreach (var expr in list)
            {
                Free(expr.Accept(visitor));
            }
        }

        public int NameToIdx(AssemblyExpr.Register.RegisterName name) => 
            Array.IndexOf(InstructionUtils.storageRegisters.Select(x => x.Name).ToArray(), name);

        public void FreeParameter(int i, AssemblyExpr.Register localParam, CodeGen assembler)
        {
            var regName = AssemblyExpr.Register.IsSseRegister(localParam.Name) ? 
                InstructionUtils.storageRegisters[i + InstructionUtils.SseRegisterOffset].Name : 
                InstructionUtils.paramRegister[i];

            Free(NameToIdx(regName));
            if (localParam.Name != regName)
            {
                assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, new AssemblyExpr.Register(regName, localParam.Size), localParam));
                Free(NameToIdx(localParam.Name));
            }
        }


        public void Free(AssemblyExpr.IValue? value, bool force = false)
        {
            if (value == null) return;

            if (value.IsPointer(out var ptr))
            {
                FreePtr(ptr, force);
            }
            else if (value.IsRegister(out var register))
            {
                FreeRegister(register, force);
            }
        }
        public void FreeRegister(AssemblyExpr.Register register, bool force = false) => Free(NameToIdx(register.Name), force);
        public void FreePtr(AssemblyExpr.Pointer ptr, bool force = false)
        {
            if (ptr.value.IsRegister(out var ptrReg))
            {
                FreeRegister(ptrReg, force);
            }
        }

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

        public RegisterState? SaveRegisterState(AssemblyExpr.IValue? value) 
        {
            int idx;

            if (value is not AssemblyExpr.IRegisterPointer registerPointer || (idx = NameToIdx(registerPointer.GetRegister().Name)) == -1)
            {
                return null;
            }

            var state = new RegisterState(); 
            state.SetState(registerStates[idx]);
            return state;
        }
        public void SetRegisterState(RegisterState? state,  AssemblyExpr.IValue? value)
        {
            if (state == null || value.IsLiteral())
                return;

            var registerPointer = (AssemblyExpr.IRegisterPointer)value;
            int idx = NameToIdx(registerPointer.GetRegister().Name);

            registers[idx] = registerPointer.GetRegister().nameBox;
            registerStates[idx] = (RegisterState)state;
        }
    }
}
