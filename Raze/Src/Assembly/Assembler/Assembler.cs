using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Raze.Assembler.Instruction;

namespace Raze;

public partial class Assembler : AssemblyExpr.IVisitor<Assembler.Instruction?>
{
    internal long location { get; private set; } = 0;

    internal Linker.SymbolTable symbolTable = new Linker.SymbolTable();

    Encoder encoder = new Encoder();

    public void Assemble(FileStream fs, CodeGen.Assembly assembly)
    {
        foreach (var assemblyExpr in Enumerable.Concat<AssemblyExpr>(assembly.text, assembly.data))
        {
            var instruction = assemblyExpr.Accept(this);

            if (instruction == null) continue;

            foreach (byte[] bytes in ((Instruction)instruction).ToBytes())
            {
                fs.Write(bytes, 0, bytes.Length);
                location += bytes.Length;
            }
        }
    }

    public Instruction? VisitBinary(AssemblyExpr.Binary instruction)
    {
        return encoder.Encode(instruction, this);
    }

    public Instruction? VisitComment(AssemblyExpr.Comment instruction)
    {
        throw new NotImplementedException();
    }

    public Instruction? VisitData(AssemblyExpr.Data instruction)
    {
        return encoder.EncodeData(instruction, this);
    }

    public Instruction? VisitDataRef(AssemblyExpr.DataRef instruction)
    {
        throw new NotImplementedException();
    }

    public Instruction? VisitGlobal(AssemblyExpr.Global instruction)
    {
        if (symbolTable.labels.ContainsKey(instruction.name))
            Linker.SymbolTable.globalLabels[instruction.name] = symbolTable.labels[instruction.name];
        else
            Linker.SymbolTable.globalData[instruction.name] = symbolTable.data[instruction.name];
        return null;
    }

    public Instruction? VisitLiteral(AssemblyExpr.Literal instruction)
    {
        throw new NotImplementedException();
    }

    public Instruction? VisitLocalProcedure(AssemblyExpr.LocalProcedure instruction)
    {
        throw new NotImplementedException();
    }

    public Instruction? VisitLocalProcedureRef(AssemblyExpr.LocalProcedureRef instruction)
    {
        throw new NotImplementedException();
    }

    public Instruction? VisitPointer(AssemblyExpr.Pointer instruction)
    {
        throw new NotImplementedException();
    }

    public Instruction? VisitProcedure(AssemblyExpr.Procedure instruction)
    {
        throw new NotImplementedException();
    }

    public Instruction? VisitProcedureRef(AssemblyExpr.ProcedureRef instruction)
    {
        throw new NotImplementedException();
    }

    public Instruction? VisitRegister(AssemblyExpr.Register instruction)
    {
        throw new NotImplementedException();
    }

    public Instruction? VisitSection(AssemblyExpr.Section instruction)
    {
        throw new NotImplementedException();
    }

    public Instruction? VisitUnary(AssemblyExpr.Unary instruction)
    {
        throw new NotImplementedException();
    }

    public Instruction? VisitZero(AssemblyExpr.Zero instruction)
    {
        throw new NotImplementedException();
    }
}