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
        internal static Instruction.ModRegRm.RegisterCode ExprRegisterToModRegRmRegister(AssemblyExpr.Register register) => (register.name, register.size) switch
        {
            (AssemblyExpr.Register.RegisterName.RAX, AssemblyExpr.Register.RegisterSize._8BitsUpper) => Instruction.ModRegRm.RegisterCode.AH,
            (AssemblyExpr.Register.RegisterName.RBX, AssemblyExpr.Register.RegisterSize._8BitsUpper) => Instruction.ModRegRm.RegisterCode.BH,
            (AssemblyExpr.Register.RegisterName.RCX, AssemblyExpr.Register.RegisterSize._8BitsUpper) => Instruction.ModRegRm.RegisterCode.CH,
            (AssemblyExpr.Register.RegisterName.RDX, AssemblyExpr.Register.RegisterSize._8BitsUpper) => Instruction.ModRegRm.RegisterCode.DH,
            (AssemblyExpr.Register.RegisterName.RAX, _) => Instruction.ModRegRm.RegisterCode.EAX,
            (AssemblyExpr.Register.RegisterName.RBX, _) => Instruction.ModRegRm.RegisterCode.EBX,
            (AssemblyExpr.Register.RegisterName.RCX, _) => Instruction.ModRegRm.RegisterCode.ECX,
            (AssemblyExpr.Register.RegisterName.RDX, _) => Instruction.ModRegRm.RegisterCode.EDX,
            (AssemblyExpr.Register.RegisterName.RBP, _) => Instruction.ModRegRm.RegisterCode.EBP,
            (AssemblyExpr.Register.RegisterName.RSP, _) => Instruction.ModRegRm.RegisterCode.ESP,
            (AssemblyExpr.Register.RegisterName.RSI, _) => Instruction.ModRegRm.RegisterCode.ESI,
            (AssemblyExpr.Register.RegisterName.RDI, _) => Instruction.ModRegRm.RegisterCode.EDI
        };
    }
}
