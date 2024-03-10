using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

internal partial class AssemblyOps
{
    internal class Zero
    {
        public static void DefaultZOp(ExprUtils.AssignableInstruction.Zero instruction, AssemblyOps assemblyOps)
        {
            assemblyOps.assembler.Emit(new AssemblyExpr.Zero(instruction.instruction.instruction));
        }
    }
}
