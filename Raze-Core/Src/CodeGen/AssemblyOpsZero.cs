using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

internal partial class AssemblyOps
{
    internal class Nullary
    {
        public static void DefaultOp(ExprUtils.AssignableInstruction.Nullary instruction, AssemblyOps assemblyOps)
        {
            assemblyOps.assembler.Emit(new AssemblyExpr.Nullary(instruction.instruction.instruction));
        }
    }
}
