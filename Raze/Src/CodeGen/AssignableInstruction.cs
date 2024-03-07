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
        public abstract (int, int) GetAssigningVars();
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

            public AssemblyExpr.Binary instruction;

            public Binary(AssemblyExpr.Binary instruction, AssignType assignType, bool returns)
            {
                this.instruction = instruction;
                this.assignType = assignType;
                this.returns = returns;
            }

            public override void Assign(AssemblyOps assemblyOps)
            {
                switch (instruction.instruction)
                {
                    case AssemblyExpr.Instruction.MOV:
                    case AssemblyExpr.Instruction.ADD:
                    case AssemblyExpr.Instruction.SUB:
                    case AssemblyExpr.Instruction.AND:
                    case AssemblyExpr.Instruction.OR:
                    case AssemblyExpr.Instruction.XOR:
                        AssemblyOps.Binary.DefaultBinOp(this, assemblyOps);
                        return;
                    case AssemblyExpr.Instruction.LEA:
                        AssemblyOps.Binary.LEA(this, assemblyOps);
                        return;
                    case AssemblyExpr.Instruction.IMUL:
                        AssemblyOps.Binary.IMUL(this, assemblyOps);
                        return;
                    case AssemblyExpr.Instruction.SAL:
                    case AssemblyExpr.Instruction.SAR:
                        AssemblyOps.Binary.SAL_SAR(this, assemblyOps);
                        return;
                    case AssemblyExpr.Instruction.IDIV:
                    case AssemblyExpr.Instruction.DIV:
                    case AssemblyExpr.Instruction.IMOD:
                    case AssemblyExpr.Instruction.MOD:
                        AssemblyOps.Binary.IDIV_DIV_IMOD_MOD(this, assemblyOps);
                        return;
                    case AssemblyExpr.Instruction.E_CMP:
                    case AssemblyExpr.Instruction.CMP:
                    case AssemblyExpr.Instruction.NE_CMP:
                    case AssemblyExpr.Instruction.G_CMP:
                    case AssemblyExpr.Instruction.GE_CMP:
                    case AssemblyExpr.Instruction.L_CMP:
                    case AssemblyExpr.Instruction.LE_CMP:
                    case AssemblyExpr.Instruction.UG_CMP:
                    case AssemblyExpr.Instruction.UGE_CMP:
                    case AssemblyExpr.Instruction.UL_CMP:
                    case AssemblyExpr.Instruction.ULE_CMP:
                        AssemblyOps.Binary.CMP(this, assemblyOps);
                        return;
                    default:
                        Diagnostics.ReportError(new Error.BackendError("Invalid Assembly Block", $"Instruction '{instruction.instruction}' not supported"));
                        return;
                }
            }

            public override (int, int) GetAssigningVars()
            {
                int variablesUsed = ((int)assignType == 1 || (int)assignType == 2) ? 1 : ((int)assignType == 3) ? 2 : 0;

                switch (instruction.instruction)
                {
                    case AssemblyExpr.Instruction.MOV:
                    case AssemblyExpr.Instruction.ADD:
                    case AssemblyExpr.Instruction.SUB:
                    case AssemblyExpr.Instruction.AND:
                    case AssemblyExpr.Instruction.OR:
                    case AssemblyExpr.Instruction.XOR:
                    case AssemblyExpr.Instruction.LEA:
                    //case "IMUL":
                    case AssemblyExpr.Instruction.SAL:
                    case AssemblyExpr.Instruction.SAR:
                    //case "IDIV":
                    //case "DIV":
                    //case "IMOD":
                    //case "MOD":
                        return (variablesUsed, (int)AssignType.AssignFirst);
                    default:
                        return (variablesUsed, (int)AssignType.AssignNone);
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

            public AssemblyExpr.Unary instruction;

            public Unary(AssemblyExpr.Unary instruction, AssignType assignType, bool returns)
            {
                this.instruction = instruction;
                this.assignType = assignType;
                this.returns = returns;
            }

            public override void Assign(AssemblyOps assemblyOps)
            {
                switch (instruction.instruction)
                {
                    case AssemblyExpr.Instruction.INC:
                    case AssemblyExpr.Instruction.DEC:
                        AssemblyOps.Unary.DefaultUnOp(this, assemblyOps);
                        return;
                    case AssemblyExpr.Instruction.DEREF:
                        AssemblyOps.Unary.DEREF(this, assemblyOps);
                        return;
                    default:
                        Diagnostics.ReportError(new Error.BackendError("Invalid Assembly Block", $"Instruction '{instruction.instruction}' not supported"));
                        break;
                }
            }

            public override (int, int) GetAssigningVars()
            {
                int variablesUsed = ((int)assignType == 1) ? 1 : 0;

                switch (instruction.instruction)
                {
                    case AssemblyExpr.Instruction.INC:
                    case AssemblyExpr.Instruction.DEC:
                    //case "DEREF":
                        return (variablesUsed, (int)AssignType.AssignFirst);
                    default:
                        return (variablesUsed, (int)AssignType.AssignNone);
                }
            }

            public override bool HasReturn()
            {
                return returns;
            }
        }

        public class Zero : AssignableInstruction
        {
            public AssemblyExpr.Zero instruction;

            public Zero(AssemblyExpr.Zero instruction)
            {
                this.instruction = instruction;
            }

            public override void Assign(AssemblyOps assemblyOps)
            {
                switch (instruction.instruction)
                {
                    case AssemblyExpr.Instruction.SYSCALL:
                        AssemblyOps.Zero.DefaultZOp(this, assemblyOps);
                        break;
                    default:
                        Diagnostics.ReportError(new Error.BackendError("Invalid Assembly Block", $"Instruction '{instruction.instruction}' not supported"));
                        break;
                }
            }

            public override (int, int) GetAssigningVars()
            {
                int variablesUsed = 0;

                switch (instruction.instruction)
                {
                    default:
                        return (variablesUsed, 0);
                }
            }

            public override bool HasReturn()
            {
                return false;
            }
        }
    }
}
