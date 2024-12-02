﻿using System;
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

        public FunctionPushPreserved fncPushPreserved = new(0);

        public AssemblyExpr.Register GetRegister(AssemblyExpr.Register.RegisterName name, AssemblyExpr.Register.RegisterSize size) =>
            GetRegister(NameToIdx(name), size);

        private AssemblyExpr.Register GetRegister(int idx, AssemblyExpr.Register.RegisterSize size)
        {
            registers[idx] ??= new(InstructionUtils.storageRegisters[idx]);
            registerStates[idx].SetState(RegisterState.RegisterStates.Used);

            return new AssemblyExpr.Register(registers[idx], AssemblyExpr.Register.IsSseRegister(registers[idx].Value) ? AssemblyExpr.Register.RegisterSize._128Bits : size);
        }

        // Returns and allocates next register
        public AssemblyExpr.Register NextRegister(AssemblyExpr.Register.RegisterSize size, Expr.Type? type) => 
            IsFloatingType(type) ? NextSseRegister() : NextRegister(size);

        public AssemblyExpr.Register NextRegister(AssemblyExpr.Register.RegisterSize size)
        {
            var register = InstructionUtils.storageRegisters[RegisterIdx];

            if (!InstructionUtils.IsScratchRegister(register))
            {
                fncPushPreserved.IncludeRegister(register);
            }
            return GetRegister(RegisterIdx, size);
        }

        public AssemblyExpr.Register NextSseRegister()
        {
            return GetRegister(SseRegisterIdx, AssemblyExpr.Register.RegisterSize._128Bits);
        }

        // Makes all subsequent register requests for register[idx] pull from a new instance (while not modifying used[idx])
        public void NullReg() => registers[RegisterIdx] = null;
        public void NullReg(AssemblyExpr.Register.RegisterName name) => registers[NameToIdx(name)] = null;

        // Returns next register without allocation
        public AssemblyExpr.Register CurrentRegister(AssemblyExpr.Register.RegisterSize size, Expr.Type? type) =>
            IsFloatingType(type) ? CurrentSseRegister() : CurrentRegister(size);

        public AssemblyExpr.Register CurrentRegister(AssemblyExpr.Register.RegisterSize size)
        {
            return new AssemblyExpr.Register(InstructionUtils.storageRegisters[RegisterIdx], size);
        }
        public AssemblyExpr.Register CurrentSseRegister()
        {
            return new AssemblyExpr.Register(InstructionUtils.storageRegisters[SseRegisterIdx], AssemblyExpr.Register.RegisterSize._128Bits);
        }

        private int NextPreservedRegisterIdx()
        {
            int idx = PreservedRegisterIdx;
            fncPushPreserved.IncludeRegister(InstructionUtils.storageRegisters[idx]);
            return idx;
        }

        public void SaveScratchRegistersBeforeCall(CodeGen codeGen, Expr.Function function)
        {
            var cconv = InstructionUtils.GetCallingConvention(function.callingConvention);

            var scratchRegisters = InstructionUtils.storageRegisters.Except(
                cconv.nonVolatileRegisters
            );

            var usedParameters = Enumerable.Range(0, function.Arity).Select(i => cconv.paramRegisters.GetRegisters(IsFloatingType(function.parameters[i].stack.type))[i]);

            foreach (var register in scratchRegisters)
            {
                if (registerStates[NameToIdx(register)].HasState(RegisterState.RegisterStates.Free) ||
                    usedParameters.Contains(register))
                {
                    continue;
                }

                int idx = NextPreservedRegisterIdx();
                _ReserveRegister(codeGen, NameToIdx(register), idx);
                registerStates[idx].SetState(RegisterState.RegisterStates.NeededPreserved);
            }
        }

        public AssemblyExpr.Register ReserveScratchRegister(CodeGen codeGen, AssemblyExpr.Register.RegisterName name, AssemblyExpr.Register.RegisterSize size)
        {
            if (registerStates[NameToIdx(name)].HasState(RegisterState.RegisterStates.Free))
            {
                return GetRegister(NameToIdx(name), size);
            }

            int i = NameToIdx(name);
            int idx = NextPreservedRegisterIdx();
            _ReserveRegister(codeGen, i, idx);
            registerStates[idx].SetState(RegisterState.RegisterStates.NeededPreserved);
            return GetRegister(idx, size);
        }

        public void ReserveRegister(CodeGen codeGen, AssemblyExpr.Register.RegisterName name) =>
            _ReserveRegister(codeGen, NameToIdx(name), RegisterIdx);


        public AssemblyExpr.Register ReserveRegister(CodeGen codeGen, AssemblyExpr.Register.RegisterName name, AssemblyExpr.Register.RegisterSize size)
        {
            var regIdx = RegisterIdx;
            _ReserveRegister(codeGen, NameToIdx(name), RegisterIdx);
            return GetRegister(regIdx, size);
        }

        private void _ReserveRegister(CodeGen assembler, int i, int newIdx)
        {
            if (registerStates[i].HasState(RegisterState.RegisterStates.Free) || registers[i] == null)
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
                reservedRegisterInfo.ToList().ForEach(x => { if (x.idx < reservedRegisterInfo[i].idx) x.idx++; });

                var saveNeededRegExpr = 
                    new AssemblyExpr.Binary(
                        AssemblyExpr.Instruction.MOV, 
                        GetRegister(newIdx, reservedRegisterInfo[i].neededSize), 
                        new AssemblyExpr.Register(InstructionUtils.storageRegisters[i], reservedRegisterInfo[i].neededSize)
                    );


                if (reservedRegisterInfo[i].idx <= assembler.assembly.text.Count)
                    assembler.assembly.text.Insert(reservedRegisterInfo[i].idx, saveNeededRegExpr);
                else
                    assembler.assembly.text.Add(saveNeededRegExpr);

                registerStates[i].RemoveState(RegisterState.RegisterStates.Needed);
            }
            registers[newIdx].Value = InstructionUtils.storageRegisters[newIdx];
            registerStates[newIdx].SetState(registerStates[i]);

            Free(i, true);
        }

        public AssemblyExpr.Register AllocParam(int i, AssemblyExpr.Register.RegisterSize size, AssemblyExpr.Register[] localParams, Expr.Type? type, Expr.Function.CallingConvention cconv, CodeGen codeGen)
        {
            int idx = NameToIdx(InstructionUtils.GetParamRegisters(IsFloatingType(type), cconv)[i]);
            if (!registerStates[idx].HasState(RegisterState.RegisterStates.Free))
            {
                int newIdx = NextPreservedRegisterIdx();
                _ReserveRegister(codeGen, idx, newIdx);
                registerStates[newIdx].SetState(RegisterState.RegisterStates.NeededPreserved);
            }
            return localParams[i] = GetRegister(idx, size);
        }

        public AssemblyExpr.Register NeededAlloc(AssemblyExpr.Register.RegisterSize size, CodeGen codeGen, AssemblyExpr.Register.RegisterName name)
            => NeededAlloc(size, codeGen, NameToIdx(name));

        private AssemblyExpr.Register NeededAlloc(AssemblyExpr.Register.RegisterSize size, CodeGen codeGen, int i)
        {
            (reservedRegisterInfo[i].neededSize, reservedRegisterInfo[i].idx) = (size, codeGen.assembly.text.Count);
            registerStates[i].SetState(RegisterState.RegisterStates.Needed);
            return GetRegister(i, size);
        }

        public AssemblyExpr.Register ReAllocConstructorReturnRegister(AssemblyExpr.Register.RegisterName name)
        {
            int idx = NameToIdx(name);
            registerStates[idx].SetState(RegisterState.RegisterStates.NeededPreserved);
            return GetRegister(idx, AssemblyExpr.Register.RegisterSize._64Bits);
        }

        public void Lock(AssemblyExpr.Register register) => Lock(NameToIdx(register.Name));
        public void Lock(int idx) => registerStates[idx].SetState(RegisterState.RegisterStates.Used, RegisterState.RegisterStates.Locked);

        public void Unlock(AssemblyExpr.Register register) => Unlock(NameToIdx(register.Name));
        public void Unlock(int idx) => registerStates[idx].RemoveState(RegisterState.RegisterStates.Locked);

        public bool IsLocked(AssemblyExpr.Register.RegisterName name) => registerStates[NameToIdx(name)].HasState(RegisterState.RegisterStates.Locked);
        public bool IsNeededOrNeededPreserved(AssemblyExpr.Register.RegisterName name) =>
            registerStates[NameToIdx(name)].HasState(RegisterState.RegisterStates.Needed) ||
            registerStates[NameToIdx(name)].HasState(RegisterState.RegisterStates.NeededPreserved);

        public void ListAccept<T, T2>(List<T> list, Expr.IVisitor<T2> visitor)
            where T : Expr
            where T2 : AssemblyExpr.IValue
        {
            foreach (var expr in list)
            {
                Free(expr.Accept(visitor));
            }
        }

        private int NameToIdx(AssemblyExpr.Register.RegisterName name) => 
            Array.IndexOf(InstructionUtils.storageRegisters.Select(x => x).ToArray(), name);

        public void FreeParameter(int i, AssemblyExpr.Register localParam, Expr.Function.CallingConvention cconv, CodeGen assembler)
        {
            var regName = InstructionUtils.GetParamRegisters(AssemblyExpr.Register.IsSseRegister(localParam.Name), cconv)[i];

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
            if (ptr.value != null)
            {
                FreeRegister(ptr.value, force);
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

            if (value is not AssemblyExpr.IRegisterPointer registerPointer || (idx = NameToIdx(registerPointer.GetRegister()?.Name ?? (AssemblyExpr.Register.RegisterName)(-1))) == -1)
            {
                return null;
            }

            var state = new RegisterState(); 
            state.SetState(registerStates[idx]);
            return state;
        }

        public void SetRegisterState(RegisterState? state, ref AssemblyExpr.IValue? value, InlinedCodeGen codeGen)
        {
            if (state == null || value.IsLiteral())
                return;

            var register = ((AssemblyExpr.IRegisterPointer)value).GetRegister()!;
            int idx = NameToIdx(register.Name);

            registerStates[idx] = (RegisterState)state;

            if (codeGen.inlineState != null && codeGen.inlineState.secondJump)
            {
                registers[idx] = (register = new AssemblyExpr.Register(register.Name, register.Size)).nameBox;

                if (value.IsRegister())
                    value = register;

                codeGen.alloc.NeededAlloc(value.Size, codeGen, idx);
            }
            else
            {
                registers[idx] = register.nameBox;
            }
        }
    }
}
