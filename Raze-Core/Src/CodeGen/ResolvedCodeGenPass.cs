using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class CodeGen : Expr.IVisitor<AssemblyExpr.IValue?>
{
    internal abstract class ResolvedCodeGenPass(ISection.Text assemblyExprs)
    {
        private protected int idx = 0;
        private readonly ISection.Text assemblyExprs = assemblyExprs;

        public abstract void Run();

        public int AssemblyExprsCount() => assemblyExprs.Count;

        public AssemblyExpr.TextExpr GetAssemblyExpr(int idx) => 
            assemblyExprs[idx];

        public void RemoveCurrentInstruction()
        {
            assemblyExprs.RemoveAt(idx--);
        }

        public void InsertInstruction(int idx, AssemblyExpr.TextExpr textExpr)
        {
            assemblyExprs.Insert(idx, textExpr);
            this.idx++;
        }
    }
}
