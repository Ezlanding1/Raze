using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Assembler
{
    internal partial class Encoder
    {
        internal static Dictionary<AssemblyExpr.Register.RegisterName, Instruction.ModRegRm.RegisterCode> exprRegisterToModRegRmRegister = new()
        {
            { AssemblyExpr.Register.RegisterName.RAX, Instruction.ModRegRm.RegisterCode.EAX },
            { AssemblyExpr.Register.RegisterName.RBX, Instruction.ModRegRm.RegisterCode.EBX },
            { AssemblyExpr.Register.RegisterName.RCX, Instruction.ModRegRm.RegisterCode.ECX },
            { AssemblyExpr.Register.RegisterName.RDX, Instruction.ModRegRm.RegisterCode.EDX },
            { AssemblyExpr.Register.RegisterName.RBP, Instruction.ModRegRm.RegisterCode.EBP },
            { AssemblyExpr.Register.RegisterName.RSP, Instruction.ModRegRm.RegisterCode.ESP },
            { AssemblyExpr.Register.RegisterName.RSI, Instruction.ModRegRm.RegisterCode.ESI },
            { AssemblyExpr.Register.RegisterName.RDI, Instruction.ModRegRm.RegisterCode.EDI }
        };
    }
}
