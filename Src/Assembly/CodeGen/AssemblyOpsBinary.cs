using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

internal partial class AssemblyOps
{
    Assembler assembler;
    public List<Expr.Variable> vars;
    public int count;

    public AssemblyOps(Assembler assembler)
    {
            this.assembler = assembler;
    }

    internal static class Binary
    {
        public static Instruction.Register.RegisterSize? GetOpSize(Instruction.Value operand, ExprUtils.AssignableInstruction.Binary.AssignType assignType, List<Expr.Variable> vars, int count, bool first)
        {
            if (operand.IsRegister() || operand.IsPointer())
            {
                return ((Instruction.SizedValue)operand).size;
            }
            if (first)
            {
                int cOff = 0;
                cOff += assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst) ? 1 : 0;
                cOff += assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond) ? 1 : 0;

                if (cOff != 0)
                {
                    return InstructionUtils.ToRegisterSize(vars[count - cOff].stack.size);
                }
            }
            else
            {
                if (assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond))
                {
                    return InstructionUtils.ToRegisterSize(vars[count - 1].stack.size);
                }
            }
            return null;
        }
        private static Instruction.Value ChangeRegister(Instruction.Value operand, Instruction.Register name, Assembler assembler)
        {
            if (operand.IsRegister())
            {
                ((Instruction.Register)operand).name = name.name;
                return operand;
            }
            else if (operand.IsPointer())
            {
                if (((Instruction.Pointer)operand).register.name == Instruction.Register.RegisterName.RBP)
                {
                    assembler.emit(new Instruction.Binary("MOV", assembler.alloc.CurrentRegister(((Instruction.Pointer)operand).size), operand));
                    return name;
                }
                else
                {
                    ((Instruction.Pointer)operand).register.name = name.name;
                    return ((Instruction.Pointer)operand).register;
                }
            }
            else
            {
                assembler.emit(new Instruction.Binary("MOV", assembler.alloc.CurrentRegister(Instruction.Register.RegisterSize._32Bits), operand));
                return assembler.alloc.NextRegister(Instruction.Register.RegisterSize._32Bits);
            }
        }
        public static void ReturnOp(ref Instruction.Value operand, ExprUtils.AssignableInstruction.Binary.AssignType assignType, AssemblyOps assemblyOps, bool first)
        {
            operand = assemblyOps.assembler.FormatOperand1(operand, GetOpSize(operand, assignType, assemblyOps.vars, assemblyOps.count, first) ?? throw new Errors.BackendError("Inavalid Assembly Block", "No size could be determined for the first operand"));
            if (((InlinedAssembler)assemblyOps.assembler).inlineState.inline)
            {
                ((InlinedAssembler.InlineStateInlined)((InlinedAssembler)assemblyOps.assembler).inlineState).callee = (Instruction.SizedValue)operand;
                ((InlinedAssembler)assemblyOps.assembler).LockOperand((Instruction.SizedValue)operand);
            }
        }
        public static Instruction.Value HandleOperand1(ExprUtils.AssignableInstruction.Binary instruction, AssemblyOps assemblyOps)
        {
            return (Instruction.Value)(instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst) ?
                    assemblyOps.assembler.FormatOperand1(assemblyOps.vars[assemblyOps.count].Accept(assemblyOps.assembler), InstructionUtils.ToRegisterSize(assemblyOps.vars[assemblyOps.count++].stack.size)) :
                    instruction.instruction.operand1);
        }
        public static Instruction.Value FormatOperand2(Instruction.Value operand2, Instruction.Value operand1, Assembler assembler)
        {
            if (operand1.IsPointer() && operand2.IsPointer())
            {
                assembler.emit(new Instruction.Binary("MOV", assembler.alloc.CurrentRegister(((Instruction.Pointer)operand2).size), operand2));
                return assembler.alloc.NextRegister(((Instruction.Pointer)operand2).size);
            }
            return operand2;
        }
        public static Instruction.Value HandleOperand2(ExprUtils.AssignableInstruction.Binary instruction, Instruction.Value operand1, AssemblyOps assemblyOps)
        {
            return (Instruction.Value)(instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond) ?
                    instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst) ?
                        FormatOperand2(assemblyOps.vars[assemblyOps.count++].Accept(assemblyOps.assembler), operand1, assemblyOps.assembler) :
                        assemblyOps.vars[assemblyOps.count++].Accept(assemblyOps.assembler) :
                    instruction.instruction.operand2);
        }

        public static void DefaultBinOp(ExprUtils.AssignableInstruction.Binary instruction, AssemblyOps assemblyOps)
        {
            var operand1 = HandleOperand1(instruction, assemblyOps);
            var operand2 = HandleOperand2(instruction, operand1, assemblyOps);

            assemblyOps.assembler.emit(new Instruction.Binary(instruction.instruction.instruction, operand1, operand2));

            if (instruction.returns && assemblyOps.assembler is InlinedAssembler)
            {
                ReturnOp(ref operand1, instruction.assignType, assemblyOps, true);
            }

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst))
                assemblyOps.assembler.alloc.Free(operand1);

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond))
                assemblyOps.assembler.alloc.Free(operand2);
        }

        public static void IMUL(ExprUtils.AssignableInstruction.Binary instruction, AssemblyOps assemblyOps)
        {
            var operand1 = HandleOperand1(instruction, assemblyOps);

            operand1 = assemblyOps.assembler.PassByValue(operand1);

            var operand2 = HandleOperand2(instruction, operand1, assemblyOps);

            assemblyOps.assembler.emit(new Instruction.Binary(instruction.instruction.instruction, operand1, operand2));

            if (instruction.returns && assemblyOps.assembler is InlinedAssembler)
            {
                ReturnOp(ref operand1, instruction.assignType, assemblyOps, true);
            }

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst))
                assemblyOps.assembler.alloc.Free(operand1);

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond))
                assemblyOps.assembler.alloc.Free(operand2);
        }

        public static void SAL_SAR(ExprUtils.AssignableInstruction.Binary instruction, AssemblyOps assemblyOps)
        {
            var operand1 = HandleOperand1(instruction, assemblyOps);
            var operand2 = HandleOperand2(instruction, operand1, assemblyOps);

            if (!operand2.IsLiteral())
            {
                if (assemblyOps.assembler.alloc.paramRegisters[3] == null)
                {
                    var cl = new Instruction.Register(InstructionUtils.paramRegister[3], Instruction.Register.RegisterSize._8Bits);

                    if (((Instruction.SizedValue)operand2).size != Instruction.Register.RegisterSize._8Bits)
                    {
                        throw new Errors.BackendError("Invalid Assembly Block", "Instruction's operand sizes don't match");
                    }

                    assemblyOps.assembler.emit(new Instruction.Binary("MOV", cl, operand2));
                    assemblyOps.assembler.emit(new Instruction.Binary(instruction.instruction.instruction, operand1, cl));
                }
                else if (operand2.IsRegister() && !assemblyOps.assembler.alloc.IsLocked(assemblyOps.assembler.alloc.NameToIdx(((Instruction.Register)operand2).name)))
                {
                    var reg = assemblyOps.assembler.alloc.NextRegister(assemblyOps.assembler.alloc.paramRegisters[3].size);

                    assemblyOps.assembler.emit(new Instruction.Binary("MOV", reg, assemblyOps.assembler.alloc.paramRegisters[3]));

                    assemblyOps.assembler.alloc.FreeRegister((Instruction.Register)operand2);

                    ((Instruction.Register)operand2).name = Instruction.Register.RegisterName.RCX;
                    assemblyOps.assembler.emit(new Instruction.Binary(instruction.instruction.instruction, operand1, new Instruction.Register(InstructionUtils.paramRegister[3], ((Instruction.SizedValue)operand2).size)));
                    assemblyOps.assembler.emit(new Instruction.Binary("MOV", assemblyOps.assembler.alloc.paramRegisters[3], reg));

                    assemblyOps.assembler.alloc.FreeRegister(reg);
                }
                else
                {
                    var reg = assemblyOps.assembler.alloc.NextRegister(assemblyOps.assembler.alloc.paramRegisters[3].size);

                    assemblyOps.assembler.emit(new Instruction.Binary("MOV", reg, assemblyOps.assembler.alloc.paramRegisters[3]));

                    assemblyOps.assembler.emit(new Instruction.Binary("MOV", new Instruction.Register(InstructionUtils.paramRegister[3], ((Instruction.SizedValue)operand2).size), operand2));
                    assemblyOps.assembler.emit(new Instruction.Binary(instruction.instruction.instruction, operand1, new Instruction.Register(InstructionUtils.paramRegister[3], ((Instruction.SizedValue)operand2).size)));
                    assemblyOps.assembler.emit(new Instruction.Binary("MOV", assemblyOps.assembler.alloc.paramRegisters[3], reg));

                    assemblyOps.assembler.alloc.FreeRegister(reg);
                }
            }

            if (instruction.returns && assemblyOps.assembler is InlinedAssembler)
            {
                ReturnOp(ref operand1, instruction.assignType, assemblyOps, true);
            }

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst))
                assemblyOps.assembler.alloc.Free(operand1);

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond))
                assemblyOps.assembler.alloc.Free(operand2);
        }

        public static void IDIV_DIV_IMOD_MOD(ExprUtils.AssignableInstruction.Binary instruction, AssemblyOps assemblyOps)
        {
            var operand1 = (Instruction.Value)(instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst) ?
                    assemblyOps.vars[assemblyOps.count++].Accept(assemblyOps.assembler) :
                    instruction.instruction.operand1);

            var operand2 = HandleOperand1(instruction, assemblyOps);

            assemblyOps.assembler.alloc.ReserveRegister(assemblyOps.assembler);

            string emitOp = "";
            switch (instruction.instruction.instruction)
            {
                case "IDIV":
                case "DIV":
                    emitOp = instruction.instruction.instruction;
                    break;
                case "MOD":
                    emitOp = "DIV";
                    break;
                case "IMOD":
                    emitOp = "IDIV";
                    break;
            }
            assemblyOps.assembler.alloc.raxNeeded = true;

            var size = GetOpSize(operand1, instruction.assignType, assemblyOps.vars, assemblyOps.count, true) ?? throw new Errors.BackendError("Inavalid Assembly Block", "No size could be determined for the first operand");
            
            var rax = assemblyOps.assembler.alloc.GetRegister(0, size);
            var rdx = new Instruction.Register(Instruction.Register.RegisterName.RDX, size);
            Instruction.Register paramStoreReg = null;

            if (assemblyOps.assembler.alloc.paramRegisters[2] == null)
            {   
                if (!(operand1.IsRegister() && ((Instruction.Register)operand1).name == Instruction.Register.RegisterName.RAX))
                {
                    assemblyOps.assembler.emit(new Instruction.Binary("MOV", rax, operand1));
                }
                assemblyOps.assembler.alloc.NullReg(0);

                assemblyOps.assembler.emit(emitOp == "DIV" ? new Instruction.Binary("XOR", new Instruction.Register(Instruction.Register.RegisterName.RDX, size), new Instruction.Register(Instruction.Register.RegisterName.RDX, size)) :  new Instruction.Zero("CDQ"));
                assemblyOps.assembler.emit(new Instruction.Unary(emitOp, operand2));
            }
            else
            {
                paramStoreReg = assemblyOps.assembler.alloc.NextRegister(assemblyOps.assembler.alloc.paramRegisters[2].size);
                assemblyOps.assembler.emit(new Instruction.Binary("MOV", paramStoreReg, assemblyOps.assembler.alloc.paramRegisters[2]));

                if (!(operand1.IsRegister() && ((Instruction.Register)operand1).name == Instruction.Register.RegisterName.RAX))
                {
                    assemblyOps.assembler.emit(new Instruction.Binary("MOV", rax, operand1));
                }
                assemblyOps.assembler.alloc.NullReg(0);

                assemblyOps.assembler.emit(emitOp == "DIV" ? new Instruction.Binary("XOR", new Instruction.Register(Instruction.Register.RegisterName.RDX, size), new Instruction.Register(Instruction.Register.RegisterName.RDX, size)) : new Instruction.Zero("CDQ"));
                assemblyOps.assembler.emit(new Instruction.Unary(emitOp, operand2));

            }

            if (instruction.returns && assemblyOps.assembler is InlinedAssembler)
            {
                if (instruction.instruction.instruction == "IDIV" || instruction.instruction.instruction == "DIV")
                {
                    if (((InlinedAssembler)assemblyOps.assembler).inlineState.inline)
                    {
                        var ret = assemblyOps.assembler.alloc.GetRegister(0, rax.size);
                        ((InlinedAssembler.InlineStateInlined)((InlinedAssembler)assemblyOps.assembler).inlineState).callee = ret;
                        ((InlinedAssembler)assemblyOps.assembler).LockOperand(ret);
                    }
                }
                else
                {
                    assemblyOps.assembler.alloc.FreeRegister(rax);
                    var reg = assemblyOps.assembler.alloc.NextRegister(rdx.size);
                    assemblyOps.assembler.emit(new Instruction.Binary("MOV", reg, rdx));
                    if (((InlinedAssembler)assemblyOps.assembler).inlineState.inline)
                    {
                        ((InlinedAssembler.InlineStateInlined)((InlinedAssembler)assemblyOps.assembler).inlineState).callee = reg;
                        ((InlinedAssembler)assemblyOps.assembler).LockOperand(reg);
                    }
                }
            }

            if (paramStoreReg != null)
            {
                assemblyOps.assembler.emit(new Instruction.Binary("MOV", assemblyOps.assembler.alloc.paramRegisters[2], paramStoreReg));
                assemblyOps.assembler.alloc.FreeRegister(paramStoreReg);
            }

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst))
                assemblyOps.assembler.alloc.Free(operand1);

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond))
                assemblyOps.assembler.alloc.Free(operand2);

        }
    }
}
