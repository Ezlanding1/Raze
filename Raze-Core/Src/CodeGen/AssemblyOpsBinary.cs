namespace Raze;

internal partial class AssemblyOps
{
    CodeGen assembler;
    public List<(AssemblyExpr.Register.RegisterSize, Expr.GetReference)> vars;
    public int count;

    public AssemblyOps(CodeGen assembler)
    {
        this.assembler = assembler;
    }

    internal static class Binary
    {
        public static void ReturnOp(ref AssemblyExpr.IValue operand, AssemblyOps assemblyOps, Expr.Type? type)
        {
            if (((InlinedCodeGen)assemblyOps.assembler).inlineState.inline)
            {
                var nonLiteral = operand.NonLiteral(assemblyOps.assembler, type);
                ((InlinedCodeGen.InlineStateInlined)((InlinedCodeGen)assemblyOps.assembler).inlineState).callee = nonLiteral;
                ((InlinedCodeGen)assemblyOps.assembler).LockOperand(nonLiteral);
            }
            else
            {
                (var instruction, var _returnRegister) = CodeGen.IsFloatingType(type) ?
                    (AssemblyExpr.Instruction.MOVSS, new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.XMM0, AssemblyExpr.Register.RegisterSize._128Bits)) :
                    (AssemblyExpr.Instruction.MOV, new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RAX, operand.Size));

                if (operand.IsRegister(out var op))
                {
                    if (op.Name != _returnRegister.Name)
                        assemblyOps.assembler.Emit(new AssemblyExpr.Binary(instruction, _returnRegister, operand));
                }
                else
                {
                    assemblyOps.assembler.Emit(new AssemblyExpr.Binary(instruction, _returnRegister, operand));
                }
            }
        }

        public static AssemblyExpr.IValue HandleOperand1Unsafe(ExprUtils.AssignableInstruction.Binary instruction, AssemblyOps assemblyOps)
        {
            return instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst) ?
                Unary.CreateOperand(assemblyOps).IfLiteralCreateLiteral(InstructionUtils.ToRegisterSize(assemblyOps.vars[assemblyOps.count - 1].Item2.GetLastSize())) :
                instruction.instruction.operand1.IfLiteralCreateLiteral(AssemblyExpr.Register.RegisterSize._64Bits);
        }
        public static AssemblyExpr.IValue HandleOperand1(ExprUtils.AssignableInstruction.Binary instruction, AssemblyOps assemblyOps)
        {
            return HandleOperand1Unsafe(instruction, assemblyOps).NonLiteral(assemblyOps.assembler, instruction.GetOp1TypeUnInc(assemblyOps));
        }

        public static AssemblyExpr.IValue HandleOperand2Unsafe(ExprUtils.AssignableInstruction.Binary instruction, AssemblyExpr.IValue operand1, AssemblyOps assemblyOps)
        {
            return instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond) ?
                Unary.CreateOperand(assemblyOps).IfLiteralCreateLiteral(InstructionUtils.ToRegisterSize(assemblyOps.vars[assemblyOps.count - 1].Item2.GetLastSize())) :
                instruction.instruction.operand2.IfLiteralCreateLiteral(operand1.Size);
        }
        public static AssemblyExpr.IValue HandleOperand2(ExprUtils.AssignableInstruction.Binary instruction, ref AssemblyExpr.IValue operand1, AssemblyOps assemblyOps)
        {
            AssemblyExpr.IValue operand2 = HandleOperand2Unsafe(instruction, operand1, assemblyOps);

            if (operand1.IsPointer(out var ptr1) && operand2.IsPointer(out var ptr2))
            {
                if (operand2.Size < AssemblyExpr.Register.RegisterSize._32Bits && 
                    instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst) &&
                    instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond))
                {
                    AssemblyExpr.Register reg2 = ptr1.AsRegister(assemblyOps.assembler);
                    assemblyOps.assembler.Emit(CodeGen.PartialRegisterOptimize(instruction.GetOp1Type(assemblyOps), reg2, operand1));
                    operand1 = reg2;

                    AssemblyExpr.Register op2Reg = ptr2.AsRegister(assemblyOps.assembler);
                    assemblyOps.assembler.Emit(CodeGen.PartialRegisterOptimize(instruction.GetOp2Type(assemblyOps), op2Reg, operand2));
                    operand2 = op2Reg;
                }
                else
                {
                    operand2 = operand2.NonPointer(assemblyOps.assembler, instruction.GetOp2Type(assemblyOps));
                }
            }

            CheckOperandSizeMismatch(operand1, operand2);
            return operand2;
        }

        private static void CheckOperandSizeMismatch(AssemblyExpr.IValue operand1, AssemblyExpr.IValue operand2)
        {
            if (operand1.Size != operand2.Size)
            {
                Diagnostics.Report(new Diagnostic.BackendDiagnostic(Diagnostic.DiagnosticName.InstructionOperandsSizeMismatch));
            }
        }

        public static void DefaultOp(ExprUtils.AssignableInstruction.Binary instruction, AssemblyOps assemblyOps)
        {
            var operand1 = HandleOperand1(instruction, assemblyOps);
            var operand2 = HandleOperand2(instruction, ref operand1, assemblyOps);

            assemblyOps.assembler.Emit(new AssemblyExpr.Binary(instruction.instruction.instruction, operand1, operand2));

            if (instruction.returns && assemblyOps.assembler is InlinedCodeGen)
            {
                ReturnOp(ref operand1, assemblyOps, null);
            }

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst))
                assemblyOps.assembler.alloc.Free(operand1);

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond))
                assemblyOps.assembler.alloc.Free(operand2);
        }

        public static void CheckLEA(AssemblyExpr.IValue operand1, AssemblyExpr.IValue operand2)
        {
            if (!operand1.IsRegister()) { Diagnostics.Report(new Diagnostic.BackendDiagnostic(Diagnostic.DiagnosticName.InvalidInstructionOperandType_Arity2, "LEA", "first", operand1.GetType().Name)); }
            if (!operand2.IsPointer()) { Diagnostics.Report(new Diagnostic.BackendDiagnostic(Diagnostic.DiagnosticName.InvalidInstructionOperandType_Arity2, "LEA", "second", operand1.GetType().Name)); }
        }
        public static void LEA(ExprUtils.AssignableInstruction.Binary instruction, AssemblyOps assemblyOps)
        {
            var operand1 = HandleOperand1(instruction, assemblyOps);
            AssemblyExpr.IValue? operand2 =
                (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond)) ?
                assemblyOps.vars[assemblyOps.count++].Item2.Accept(assemblyOps.assembler) :
                instruction.instruction.operand2;

            CheckLEA(operand1, operand2);

            assemblyOps.assembler.Emit(new AssemblyExpr.Binary(instruction.instruction.instruction, operand1, operand2));

            if (instruction.returns && assemblyOps.assembler is InlinedCodeGen)
            {
                ReturnOp(ref operand1, assemblyOps, null);
            }

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst))
                assemblyOps.assembler.alloc.Free(operand1);

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond))
                assemblyOps.assembler.alloc.Free(operand2);
        }

        public static void IMUL(ExprUtils.AssignableInstruction.Binary instruction, AssemblyOps assemblyOps)
        {
            AssemblyExpr.IValue operand1 = HandleOperand1(instruction, assemblyOps).NonPointer(assemblyOps.assembler, null);
            var operand2 = HandleOperand2(instruction, ref operand1, assemblyOps);

            assemblyOps.assembler.Emit(new AssemblyExpr.Binary(instruction.instruction.instruction, operand1, operand2));

            if (instruction.returns && assemblyOps.assembler is InlinedCodeGen)
            {
                ReturnOp(ref operand1, assemblyOps, null);
            }

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst))
                assemblyOps.assembler.alloc.Free(operand1);

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond))
                assemblyOps.assembler.alloc.Free(operand2);
        }

        public static void SAL_SAR(ExprUtils.AssignableInstruction.Binary instruction, AssemblyOps assemblyOps)
        {
            var operand1 = HandleOperand1(instruction, assemblyOps);
            var operand2 = HandleOperand2Unsafe(instruction, operand1, assemblyOps).NonLiteral(assemblyOps.assembler, null);

            if (!operand2.IsLiteral())
            {
                assemblyOps.assembler.alloc.ReserveRegister(assemblyOps.assembler, AssemblyExpr.Register.RegisterName.RCX);
                var cl = new AssemblyExpr.Register(InstructionUtils.paramRegister[3], AssemblyExpr.Register.RegisterSize._8Bits);

                if (operand2.Size != AssemblyExpr.Register.RegisterSize._8Bits)
                {
                    Diagnostics.Report(new Diagnostic.BackendDiagnostic(Diagnostic.DiagnosticName.InstructionOperandsSizeMismatch));
                }

                assemblyOps.assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, cl, operand2));
                assemblyOps.assembler.Emit(new AssemblyExpr.Binary(instruction.instruction.instruction, operand1, cl));
            }
            else
            {
                assemblyOps.assembler.Emit(new AssemblyExpr.Binary(instruction.instruction.instruction, operand1, operand2));
            }

            if (instruction.returns && assemblyOps.assembler is InlinedCodeGen)
            {
                ReturnOp(ref operand1, assemblyOps, null);
            }

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst))
                assemblyOps.assembler.alloc.Free(operand1);

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond))
                assemblyOps.assembler.alloc.Free(operand2);
        }

        public static void IDIV_DIV_IMOD_MOD(ExprUtils.AssignableInstruction.Binary instruction, AssemblyOps assemblyOps)
        {
            if (HandleOperand1Unsafe(instruction, assemblyOps) is not AssemblyExpr.IRegisterPointer rp || rp.GetRegister().Name != AssemblyExpr.Register.RegisterName.RAX)
            {
                assemblyOps.assembler.alloc.ReserveRegister(assemblyOps.assembler, AssemblyExpr.Register.RegisterName.RAX);
            }
            assemblyOps.count--;

            var operand1 = HandleOperand1(instruction, assemblyOps).NonPointer(assemblyOps.assembler, null);
            var operand2 = HandleOperand2Unsafe(instruction, operand1, assemblyOps).NonLiteral(assemblyOps.assembler, null);

            var emitOp = instruction.instruction.instruction switch
            {
                AssemblyExpr.Instruction.IDIV or AssemblyExpr.Instruction.DIV => instruction.instruction.instruction,
                AssemblyExpr.Instruction.MOD => AssemblyExpr.Instruction.DIV,
                AssemblyExpr.Instruction.IMOD => AssemblyExpr.Instruction.IDIV,
                _ => throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("Impossible instruction in IDIV_DIV_IMOD_MOD")),
            };

            var rax = assemblyOps.assembler.alloc.GetRegister(AssemblyExpr.Register.RegisterName.RAX, operand1.Size);
            assemblyOps.assembler.alloc.ReserveRegister(assemblyOps.assembler, AssemblyExpr.Register.RegisterName.RDX);
            var rdx = assemblyOps.assembler.alloc.GetRegister(AssemblyExpr.Register.RegisterName.RDX, operand1.Size);

            if (!(operand1.IsRegister(out var register) && register.Name == AssemblyExpr.Register.RegisterName.RAX))
            {
                assemblyOps.assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, rax, operand1));
            }

            assemblyOps.assembler.Emit(emitOp == AssemblyExpr.Instruction.DIV ? 
                new AssemblyExpr.Binary(AssemblyExpr.Instruction.XOR, rdx, rdx) : 
                new AssemblyExpr.Nullary(AssemblyExpr.Instruction.CDQ)
            );

            assemblyOps.assembler.Emit(new AssemblyExpr.Unary(emitOp, operand2));

            assemblyOps.assembler.alloc.NullReg(AssemblyExpr.Register.RegisterName.RAX);
            assemblyOps.assembler.alloc.NullReg(AssemblyExpr.Register.RegisterName.RDX);

            if (instruction.returns && assemblyOps.assembler is InlinedCodeGen inlinedAssembler)
            {
                if (instruction.instruction.instruction == AssemblyExpr.Instruction.IDIV || instruction.instruction.instruction == AssemblyExpr.Instruction.DIV)
                {
                    assemblyOps.assembler.alloc.FreeRegister(rdx);
                    if (inlinedAssembler.inlineState.inline)
                    {
                        var ret = assemblyOps.assembler.alloc.GetRegister(0, rax.Size);
                        ((InlinedCodeGen.InlineStateInlined)inlinedAssembler.inlineState).callee = ret;
                        inlinedAssembler.LockOperand(ret);
                    }
                    assemblyOps.assembler.alloc.NeededAlloc(operand1.Size, assemblyOps.assembler, AssemblyExpr.Register.RegisterName.RAX);
                }
                else
                {
                    assemblyOps.assembler.alloc.FreeRegister(rax);

                    if (inlinedAssembler.inlineState.inline)
                    {
                        var ret = assemblyOps.assembler.alloc.GetRegister(AssemblyExpr.Register.RegisterName.RDX, rdx.Size);
                        ((InlinedCodeGen.InlineStateInlined)inlinedAssembler.inlineState).callee = ret;
                        inlinedAssembler.LockOperand(ret);
                    }
                    assemblyOps.assembler.alloc.NeededAlloc(operand1.Size, assemblyOps.assembler, AssemblyExpr.Register.RegisterName.RDX);
                }
            }

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst))
                assemblyOps.assembler.alloc.Free(operand1);

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond))
                assemblyOps.assembler.alloc.Free(operand2);
        }

        public static void CMP(ExprUtils.AssignableInstruction.Binary instruction, AssemblyOps assemblyOps)
        {
            var operand1 = HandleOperand1(instruction, assemblyOps);
            var operand2 = HandleOperand2(instruction, ref operand1, assemblyOps);

            assemblyOps.assembler.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.CMP, operand1, operand2));

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst))
                assemblyOps.assembler.alloc.Free(operand1);

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond))
                assemblyOps.assembler.alloc.Free(operand2);

            AssemblyExpr.IValue reg = assemblyOps.assembler.alloc.NextRegister(AssemblyExpr.Register.RegisterSize._8Bits);

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
                ReturnOp(ref reg, assemblyOps, null);
            }
        }


        public static void DefaultFloatingOp(ExprUtils.AssignableInstruction.Binary instruction, AssemblyOps assemblyOps)
        {
            AssemblyExpr.IValue operand1 = HandleOperand1(instruction, assemblyOps)
                .NonPointerNonLiteral(assemblyOps.assembler, instruction.GetOp1TypeUnInc(assemblyOps));

            var operand2 = HandleOperand2Unsafe(instruction, operand1, assemblyOps);

            if (operand2.IsLiteral(out var literal))
            {
                operand2 = ((AssemblyExpr.Literal)literal).PutFloatLiteralOnDataSection(assemblyOps.assembler);
            }

            assemblyOps.assembler.Emit(new AssemblyExpr.Binary(instruction.instruction.instruction, operand1, operand2));

            if (instruction.returns && assemblyOps.assembler is InlinedCodeGen)
            {
                ReturnOp(ref operand1, assemblyOps, instruction.GetOp1Type(assemblyOps));
            }

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst))
                assemblyOps.assembler.alloc.Free(operand1);

            if (instruction.assignType.HasFlag(ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond))
                assemblyOps.assembler.alloc.Free(operand2);
        }
    }
}
