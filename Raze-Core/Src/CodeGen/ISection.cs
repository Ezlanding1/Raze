using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class CodeGen : Expr.IVisitor<AssemblyExpr.IValue?>
{
    public interface ISection : IEnumerable<AssemblyExpr>
    {
        public class TopLevel : List<AssemblyExpr.TopLevelExpr>, ISection
        {
        }

        public class Text : List<AssemblyExpr.TextExpr>, ISection
        {
            public static TopLevel GenerateHeaderInstructions()
            {
                return new TopLevel() { new AssemblyExpr.Global("_start"), new AssemblyExpr.Section("text") };
            }

            public static Text GenerateDriverInstructions()
            {
                return new Text() { new AssemblyExpr.Procedure("_start") };
            }
        }

        public class Data : List<AssemblyExpr.DataExpr>, ISection
        {
            public static TopLevel GenerateHeaderInstructions()
            {
                return new TopLevel() { new AssemblyExpr.Section("data") };
            }
        }

        public class IData : List<AssemblyExpr.IDataExpr>, ISection
        {
            public static TopLevel GenerateHeaderInstructions()
            {
                return new TopLevel() { new AssemblyExpr.Section("idata") };
            }
        }

        IEnumerator<AssemblyExpr> IEnumerable<AssemblyExpr>.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
