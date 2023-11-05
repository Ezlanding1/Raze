using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Raze.Assembler.Instruction;

namespace Raze;

public partial class Assembler : AssemblyExpr.IVisitor<Assembler.Instruction>
{
    public void Assemble(string fn, List<AssemblyExpr> textExprs, List<AssemblyExpr> dataExprs)
    {
        using (var fs = new FileStream(fn, FileMode.Create, FileAccess.Write))
        {
            foreach (var assemblyExpr in textExprs)
            {
                var instruction = assemblyExpr.Accept(this);

                byte[] bytes = instruction.ToByteArr();

                fs.Write(bytes, 0, bytes.Length);
            }
        }
    }

    public Instruction VisitBinary(AssemblyExpr.Binary instruction)
    {
        throw new NotImplementedException();
    }

    public Instruction VisitComment(AssemblyExpr.Comment instruction)
    {
        throw new NotImplementedException();
    }

    public Instruction VisitData(AssemblyExpr.Data instruction)
    {
        throw new NotImplementedException();
    }

    public Instruction VisitDataRef(AssemblyExpr.DataRef instruction)
    {
        throw new NotImplementedException();
    }

    public Instruction VisitGlobal(AssemblyExpr.Global instruction)
    {
        throw new NotImplementedException();
    }

    public Instruction VisitLiteral(AssemblyExpr.Literal instruction)
    {
        throw new NotImplementedException();
    }

    public Instruction VisitLocalProcedure(AssemblyExpr.LocalProcedure instruction)
    {
        throw new NotImplementedException();
    }

    public Instruction VisitLocalProcedureRef(AssemblyExpr.LocalProcedureRef instruction)
    {
        throw new NotImplementedException();
    }

    public Instruction VisitPointer(AssemblyExpr.Pointer instruction)
    {
        throw new NotImplementedException();
    }

    public Instruction VisitProcedure(AssemblyExpr.Procedure instruction)
    {
        throw new NotImplementedException();
    }

    public Instruction VisitProcedureRef(AssemblyExpr.ProcedureRef instruction)
    {
        throw new NotImplementedException();
    }

    public Instruction VisitRegister(AssemblyExpr.Register instruction)
    {
        throw new NotImplementedException();
    }

    public Instruction VisitSection(AssemblyExpr.Section instruction)
    {
        throw new NotImplementedException();
    }

    public Instruction VisitUnary(AssemblyExpr.Unary instruction)
    {
        throw new NotImplementedException();
    }

    public Instruction VisitZero(AssemblyExpr.Zero instruction)
    {
        throw new NotImplementedException();
    }
}