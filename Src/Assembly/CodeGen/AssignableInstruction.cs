using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

internal abstract partial class ExprUtils
{
    public abstract class AssignableInstruction
    {
        public abstract void Assign(AssemblyOps assemblyOps);
        public abstract bool HasReturn();


        public class Binary : AssignableInstruction
        {
            [Flags]
            public enum AssignType
            {
                AssignNone = 0,
                AssignFirst = 1,
                AssignSecond = 2
            }
            public AssignType assignType;

            public bool returns;

            public Instruction.Binary instruction;

            public Binary(Instruction.Binary instruction, AssignType assignType, bool returns)
            {
                this.instruction = instruction;
                this.assignType = assignType;
                this.returns = returns;
            }

            public override void Assign(AssemblyOps assemblyOps)
            {
                switch (instruction.instruction)
                {
                    case "MOV":
                    case "ADD":
                    case "SUB":
                    case "AND":
                    case "OR":
                    case "XOR":
                        AssemblyOps.Binary.DefaultBinOp(this, assemblyOps);
                        return;
                    case "IMUL":
                        AssemblyOps.Binary.IMUL(this, assemblyOps);
                        return;
                    case "SAL":
                    case "SAR":
                        AssemblyOps.Binary.SAL_SAR(this, assemblyOps);
                        return;
                    case "IDIV":
                    case "DIV":
                    case "IMOD":
                    case "MOD":
                        AssemblyOps.Binary.IDIV_DIV_IMOD_MOD(this, assemblyOps);
                        return;
                    case "E_CMP":
                    case "CMP":
                    case "NE_CMP":
                    case "G_CMP":
                    case "GE_CMP":
                    case "L_CMP":
                    case "LE_CMP":
                        AssemblyOps.Binary.CMP(this, assemblyOps);
                        return;
                    default:
                        Diagnostics.errors.Push(new Error.BackendError("Invalid Assembly Block", $"Instruction '{instruction.instruction}' not supported"));
                        return;
                }
            }

            public override bool HasReturn()
            {
                return returns;
            }
        }

        public class Unary : AssignableInstruction
        {
            [Flags]
            public enum AssignType
            {
                AssignNone = 0,
                AssignFirst = 1
            }
            public AssignType assignType;

            public bool returns;

            public Instruction.Unary instruction;

            public Unary(Instruction.Unary instruction, AssignType assignType, bool returns)
            {
                this.instruction = instruction;
                this.assignType = assignType;
                this.returns = returns;
            }

            public override void Assign(AssemblyOps assemblyOps)
            {
                switch (instruction.instruction)
                {
                    case "INC":
                        AssemblyOps.Unary.DefaultUnOp(this, assemblyOps);
                        return;
                    default:
                        Diagnostics.errors.Push(new Error.BackendError("Invalid Assembly Block", $"Instruction '{instruction.instruction}' not supported"));
                        break;
                }
            }

            public override bool HasReturn()
            {
                return returns;
            }
        }

        public class AssignableInstructionZ : AssignableInstruction
        {
            public Instruction.Zero instruction;

            public AssignableInstructionZ(Instruction.Zero instruction)
            {
                this.instruction = instruction;
            }

            public override void Assign(AssemblyOps assemblyOps)
            {
                switch (instruction.instruction)
                {
                    default:
                        Diagnostics.errors.Push(new Error.BackendError("Invalid Assembly Block", $"Instruction '{instruction.instruction}' not supported"));
                        break;
                }
            }

            public override bool HasReturn()
            {
                return false;
            }
        }
    }
}
