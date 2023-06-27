using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze
{
    internal abstract partial class ExprUtils
    {
        public abstract class AssignableInstruction
        {
            public abstract void Assign(ref int count, List<Expr.Variable> vars, Assembler assembler);
            public abstract bool HasReturn();

            private protected Instruction.Value FormatOperand2(Instruction.Value operand2, Instruction.Value operand1, Assembler assembler)
            {
                if (operand1.IsPointer() && operand2.IsPointer())
                {
                    assembler.emit(new Instruction.Binary("MOV", assembler.alloc.CurrentRegister(((Instruction.Pointer)operand2).size), operand2));
                    return assembler.alloc.NextRegister(((Instruction.Pointer)operand2).size);
                }
                return operand2;
            }
            private protected void HandleInstruction(string instruction, ref Instruction operand1, Assembler assembler)
            {
                switch (instruction)
                {
                    case "IMUL":
                        operand1 = assembler.PassByValue((Instruction.Value)operand1);
                        break;
                    default:
                        break;
                }
            }
        }

        public class AssignableInstructionBin : AssignableInstruction
        {
            [Flags]
            public enum AssignType
            {
                AssignNone = 0,
                AssignFirst = 1,
                AssignSecond = 2
            }
            AssignType assignType;

            public enum ReturnType
            {
                ReturnNone = 0,
                ReturnFirst = 1,
                ReturnSecond = 2
            }
            ReturnType returnType;

            Instruction.Binary instruction;

            public AssignableInstructionBin(Instruction.Binary instruction, AssignType assignType, int returnType)
            {
                this.instruction = instruction;
                this.assignType = assignType;
                this.returnType = (ReturnType)returnType;
            }

            public override void Assign(ref int count, List<Expr.Variable> vars, Assembler assembler)
            {
                Instruction operand1 = assignType.HasFlag(AssignType.AssignFirst) ?
                    assembler.FormatOperand1(vars[count].Accept(assembler), InstructionUtils.ToRegisterSize(vars[count++].stack.size)) :
                    instruction.operand1;

                HandleInstruction(instruction.instruction, ref operand1, assembler);

                Instruction operand2 = assignType.HasFlag(AssignType.AssignSecond) ?
                    assignType.HasFlag(AssignType.AssignFirst) ?
                        FormatOperand2(vars[count++].Accept(assembler), (Instruction.Value)operand1, assembler) :
                        vars[count++].Accept(assembler) :
                    instruction.operand2;

                if (returnType == ReturnType.ReturnFirst && assembler is InlinedAssembler)
                {
                    operand1 = assembler.FormatOperand1((Instruction.Value)operand1, ((Instruction.Value)operand1).IsLiteral()? (assignType.HasFlag(AssignType.AssignFirst)? InstructionUtils.ToRegisterSize(vars[count-(assignType.HasFlag(AssignType.AssignSecond) ? 2 : 1)].stack.size) : throw new Errors.BackendError("Inavalid Assembly Block", "No size could be determined for the first operand")) : null);
                    if (((InlinedAssembler)assembler).inlineState.inline)
                    {
                        ((InlinedAssembler.InlineStateInlined)((InlinedAssembler)assembler).inlineState).callee = (Instruction.SizedValue)operand1;
                        ((InlinedAssembler)assembler).LockOperand((Instruction.SizedValue)operand1);
                    }
                }
                else if (returnType == ReturnType.ReturnSecond && assembler is InlinedAssembler)
                {
                    operand2 = assembler.FormatOperand1((Instruction.Value)operand2, ((Instruction.Value)operand2).IsLiteral() ? (assignType.HasFlag(AssignType.AssignSecond) ? InstructionUtils.ToRegisterSize(vars[count - 1].stack.size) : throw new Errors.BackendError("Inavalid Assembly Block", "No size could be determined for the first operand")) : null);
                    if (((InlinedAssembler)assembler).inlineState.inline)
                    {
                        ((InlinedAssembler.InlineStateInlined)((InlinedAssembler)assembler).inlineState).callee = (Instruction.SizedValue)operand2;
                    }
                    ((InlinedAssembler)assembler).LockOperand((Instruction.SizedValue)operand2);
                }

                assembler.emit(new Instruction.Binary(this.instruction.instruction, operand1, operand2));

                if (assignType.HasFlag(AssignType.AssignFirst))
                    assembler.alloc.Free((Instruction.Value)operand1);

                if (assignType.HasFlag(AssignType.AssignSecond))
                    assembler.alloc.Free((Instruction.Value)operand2);
            }

            public override bool HasReturn()
            {
                return returnType != ReturnType.ReturnNone;
            }
        }
        public class AssignableInstructionUn : AssignableInstruction
        {
            [Flags]
            public enum AssignType
            {
                AssignNone = 0,
                AssignFirst = 1
            }
            AssignType assignType;

            public enum ReturnType
            {
                ReturnNone = 0,
                ReturnFirst = 1,
            }
            ReturnType returnType;

            Instruction.Unary instruction;

            public AssignableInstructionUn(Instruction.Unary instruction, AssignType assignType, int returnType)
            {
                this.instruction = instruction;
                this.assignType = assignType;
                this.returnType = (ReturnType)returnType;
            }

            public override void Assign(ref int count, List<Expr.Variable> vars, Assembler assembler)
            {
                Instruction operand = assignType.HasFlag(AssignType.AssignFirst) ? vars[count++].Accept(assembler) : instruction.operand;

                assembler.emit(new Instruction.Unary(this.instruction.instruction, operand));

                if (returnType == ReturnType.ReturnFirst && assembler is InlinedAssembler)
                {
                    operand = assembler.FormatOperand1((Instruction.Value)operand, ((Instruction.Value)operand).IsLiteral() ? (assignType.HasFlag(AssignType.AssignFirst) ? InstructionUtils.ToRegisterSize(vars[count - 1].stack.size) : throw new Errors.BackendError("Inavalid Assembly Block", "No size could be determined for the first operand")) : null);
                    if (((InlinedAssembler)assembler).inlineState.inline)
                    {
                        ((InlinedAssembler.InlineStateInlined)((InlinedAssembler)assembler).inlineState).callee = (Instruction.SizedValue)operand;
                        ((InlinedAssembler)assembler).LockOperand((Instruction.SizedValue)operand);
                    }
                }

                if (assignType.HasFlag(AssignType.AssignFirst))
                    assembler.alloc.Free((Instruction.Value)operand);
            }

            public override bool HasReturn()
            {
                return returnType != ReturnType.ReturnNone;
            }
        }
        public class AssignableInstructionZ : AssignableInstruction
        {
            public Instruction.Zero instruction;

            public AssignableInstructionZ(Instruction.Zero instruction)
            {
                this.instruction = instruction;
            }

            public override void Assign(ref int count, List<Expr.Variable> vars, Assembler assembler)
            {
                assembler.emit(this.instruction);
            }

            public override bool HasReturn()
            {
                return false;
            }
        }

        public class Modifiers
        {
            private (string, bool)[] modifiers;

            public Modifiers(params string[] modifierNames) 
            {
                this.modifiers = new (string, bool)[modifierNames.Length];

                for (int i = 0; i < modifierNames.Length; i++)
                {
                    modifiers[i] = new(modifierNames[i], false);
                }
            }

            public IEnumerable<string> EnumerateTrueModifiers()
            {
                return modifiers.Where(modifier => modifier.Item2).Select(modifier => modifier.Item1);
            }

            public bool ContainsModifier(string s)
            {
                for (int i = 0; i < modifiers.Length; i++)
                {
                    if (modifiers[i].Item1 == s)
                    {
                        return true;
                    }
                }
                return false;
            }

            private int GetModifier(string s)
            {
                for (int i = 0; i < modifiers.Length; i++)
                {
                    if (modifiers[i].Item1 == s)
                    {
                        return i;
                    }
                }
                throw new Errors.ImpossibleError("Requested parameter not found");
            }

            public bool this[string s]
            {
                get { return modifiers[GetModifier(s)].Item2; }
                set { modifiers[GetModifier(s)].Item2 = value; }
            }
        }
    }
}
