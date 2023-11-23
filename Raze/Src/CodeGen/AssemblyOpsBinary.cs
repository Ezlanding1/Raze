using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

internal partial class AssemblyOps
{
    CodeGen assembler;
    public List<Expr.GetReference> vars;
    public int count;

    public AssemblyOps(CodeGen assembler)
    {
        this.assembler = assembler;
    }

    internal static class Binary
    {
        public static AssemblyExpr.Register.RegisterSize? GetOpSize(AssemblyExpr.Value operand, ExprUtils.AssignableInstruction.Binary.AssignType assignType, List<Expr.GetReference> vars, int count, bool first)
        {
            if (operand.IsRegister() || operand.IsPointer())
            {
                return ((AssemblyExpr.SizedValue)operand).size;
            }

            int cOff = Convert.ToInt32(assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst))
                + Convert.ToInt32(assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond));

            if (cOff == 2)
                cOff = Convert.ToInt32(first) + 1;
            else if (cOff == 1)
                cOff += Convert.ToInt32(!first);

            if (cOff != 0)
            {
                return InstructionUtils.ToRegisterSize(vars[count - cOff].GetLastData().size);
            }

            Diagnostics.errors.Push(new Error.BackendError("Inavalid Assembly Block", $"No size could be determined for the {(first ? "first" : "second")} operand"));
            return null;
        }

        public static void ReturnOp(ref AssemblyExpr.Value operand, ExprUtils.AssignableInstruction.Binary.AssignType assignType, AssemblyOps assemblyOps, bool first)
        {
            if (((InlinedCodeGen)assemblyOps.assembler).inlineState.inline)
            {
                operand = operand.NonLiteral(GetOpSize(operand, assignType, assemblyOps.vars, assemblyOps.count, first), assemblyOps.assembler);
                ((InlinedCodeGen.InlineStateInlined)((InlinedCodeGen)assemblyOps.assembler).inlineState).callee = (AssemblyExpr.SizedValue)operand;
                ((InlinedCodeGen)assemblyOps.assembler).LockOperand((AssemblyExpr.SizedValue)operand);
            }
            else
            {
                if (operand.IsRegister())
                {
                    var op = (AssemblyExpr.Register)operand;
                    if (op.name != AssemblyExpr.Register.RegisterName.RAX)
                        assemblyOps.assembler.Emit(new AssemblyExpr.Binary("MOV", new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RAX, op.size), operand));
                }
                else if (operand.IsPointer())
                {
                    assemblyOps.assembler.Emit(new AssemblyExpr.Binary("MOV", new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RAX, ((AssemblyExpr.SizedValue)operand).size), operand));
                }
                else
                {
                    var size = GetOpSize(operand, assignType, assemblyOps.vars, assemblyOps.count, first);
                    if (size != null)
                    {
                        assemblyOps.assembler.Emit(new AssemblyExpr.Binary("MOV", new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RAX, (AssemblyExpr.Register.RegisterSize)size), operand));
                    }
                }
            }
        }

        public static AssemblyExpr.Value HandleOperand1(ExprUtils.AssignableInstruction.Binary instruction, AssemblyOps assemblyOps)
        {
            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst))
            {
                return assemblyOps.vars[assemblyOps.count].Accept(assemblyOps.assembler).NonLiteral(InstructionUtils.ToRegisterSize(assemblyOps.vars[assemblyOps.count++].GetLastData().size), assemblyOps.assembler);
            }
            return (AssemblyExpr.Value)instruction.instruction.operand1;
        }
        public static AssemblyExpr.Value HandleOperand2(ExprUtils.AssignableInstruction.Binary instruction, AssemblyExpr.Value operand1, AssemblyOps assemblyOps, bool ignoreSize=false)
        {
            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond))
            {
                var operand2 = assemblyOps.vars[assemblyOps.count++].Accept(assemblyOps.assembler);
                operand2 = (operand1.IsPointer() && operand2.IsPointer()) ? operand2.NonPointer(assemblyOps.assembler) : operand2;

                if (!operand1.IsLiteral() && !operand2.IsLiteral() && !ignoreSize)
                {
                    var op1size = ((AssemblyExpr.SizedValue)operand1).size;
                    var op2size = ((AssemblyExpr.SizedValue)operand2).size;

                    if ((int)op1size > (int)op2size)
                    {
                        AssemblyExpr.Register reg = ((AssemblyExpr.SizedValue)operand2).AsRegister(op1size, assemblyOps.assembler);

                        assemblyOps.assembler.Emit(new AssemblyExpr.Binary("MOVSX", reg, operand2));
                        operand2 = reg;
                    }
                    else if ((int)op1size < (int)op2size)
                    {
                        // WARNING: Loss of information from operand2, since the only a subsection of operand2 is used.
                        if (operand2.IsRegister())
                        {
                            operand2 = ((AssemblyExpr.Register)operand2).AsRegister(op1size, assemblyOps.assembler);
                        }
                        else
                        {
                            var ptr = (AssemblyExpr.Pointer)operand2;
                            operand2 = new AssemblyExpr.Pointer(ptr.register, ptr.offset, op1size);
                        }
                    }
                }
                return operand2;
            }
            return (AssemblyExpr.Value)instruction.instruction.operand2;
        }

        public static void DefaultBinOp(ExprUtils.AssignableInstruction.Binary instruction, AssemblyOps assemblyOps)
        {
            var operand1 = HandleOperand1(instruction, assemblyOps);
            var operand2 = HandleOperand2(instruction, operand1, assemblyOps);

            assemblyOps.assembler.Emit(new AssemblyExpr.Binary(instruction.instruction.instruction, operand1, operand2));

            if (instruction.returns && assemblyOps.assembler is InlinedCodeGen)
            {
                ReturnOp(ref operand1, instruction.assignType, assemblyOps, true);
            }

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst))
                assemblyOps.assembler.alloc.Free(operand1);

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond))
                assemblyOps.assembler.alloc.Free(operand2);
        }

        public static void CheckLEA(AssemblyExpr.Value operand1, AssemblyExpr.Value operand2)
        {
            if (!operand1.IsRegister()) { Diagnostics.errors.Push(new Error.BackendError("Invalid Assembly Block", $"'LEA' Instruction's first operand must be a register. Got {operand1.GetType().Name}")); }
            if (!operand2.IsPointer()) { Diagnostics.errors.Push(new Error.BackendError("Invalid Assembly Block", $"'LEA' Instruction's second operand must be a pointer. Got {operand2.GetType().Name}")); }
        }
        public static void LEA(ExprUtils.AssignableInstruction.Binary instruction, AssemblyOps assemblyOps)
        {
            var operand1 = HandleOperand1(instruction, assemblyOps);
            AssemblyExpr.Value? operand2 =
                (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond)) ?
                assemblyOps.vars[assemblyOps.count++].Accept(assemblyOps.assembler) :
                (AssemblyExpr.Value)instruction.instruction.operand2;

            CheckLEA(operand1, operand2);

            assemblyOps.assembler.Emit(new AssemblyExpr.Binary(instruction.instruction.instruction, operand1, operand2));

            if (instruction.returns && assemblyOps.assembler is InlinedCodeGen)
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
            var operand1 = HandleOperand1(instruction, assemblyOps).NonPointer(assemblyOps.assembler);

            var operand2 = HandleOperand2(instruction, operand1, assemblyOps);

            assemblyOps.assembler.Emit(new AssemblyExpr.Binary(instruction.instruction.instruction, operand1, operand2));

            if (instruction.returns && assemblyOps.assembler is InlinedCodeGen)
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
            var operand2 = HandleOperand2(instruction, operand1, assemblyOps, true);

            if (!operand2.IsLiteral())
            {
                if (assemblyOps.assembler.alloc.paramRegisters[3] == null)
                {
                    var cl = new AssemblyExpr.Register(InstructionUtils.paramRegister[3], AssemblyExpr.Register.RegisterSize._8Bits);

                    if (((AssemblyExpr.SizedValue)operand2).size != AssemblyExpr.Register.RegisterSize._8Bits)
                    {
                        Diagnostics.errors.Push(new Error.BackendError("Invalid Assembly Block", "Instruction's operand sizes don't match"));
                    }

                    assemblyOps.assembler.Emit(new AssemblyExpr.Binary("MOV", cl, operand2));
                    assemblyOps.assembler.Emit(new AssemblyExpr.Binary(instruction.instruction.instruction, operand1, cl));
                }
                else if (operand2.IsRegister() && !assemblyOps.assembler.alloc.IsLocked(assemblyOps.assembler.alloc.NameToIdx(((AssemblyExpr.Register)operand2).name)))
                {
                    var reg = assemblyOps.assembler.alloc.NextRegister(assemblyOps.assembler.alloc.paramRegisters[3].size);

                    assemblyOps.assembler.Emit(new AssemblyExpr.Binary("MOV", reg, assemblyOps.assembler.alloc.paramRegisters[3]));

                    assemblyOps.assembler.alloc.FreeRegister((AssemblyExpr.Register)operand2);

                    ((AssemblyExpr.Register)operand2).name = AssemblyExpr.Register.RegisterName.RCX;
                    assemblyOps.assembler.Emit(new AssemblyExpr.Binary(instruction.instruction.instruction, operand1, new AssemblyExpr.Register(InstructionUtils.paramRegister[3], ((AssemblyExpr.SizedValue)operand2).size)));
                    assemblyOps.assembler.Emit(new AssemblyExpr.Binary("MOV", assemblyOps.assembler.alloc.paramRegisters[3], reg));

                    assemblyOps.assembler.alloc.FreeRegister(reg);
                }
                else
                {
                    var reg = assemblyOps.assembler.alloc.NextRegister(assemblyOps.assembler.alloc.paramRegisters[3].size);

                    assemblyOps.assembler.Emit(new AssemblyExpr.Binary("MOV", reg, assemblyOps.assembler.alloc.paramRegisters[3]));

                    assemblyOps.assembler.Emit(new AssemblyExpr.Binary("MOV", new AssemblyExpr.Register(InstructionUtils.paramRegister[3], ((AssemblyExpr.SizedValue)operand2).size), operand2));
                    assemblyOps.assembler.Emit(new AssemblyExpr.Binary(instruction.instruction.instruction, operand1, new AssemblyExpr.Register(InstructionUtils.paramRegister[3], ((AssemblyExpr.SizedValue)operand2).size)));
                    assemblyOps.assembler.Emit(new AssemblyExpr.Binary("MOV", assemblyOps.assembler.alloc.paramRegisters[3], reg));

                    assemblyOps.assembler.alloc.FreeRegister(reg);
                }
            }

            if (instruction.returns && assemblyOps.assembler is InlinedCodeGen)
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

            var operand1 = (AssemblyExpr.Value)(instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst) ?
                    assemblyOps.assembler.MovToRegister(assemblyOps.vars[assemblyOps.count++].Accept(assemblyOps.assembler), InstructionUtils.ToRegisterSize(assemblyOps.vars[assemblyOps.count - 1].GetLastData().size)) :
                    instruction.instruction.operand1);

            var operand2 = (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond)) ?
                assemblyOps.vars[assemblyOps.count].Accept(assemblyOps.assembler).NonLiteral(InstructionUtils.ToRegisterSize(assemblyOps.vars[assemblyOps.count++].GetLastData().size), assemblyOps.assembler) :
                (AssemblyExpr.Value)instruction.instruction.operand2;

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

            var _size = GetOpSize(operand1, instruction.assignType, assemblyOps.vars, assemblyOps.count, true);
            if (_size == null) return;

            var size = (AssemblyExpr.Register.RegisterSize)_size;
            var rax = assemblyOps.assembler.alloc.CallAlloc(size);
            var rdx = new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RDX, size);

            AssemblyExpr.Register paramStoreReg = null;

            if (assemblyOps.assembler.alloc.paramRegisters[2] == null)
            {
                if (!(operand1.IsRegister() && ((AssemblyExpr.Register)operand1).name == AssemblyExpr.Register.RegisterName.RAX))
                {
                    assemblyOps.assembler.Emit(new AssemblyExpr.Binary("MOV", rax, operand1));
                }
                assemblyOps.assembler.alloc.NullReg(0);

                assemblyOps.assembler.Emit(emitOp == "DIV" ? new AssemblyExpr.Binary("XOR", new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RDX, size), new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RDX, size)) : new AssemblyExpr.Zero("CDQ"));
                assemblyOps.assembler.Emit(new AssemblyExpr.Unary(emitOp, operand2));
            }
            else
            {
                paramStoreReg = assemblyOps.assembler.alloc.NextRegister(assemblyOps.assembler.alloc.paramRegisters[2].size);
                assemblyOps.assembler.Emit(new AssemblyExpr.Binary("MOV", paramStoreReg, assemblyOps.assembler.alloc.paramRegisters[2]));

                if (!(operand1.IsRegister() && ((AssemblyExpr.Register)operand1).name == AssemblyExpr.Register.RegisterName.RAX))
                {
                    assemblyOps.assembler.Emit(new AssemblyExpr.Binary("MOV", rax, operand1));
                }
                assemblyOps.assembler.alloc.NullReg(0);

                assemblyOps.assembler.Emit(emitOp == "DIV" ? new AssemblyExpr.Binary("XOR", new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RDX, size), new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RDX, size)) : new AssemblyExpr.Zero("CDQ"));
                assemblyOps.assembler.Emit(new AssemblyExpr.Unary(emitOp, operand2));

            }

            if (instruction.returns && assemblyOps.assembler is InlinedCodeGen inlinedAssembler)
            {
                if (instruction.instruction.instruction == "IDIV" || instruction.instruction.instruction == "DIV")
                {
                    if (inlinedAssembler.inlineState.inline)
                    {
                        var ret = assemblyOps.assembler.alloc.GetRegister(0, rax.size);
                        ((InlinedCodeGen.InlineStateInlined)inlinedAssembler.inlineState).callee = ret;
                        inlinedAssembler.LockOperand(ret);
                    }
                }
                else
                {
                    assemblyOps.assembler.alloc.FreeRegister(rax);
                    var reg = assemblyOps.assembler.alloc.NextRegister(rdx.size);
                    assemblyOps.assembler.Emit(new AssemblyExpr.Binary("MOV", reg, rdx));
                    if (inlinedAssembler.inlineState.inline)
                    {
                        ((InlinedCodeGen.InlineStateInlined)inlinedAssembler.inlineState).callee = reg;
                        inlinedAssembler.LockOperand(reg);
                    }
                }
            }

            if (paramStoreReg != null)
            {
                assemblyOps.assembler.Emit(new AssemblyExpr.Binary("MOV", assemblyOps.assembler.alloc.paramRegisters[2], paramStoreReg));
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

            assemblyOps.assembler.Emit(new AssemblyExpr.Binary("CMP", operand1, operand2));

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst))
                assemblyOps.assembler.alloc.Free(operand1);

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond))
                assemblyOps.assembler.alloc.Free(operand2);

            AssemblyExpr.Value reg = assemblyOps.assembler.alloc.NextRegister(AssemblyExpr.Register.RegisterSize._8Bits);

            switch (instruction.instruction.instruction)
            {
                case "E_CMP":
                case "CMP":
                    assemblyOps.assembler.Emit(new AssemblyExpr.Unary("SETE", reg));
                    break;
                case "NE_CMP":
                    assemblyOps.assembler.Emit(new AssemblyExpr.Unary("SETNE", reg));
                    break;
                case "G_CMP":
                    assemblyOps.assembler.Emit(new AssemblyExpr.Unary("SETG", reg));
                    break;
                case "GE_CMP":
                    assemblyOps.assembler.Emit(new AssemblyExpr.Unary("SETGE", reg));
                    break;
                case "L_CMP":
                    assemblyOps.assembler.Emit(new AssemblyExpr.Unary("SETL", reg));
                    break;
                case "LE_CMP":
                    assemblyOps.assembler.Emit(new AssemblyExpr.Unary("SETLE", reg));
                    break;
            }

            if (instruction.returns && assemblyOps.assembler is InlinedCodeGen)
            {
                ReturnOp(ref reg, instruction.assignType, assemblyOps, true);
            }
        }
    }
}
