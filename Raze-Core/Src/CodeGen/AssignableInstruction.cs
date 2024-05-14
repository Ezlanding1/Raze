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


        public class Binary(AssemblyExpr.Binary instruction, Binary.AssignType assignType, bool returns) : AssignableInstruction
        {
            [Flags]
            public enum AssignType
            {
                AssignNone = 0,
                AssignFirst = 1,
                AssignSecond = 2
            }
            public AssignType assignType = assignType;
            public bool returns = returns;
            public AssemblyExpr.Binary instruction = instruction;

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
                        AssemblyOps.Binary.DefaultOp(this, assemblyOps);
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
                        Diagnostics.Report(new Diagnostic.BackendDiagnostic(Diagnostic.DiagnosticName.UnsupportedInstruction, instruction.instruction));
                        return;
                }
            }

            public override (int, int) GetAssigningVars()
            {
                int variablesUsed = ((int)assignType == 1 || (int)assignType == 2) ? 1 : ((int)assignType == 3) ? 2 : 0;

                return instruction.instruction switch
                {
                    AssemblyExpr.Instruction.MOV or 
                    AssemblyExpr.Instruction.ADD or 
                    AssemblyExpr.Instruction.SUB or 
                    AssemblyExpr.Instruction.AND or 
                    AssemblyExpr.Instruction.OR or 
                    AssemblyExpr.Instruction.XOR or 
                    AssemblyExpr.Instruction.LEA or
                    AssemblyExpr.Instruction.SAL or
                    AssemblyExpr.Instruction.SAR => (variablesUsed, (int)AssignType.AssignFirst),                                                                                                                                                                                                                                                                                                                                       //case "MOD":
                    _ => (variablesUsed, (int)AssignType.AssignNone),
                };
            }

            public override bool HasReturn() => returns;
        }

        public class Unary(AssemblyExpr.Unary instruction, Unary.AssignType assignType, bool returns) : AssignableInstruction
        {
            [Flags]
            public enum AssignType
            {
                AssignNone = 0,
                AssignFirst = 1
            }
            public AssignType assignType = assignType;
            public bool returns = returns;

            public AssemblyExpr.Unary instruction = instruction;

            public override void Assign(AssemblyOps assemblyOps)
            {
                switch (instruction.instruction)
                {
                    case AssemblyExpr.Instruction.INC:
                    case AssemblyExpr.Instruction.DEC:
                        AssemblyOps.Unary.DefaultOp(this, assemblyOps);
                        return;
                    case AssemblyExpr.Instruction.DEREF:
                        AssemblyOps.Unary.DEREF(this, assemblyOps);
                        return;
                    case AssemblyExpr.Instruction.RETURN:
                        AssemblyOps.Unary.RETURN(this, assemblyOps);
                        break;
                    default:
                        Diagnostics.Report(new Diagnostic.BackendDiagnostic(Diagnostic.DiagnosticName.UnsupportedInstruction, instruction.instruction));
                        break;
                }
            }

            public override (int, int) GetAssigningVars()
            {
                int variablesUsed = ((int)assignType == 1) ? 1 : 0;

                return instruction.instruction switch
                {
                    AssemblyExpr.Instruction.INC or 
                    AssemblyExpr.Instruction.DEC => (variablesUsed, (int)AssignType.AssignFirst),
                    _ => (variablesUsed, (int)AssignType.AssignNone),
                };
            }

            public override bool HasReturn() => returns;
        }

        public class Nullary(AssemblyExpr.Nullary instruction) : AssignableInstruction
        {
            public AssemblyExpr.Nullary instruction = instruction;

            public override void Assign(AssemblyOps assemblyOps)
            {
                switch (instruction.instruction)
                {
                    case AssemblyExpr.Instruction.SYSCALL:
                        AssemblyOps.Nullary.DefaultOp(this, assemblyOps);
                        break;
                    default:
                        Diagnostics.Report(new Diagnostic.BackendDiagnostic(Diagnostic.DiagnosticName.UnsupportedInstruction, instruction.instruction));
                        break;
                }
            }

            public override (int, int) GetAssigningVars() => (0, 0);

            public override bool HasReturn() => false;
        }
    }
}
