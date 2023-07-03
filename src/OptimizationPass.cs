using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze
{
    internal partial class Analyzer
    {
        internal class OptimizationPass : Pass<object?>
        {
            public OptimizationPass(List<Expr> expressions) : base(expressions)
            {
            }

            internal override List<Expr> Run()
            {
                foreach (Expr expr in expressions)
                {
                    expr.Accept(this);
                }
                return expressions;
            }
        }
    }
}
