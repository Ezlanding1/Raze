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

            private protected Instruction.Value FormatOperand2(Instruction.Value operand2, Instruction.Value operand1, Assembler assembler)
            {
                if (operand1.IsPointer() && operand2.IsPointer())
                {
                    assembler.emit(new Instruction.Binary("MOV", assembler.alloc.CurrentRegister(((Instruction.Pointer)operand2).size), operand2));
                    return assembler.alloc.NextRegister(((Instruction.Pointer)operand2).size);
                }
                return operand2;
            }
            private protected void HandleInstruction(string instruction, ref Instruction operand1, global::Raze.Assembler assembler)
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

            Instruction.Binary instruction;

            public AssignableInstructionBin(Instruction.Binary instruction, AssignType assignType)
            {
                this.instruction = instruction;
                this.assignType = assignType;
            }

            public override void Assign(ref int count, List<Expr.Variable> vars, Assembler assembler)
            {
                Instruction operand1 = assignType.HasFlag(AssignType.AssignFirst) ?
                    assembler.FormatOperand1(vars[count++].Accept(assembler)) :
                    instruction.operand1;

                HandleInstruction(instruction.instruction, ref operand1, assembler);

                Instruction operand2 = assignType.HasFlag(AssignType.AssignSecond) ?
                    assignType.HasFlag(AssignType.AssignFirst) ?
                        FormatOperand2(vars[count++].Accept(assembler), (Instruction.Value)operand1, assembler) :
                        vars[count++].Accept(assembler) :
                    instruction.operand2;


                assembler.emit(new Instruction.Binary(this.instruction.instruction, operand1, operand2));

                if (assignType.HasFlag(AssignType.AssignFirst))
                    assembler.alloc.Free((Instruction.Value)operand1);

                if (assignType.HasFlag(AssignType.AssignSecond))
                    assembler.alloc.Free((Instruction.Value)operand2);
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

            Instruction.Unary instruction;

            public AssignableInstructionUn(Instruction.Unary instruction, AssignType assignType)
            {
                this.instruction = instruction;
                this.assignType = assignType;
            }

            public override void Assign(ref int count, List<Expr.Variable> vars, Assembler assembler)
            {
                Instruction operand = assignType.HasFlag(AssignType.AssignFirst) ? vars[count++].Accept(assembler) : instruction.operand;

                assembler.emit(new Instruction.Unary(this.instruction.instruction, operand));

                if (assignType.HasFlag(AssignType.AssignFirst))
                    assembler.alloc.Free((Instruction.Value)operand);
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
