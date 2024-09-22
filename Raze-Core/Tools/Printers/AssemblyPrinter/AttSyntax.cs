using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze.Tools;

partial class Syntaxes
{
    partial class SyntaxFactory
    {
        class AttSyntax : ISyntaxFactory, AssemblyExpr.IVisitor<string>
        {
            public AttSyntax()
            {
                header = new AssemblyExpr.Comment("Raze Compiler Version ALPHA 0.0.0 Intel_x86-64 GAS");
            }

            public override void Run(CodeGen.ISection instructions)
            {
                foreach (var instruction in instructions)
                {
                    instruction.Accept(this);
                    Console.WriteLine();
                }
            }

            public override void Run(AssemblyExpr instruction)
            {
                instruction.Accept(this);
            }

            public string VisitBinary(AssemblyExpr.Binary instruction)
            {
                throw new NotImplementedException();
            }

            public string VisitComment(AssemblyExpr.Comment instruction)
            {
                throw new NotImplementedException();
            }

            public string VisitData(AssemblyExpr.Data instruction)
            {
                throw new NotImplementedException();
            }

            public string VisitGlobal(AssemblyExpr.Global instruction)
            {
                throw new NotImplementedException();
            }

            public string VisitLocalProcedure(AssemblyExpr.LocalProcedure instruction)
            {
                throw new NotImplementedException();
            }

            public string VisitProcedure(AssemblyExpr.Procedure instruction)
            {
                throw new NotImplementedException();
            }

            public string VisitSection(AssemblyExpr.Section instruction)
            {
                throw new NotImplementedException();
            }

            public string VisitUnary(AssemblyExpr.Unary instruction)
            {
                throw new NotImplementedException();
            }

            public string VisitZero(AssemblyExpr.Nullary instruction)
            {
                throw new NotImplementedException();
            }
        }
    }
}
