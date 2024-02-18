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
        public static void ReturnOp(ref AssemblyExpr.Value operand, ExprUtils.AssignableInstruction.Binary.AssignType assignType, AssemblyOps assemblyOps, bool first)
        {
            if (((InlinedCodeGen)assemblyOps.assembler).inlineState.inline)
            {
                var nonLiteral = operand.NonLiteral(assemblyOps.assembler);
                ((InlinedCodeGen.InlineStateInlined)((InlinedCodeGen)assemblyOps.assembler).inlineState).callee = nonLiteral;
                ((InlinedCodeGen)assemblyOps.assembler).LockOperand(nonLiteral);
            }
            else
            {
                if (operand.IsRegister())
                {
                    var op = (AssemblyExpr.Register)operand;
                    if (op.Name != AssemblyExpr.Register.RegisterName.RAX)
                        assemblyOps.assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RAX, op.Size), operand));
                }
                else
                {
                    assemblyOps.assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RAX, operand.Size), operand));
                }
            }
        }

        public static AssemblyExpr.Value HandleOperand1(ExprUtils.AssignableInstruction.Binary instruction, AssemblyOps assemblyOps)
        {
            AssemblyExpr.Value operand1;

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst))
            {
                operand1 = assemblyOps.vars[assemblyOps.count++].Accept(assemblyOps.assembler);
            }
            else
            {
                operand1 = instruction.instruction.operand1;
            }

            if (operand1.IsLiteral())
            {
                operand1 = ((AssemblyExpr.UnresolvedLiteral)operand1).CreateLiteral(InstructionUtils.ToRegisterSize(assemblyOps.vars[assemblyOps.count].GetLastData().size));
            }

            return operand1.NonLiteral(assemblyOps.assembler);
        }
        public static AssemblyExpr.Value HandleOperand2(ExprUtils.AssignableInstruction.Binary instruction, AssemblyExpr.Value operand1, AssemblyOps assemblyOps)
        {
            AssemblyExpr.Value operand2;
            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond))
            {
                operand2 = assemblyOps.vars[assemblyOps.count++].Accept(assemblyOps.assembler);
                if (operand1.IsPointer() && operand2.IsPointer())
                {
                    if (operand2.Size < AssemblyExpr.Register.RegisterSize._32Bits)
                    {
                        int varOffset = Convert.ToInt32(instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst)) + 1;
                        Expr.Type type = assemblyOps.vars[assemblyOps.vars.Count - varOffset].GetLastData().type;

                        AssemblyExpr.Register reg = ((AssemblyExpr.Pointer)operand2).AsRegister(assemblyOps.assembler);
                        assemblyOps.assembler.Emit(CodeGen.PartialRegisterOptimize(type, reg, operand2));

                        ((AssemblyExpr.RegisterPointer)operand1).size = AssemblyExpr.Register.RegisterSize._32Bits;
                        operand2 = reg;
                    }
                    else
                    {
                        operand2 = operand2.NonPointer(assemblyOps.assembler);
                    }
                }
            }
            else
            {
                operand2 = instruction.instruction.operand2;
            }

            if (operand2.IsLiteral())
            {
                operand2 = ((AssemblyExpr.UnresolvedLiteral)operand2).CreateLiteral(operand1.Size);
            }

            CheckOperandSizeMismatch(operand1, operand2);
            return operand2;
        }

        private static void CheckOperandSizeMismatch(AssemblyExpr.Value operand1, AssemblyExpr.Value operand2)
        {
            if (operand1.Size < operand2.Size)
            {
                Diagnostics.errors.Push(new Error.BackendError("Invalid Assembly Block", "Operand size mistmatch"));
            }
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
                instruction.instruction.operand2;

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
            var operand2 = (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond)) ?
                assemblyOps.vars[assemblyOps.count++].Accept(assemblyOps.assembler) :
                instruction.instruction.operand2;

            if (operand2.IsLiteral())
            {
                operand2 = ((AssemblyExpr.UnresolvedLiteral)operand2).CreateLiteral(AssemblyExpr.Register.RegisterSize._8Bits).NonLiteral(assemblyOps.assembler);
            }


            if (!operand2.IsLiteral())
            {
                if (assemblyOps.assembler.alloc.paramRegisters[3] == null)
                {
                    var cl = new AssemblyExpr.Register(InstructionUtils.paramRegister[3], AssemblyExpr.Register.RegisterSize._8Bits);

                    if (operand2.Size != AssemblyExpr.Register.RegisterSize._8Bits)
                    {
                        Diagnostics.errors.Push(new Error.BackendError("Invalid Assembly Block", "Instruction operand sizes don't match"));
                    }

                    assemblyOps.assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, cl, operand2));
                    assemblyOps.assembler.Emit(new AssemblyExpr.Binary(instruction.instruction.instruction, operand1, cl));
                }
                else if (operand2.IsRegister() && !assemblyOps.assembler.alloc.IsLocked(assemblyOps.assembler.alloc.NameToIdx(((AssemblyExpr.Register)operand2).Name)))
                {
                    var reg = assemblyOps.assembler.alloc.NextRegister(assemblyOps.assembler.alloc.paramRegisters[3].Size);

                    assemblyOps.assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, reg, assemblyOps.assembler.alloc.paramRegisters[3]));

                    assemblyOps.assembler.alloc.FreeRegister((AssemblyExpr.Register)operand2);

                    ((AssemblyExpr.Register)operand2).Name = AssemblyExpr.Register.RegisterName.RCX;
                    assemblyOps.assembler.Emit(new AssemblyExpr.Binary(instruction.instruction.instruction, operand1, new AssemblyExpr.Register(InstructionUtils.paramRegister[3], operand2.Size)));
                    assemblyOps.assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, assemblyOps.assembler.alloc.paramRegisters[3], reg));

                    assemblyOps.assembler.alloc.FreeRegister(reg);
                }
                else
                {
                    var reg = assemblyOps.assembler.alloc.NextRegister(assemblyOps.assembler.alloc.paramRegisters[3].Size);

                    assemblyOps.assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, reg, assemblyOps.assembler.alloc.paramRegisters[3]));

                    assemblyOps.assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, new AssemblyExpr.Register(InstructionUtils.paramRegister[3], operand2.Size), operand2));
                    assemblyOps.assembler.Emit(new AssemblyExpr.Binary(instruction.instruction.instruction, operand1, new AssemblyExpr.Register(InstructionUtils.paramRegister[3], operand2.Size)));
                    assemblyOps.assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, assemblyOps.assembler.alloc.paramRegisters[3], reg));

                    assemblyOps.assembler.alloc.FreeRegister(reg);
                }
            }
            else
            {
                operand2 = ((AssemblyExpr.ILiteralBase)operand2).CreateLiteral(AssemblyExpr.Register.RegisterSize._8Bits);
                assemblyOps.assembler.Emit(new AssemblyExpr.Binary(instruction.instruction.instruction, operand1, operand2));
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

            var operand1 = HandleOperand1(instruction, assemblyOps).NonPointer(assemblyOps.assembler);

            var operand2 = (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond)) ?
                assemblyOps.vars[assemblyOps.count++].Accept(assemblyOps.assembler) :
                instruction.instruction.operand2;

            if (operand2.IsLiteral())
            {
                operand2 = ((AssemblyExpr.UnresolvedLiteral)operand2).CreateLiteral(AssemblyExpr.Register.RegisterSize._8Bits).NonLiteral(assemblyOps.assembler);
            }

            AssemblyExpr.Instruction emitOp;
            switch (instruction.instruction.instruction)
            {
                case AssemblyExpr.Instruction.IDIV:
                case AssemblyExpr.Instruction.DIV:
                    emitOp = instruction.instruction.instruction;
                    break;
                case AssemblyExpr.Instruction.MOD:
                    emitOp = AssemblyExpr.Instruction.DIV;
                    break;
                case AssemblyExpr.Instruction.IMOD:
                    emitOp = AssemblyExpr.Instruction.IDIV;
                    break;
                default:
                    Diagnostics.errors.Push(new Error.ImpossibleError("Impossible instruction in IDIV_DIV_IMOD_MOD"));
                    return;
            }
            var rax = assemblyOps.assembler.alloc.CallAlloc(operand1.Size);
            var rdx = new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RDX, operand1.Size);

            AssemblyExpr.Register paramStoreReg = null;

            if (assemblyOps.assembler.alloc.paramRegisters[2] == null)
            {
                if (!(operand1.IsRegister() && ((AssemblyExpr.Register)operand1).Name == AssemblyExpr.Register.RegisterName.RAX))
                {
                    assemblyOps.assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, rax, operand1));
                }
                assemblyOps.assembler.alloc.NullReg(0);

                assemblyOps.assembler.Emit(emitOp == AssemblyExpr.Instruction.DIV ? new AssemblyExpr.Binary(AssemblyExpr.Instruction.XOR, new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RDX, operand1.Size), new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RDX, operand1.Size)) : new AssemblyExpr.Zero(AssemblyExpr.Instruction.CDQ));
                assemblyOps.assembler.Emit(new AssemblyExpr.Unary(emitOp, operand2));
            }
            else
            {
                paramStoreReg = assemblyOps.assembler.alloc.NextRegister(assemblyOps.assembler.alloc.paramRegisters[2].Size);
                assemblyOps.assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, paramStoreReg, assemblyOps.assembler.alloc.paramRegisters[2]));

                if (!(operand1.IsRegister() && ((AssemblyExpr.Register)operand1).Name == AssemblyExpr.Register.RegisterName.RAX))
                {
                    assemblyOps.assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, rax, operand1));
                }
                assemblyOps.assembler.alloc.NullReg(0);

                assemblyOps.assembler.Emit(emitOp == AssemblyExpr.Instruction.DIV ? new AssemblyExpr.Binary(AssemblyExpr.Instruction.XOR, new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RDX, operand1.Size), new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RDX, operand1.Size)) : new AssemblyExpr.Zero(AssemblyExpr.Instruction.CDQ));
                assemblyOps.assembler.Emit(new AssemblyExpr.Unary(emitOp, operand2));

            }

            if (instruction.returns && assemblyOps.assembler is InlinedCodeGen inlinedAssembler)
            {
                if (instruction.instruction.instruction == AssemblyExpr.Instruction.IDIV || instruction.instruction.instruction == AssemblyExpr.Instruction.DIV)
                {
                    if (inlinedAssembler.inlineState.inline)
                    {
                        var ret = assemblyOps.assembler.alloc.GetRegister(0, rax.Size);
                        ((InlinedCodeGen.InlineStateInlined)inlinedAssembler.inlineState).callee = ret;
                        inlinedAssembler.LockOperand(ret);
                    }
                }
                else
                {
                    assemblyOps.assembler.alloc.FreeRegister(rax);
                    var reg = assemblyOps.assembler.alloc.NextRegister(rdx.Size);
                    assemblyOps.assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, reg, rdx));
                    if (inlinedAssembler.inlineState.inline)
                    {
                        ((InlinedCodeGen.InlineStateInlined)inlinedAssembler.inlineState).callee = reg;
                        inlinedAssembler.LockOperand(reg);
                    }
                }
            }

            if (paramStoreReg != null)
            {
                assemblyOps.assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, assemblyOps.assembler.alloc.paramRegisters[2], paramStoreReg));
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

            assemblyOps.assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.CMP, operand1, operand2));

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst))
                assemblyOps.assembler.alloc.Free(operand1);

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond))
                assemblyOps.assembler.alloc.Free(operand2);

            AssemblyExpr.Value reg = assemblyOps.assembler.alloc.NextRegister(AssemblyExpr.Register.RegisterSize._8Bits);

            switch (instruction.instruction.instruction)
            {
                case AssemblyExpr.Instruction.E_CMP:
                case AssemblyExpr.Instruction.CMP:
                    assemblyOps.assembler.Emit(new AssemblyExpr.Unary(AssemblyExpr.Instruction.SETE, reg));
                    break;
                case AssemblyExpr.Instruction.NE_CMP:
                    assemblyOps.assembler.Emit(new AssemblyExpr.Unary(AssemblyExpr.Instruction.SETNE, reg));
                    break;
                case AssemblyExpr.Instruction.G_CMP:
                    assemblyOps.assembler.Emit(new AssemblyExpr.Unary(AssemblyExpr.Instruction.SETG, reg));
                    break;
                case AssemblyExpr.Instruction.GE_CMP:
                    assemblyOps.assembler.Emit(new AssemblyExpr.Unary(AssemblyExpr.Instruction.SETGE, reg));
                    break;
                case AssemblyExpr.Instruction.L_CMP:
                    assemblyOps.assembler.Emit(new AssemblyExpr.Unary(AssemblyExpr.Instruction.SETL, reg));
                    break;
                case AssemblyExpr.Instruction.LE_CMP:
                    assemblyOps.assembler.Emit(new AssemblyExpr.Unary(AssemblyExpr.Instruction.SETLE, reg));
                    break;
                case AssemblyExpr.Instruction.UG_CMP:
                    assemblyOps.assembler.Emit(new AssemblyExpr.Unary(AssemblyExpr.Instruction.SETA, reg));
                    break;
                case AssemblyExpr.Instruction.UGE_CMP:
                    assemblyOps.assembler.Emit(new AssemblyExpr.Unary(AssemblyExpr.Instruction.SETAE, reg));
                    break;
                case AssemblyExpr.Instruction.UL_CMP:
                    assemblyOps.assembler.Emit(new AssemblyExpr.Unary(AssemblyExpr.Instruction.SETB, reg));
                    break;
                case AssemblyExpr.Instruction.ULE_CMP:
                    assemblyOps.assembler.Emit(new AssemblyExpr.Unary(AssemblyExpr.Instruction.SETBE, reg));
                    break;
            }

            if (instruction.returns && assemblyOps.assembler is InlinedCodeGen)
            {
                ReturnOp(ref reg, instruction.assignType, assemblyOps, true);
            }
        }
    }
}
