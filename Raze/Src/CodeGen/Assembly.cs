using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class CodeGen : Expr.IVisitor<AssemblyExpr.Value?>
{
    public class Assembly
    {
        public readonly ISection.Text text = new();
        public readonly ISection.Data data = new();
    }
}