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
        public static Instruction.Register.RegisterSize GetOpSize(Instruction.Value operand, ExprUtils.AssignableInstruction.Binary.AssignType assignType, List<Expr.Variable> vars, int count, bool first)
        {
            if (operand.IsRegister() || operand.IsPointer())
            {
                return ((Instruction.SizedValue)operand).size;
            }

            int cOff = Convert.ToInt32(assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst)) 
                + Convert.ToInt32(assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond));

            cOff += Convert.ToInt32(!first);

            if (cOff != 0)
            {
                return InstructionUtils.ToRegisterSize(vars[count - cOff].Stack.size);
            }

            throw new Errors.BackendError("Inavalid Assembly Block", $"No size could be determined for the { (first? "first" : "second") } operand");
        }

        public static void ReturnOp(ref Instruction.Value operand, ExprUtils.AssignableInstruction.Binary.AssignType assignType, AssemblyOps assemblyOps, bool first)
        {
            operand = assemblyOps.assembler.NonLiteral(operand, GetOpSize(operand, assignType, assemblyOps.vars, assemblyOps.count, first));
            if (((InlinedAssembler)assemblyOps.assembler).inlineState.inline)
            {
                ((InlinedAssembler.InlineStateInlined)((InlinedAssembler)assemblyOps.assembler).inlineState).callee = (Instruction.SizedValue)operand;
                ((InlinedAssembler)assemblyOps.assembler).LockOperand((Instruction.SizedValue)operand);
            }
        }

        public static Instruction.Value HandleOperand1(ExprUtils.AssignableInstruction.Binary instruction, AssemblyOps assemblyOps)
        {
            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst))
            {
                return assemblyOps.assembler.NonLiteral(assemblyOps.vars[assemblyOps.count].Accept(assemblyOps.assembler), InstructionUtils.ToRegisterSize(assemblyOps.vars[assemblyOps.count++].Stack.size));
            }
            return (Instruction.Value)instruction.instruction.operand1;
        }
        public static Instruction.Value HandleOperand2(ExprUtils.AssignableInstruction.Binary instruction, Instruction.Value operand1, AssemblyOps assemblyOps)
        {
            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond))
            {
                var operand2 = assemblyOps.vars[assemblyOps.count++].Accept(assemblyOps.assembler);

                return (operand1.IsPointer() && operand2.IsPointer()) ? assemblyOps.assembler.NonPointer(operand2) : operand2;
            }
            return (Instruction.Value)instruction.instruction.operand2;     
        }

        public static void DefaultBinOp(ExprUtils.AssignableInstruction.Binary instruction, AssemblyOps assemblyOps)
        {
            var operand1 = HandleOperand1(instruction, assemblyOps);
            var operand2 = HandleOperand2(instruction, operand1, assemblyOps);

            assemblyOps.assembler.Emit(new Instruction.Binary(instruction.instruction.instruction, operand1, operand2));

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
            var operand1 = assemblyOps.assembler.NonPointer(HandleOperand1(instruction, assemblyOps));

            var operand2 = HandleOperand2(instruction, operand1, assemblyOps);

            assemblyOps.assembler.Emit(new Instruction.Binary(instruction.instruction.instruction, operand1, operand2));

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

                    assemblyOps.assembler.Emit(new Instruction.Binary("MOV", cl, operand2));
                    assemblyOps.assembler.Emit(new Instruction.Binary(instruction.instruction.instruction, operand1, cl));
                }
                else if (operand2.IsRegister() && !assemblyOps.assembler.alloc.IsLocked(assemblyOps.assembler.alloc.NameToIdx(((Instruction.Register)operand2).name)))
                {
                    var reg = assemblyOps.assembler.alloc.NextRegister(assemblyOps.assembler.alloc.paramRegisters[3].size);

                    assemblyOps.assembler.Emit(new Instruction.Binary("MOV", reg, assemblyOps.assembler.alloc.paramRegisters[3]));

                    assemblyOps.assembler.alloc.FreeRegister((Instruction.Register)operand2);

                    ((Instruction.Register)operand2).name = Instruction.Register.RegisterName.RCX;
                    assemblyOps.assembler.Emit(new Instruction.Binary(instruction.instruction.instruction, operand1, new Instruction.Register(InstructionUtils.paramRegister[3], ((Instruction.SizedValue)operand2).size)));
                    assemblyOps.assembler.Emit(new Instruction.Binary("MOV", assemblyOps.assembler.alloc.paramRegisters[3], reg));

                    assemblyOps.assembler.alloc.FreeRegister(reg);
                }
                else
                {
                    var reg = assemblyOps.assembler.alloc.NextRegister(assemblyOps.assembler.alloc.paramRegisters[3].size);

                    assemblyOps.assembler.Emit(new Instruction.Binary("MOV", reg, assemblyOps.assembler.alloc.paramRegisters[3]));

                    assemblyOps.assembler.Emit(new Instruction.Binary("MOV", new Instruction.Register(InstructionUtils.paramRegister[3], ((Instruction.SizedValue)operand2).size), operand2));
                    assemblyOps.assembler.Emit(new Instruction.Binary(instruction.instruction.instruction, operand1, new Instruction.Register(InstructionUtils.paramRegister[3], ((Instruction.SizedValue)operand2).size)));
                    assemblyOps.assembler.Emit(new Instruction.Binary("MOV", assemblyOps.assembler.alloc.paramRegisters[3], reg));

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
            assemblyOps.assembler.alloc.ReserveRegister(assemblyOps.assembler);

            var operand1 = (Instruction.Value)(instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst) ?
                    assemblyOps.assembler.MovToRegister(assemblyOps.vars[assemblyOps.count++].Accept(assemblyOps.assembler), InstructionUtils.ToRegisterSize(assemblyOps.vars[assemblyOps.count-1].Stack.size)) :
                    instruction.instruction.operand1);

            var operand2 = HandleOperand1(instruction, assemblyOps);

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

            var size = GetOpSize(operand1, instruction.assignType, assemblyOps.vars, assemblyOps.count, true);
            
            var rax = assemblyOps.assembler.alloc.CallAlloc(size);
            var rdx = new Instruction.Register(Instruction.Register.RegisterName.RDX, size);
            
            Instruction.Register paramStoreReg = null;

            if (assemblyOps.assembler.alloc.paramRegisters[2] == null)
            {
                if (!(operand1.IsRegister() && ((Instruction.Register)operand1).name == Instruction.Register.RegisterName.RAX))
                {
                    assemblyOps.assembler.Emit(new Instruction.Binary("MOV", rax, operand1));
                }
                assemblyOps.assembler.alloc.NullReg(0);

                assemblyOps.assembler.Emit(emitOp == "DIV" ? new Instruction.Binary("XOR", new Instruction.Register(Instruction.Register.RegisterName.RDX, size), new Instruction.Register(Instruction.Register.RegisterName.RDX, size)) :  new Instruction.Zero("CDQ"));
                assemblyOps.assembler.Emit(new Instruction.Unary(emitOp, operand2));
            }
            else
            {
                paramStoreReg = assemblyOps.assembler.alloc.NextRegister(assemblyOps.assembler.alloc.paramRegisters[2].size);
                assemblyOps.assembler.Emit(new Instruction.Binary("MOV", paramStoreReg, assemblyOps.assembler.alloc.paramRegisters[2]));

                if (!(operand1.IsRegister() && ((Instruction.Register)operand1).name == Instruction.Register.RegisterName.RAX))
                {
                    assemblyOps.assembler.Emit(new Instruction.Binary("MOV", rax, operand1));
                }
                assemblyOps.assembler.alloc.NullReg(0);

                assemblyOps.assembler.Emit(emitOp == "DIV" ? new Instruction.Binary("XOR", new Instruction.Register(Instruction.Register.RegisterName.RDX, size), new Instruction.Register(Instruction.Register.RegisterName.RDX, size)) : new Instruction.Zero("CDQ"));
                assemblyOps.assembler.Emit(new Instruction.Unary(emitOp, operand2));

            }

            if (instruction.returns && assemblyOps.assembler is InlinedAssembler inlinedAssembler)
            {
                if (instruction.instruction.instruction == "IDIV" || instruction.instruction.instruction == "DIV")
                {
                    if (inlinedAssembler.inlineState.inline)
                    {
                        var ret = assemblyOps.assembler.alloc.GetRegister(0, rax.size);
                        ((InlinedAssembler.InlineStateInlined)inlinedAssembler.inlineState).callee = ret;
                        inlinedAssembler.LockOperand(ret);
                    }
                }
                else
                {
                    assemblyOps.assembler.alloc.FreeRegister(rax);
                    var reg = assemblyOps.assembler.alloc.NextRegister(rdx.size);
                    assemblyOps.assembler.Emit(new Instruction.Binary("MOV", reg, rdx));
                    if (inlinedAssembler.inlineState.inline)
                    {
                        ((InlinedAssembler.InlineStateInlined)inlinedAssembler.inlineState).callee = reg;
                        inlinedAssembler.LockOperand(reg);
                    }
                }
            }

            if (paramStoreReg != null)
            {
                assemblyOps.assembler.Emit(new Instruction.Binary("MOV", assemblyOps.assembler.alloc.paramRegisters[2], paramStoreReg));
                assemblyOps.assembler.alloc.FreeRegister(paramStoreReg);
            }

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst))
                assemblyOps.assembler.alloc.Free(operand1);

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond))
                assemblyOps.assembler.alloc.Free(operand2);

        }

        public static void CMP(ExprUtils.AssignableInstruction.Binary instruction, AssemblyOps assemblyOps)
        {
            var operand1 = HandleOperand1(instruction, assemblyOps);
            var operand2 = HandleOperand2(instruction, operand1, assemblyOps);

            assemblyOps.assembler.Emit(new Instruction.Binary("CMP", operand1, operand2));

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst))
                assemblyOps.assembler.alloc.Free(operand1);

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond))
                assemblyOps.assembler.alloc.Free(operand2);

            Instruction.Value reg = assemblyOps.assembler.alloc.NextRegister(Instruction.Register.RegisterSize._8Bits);

            switch (instruction.instruction.instruction)
            {
                case "E_CMP":
                case "CMP":
                    assemblyOps.assembler.Emit(new Instruction.Unary("SETE", reg));
                    break;
                case "NE_CMP":
                    assemblyOps.assembler.Emit(new Instruction.Unary("SETNE", reg));
                    break;
                case "G_CMP":
                    assemblyOps.assembler.Emit(new Instruction.Unary("SETG", reg));
                    break;
                case "GE_CMP":
                    assemblyOps.assembler.Emit(new Instruction.Unary("SETGE", reg));
                    break;
                case "L_CMP":
                    assemblyOps.assembler.Emit(new Instruction.Unary("SETL", reg));
                    break;
                case "LE_CMP":
                    assemblyOps.assembler.Emit(new Instruction.Unary("SETLE", reg));
                    break;
            }

            if (instruction.returns && assemblyOps.assembler is InlinedAssembler)
            {
                ReturnOp(ref reg, instruction.assignType, assemblyOps, true);
            }
        }
    }
}
