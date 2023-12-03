using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class CodeGen : Expr.IVisitor<AssemblyExpr.Value?>
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

            public static Text GenerateDriverInstructions(Expr.Function main)
            {
                return new Text
                {
                    { new AssemblyExpr.Procedure("_start") },
                    { new AssemblyExpr.Unary(AssemblyExpr.Instruction.CALL, new AssemblyExpr.ProcedureRef(CodeGen.ToMangledName(main))) },
                    { new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RDI, AssemblyExpr.Register.RegisterSize._64Bits), (Analyzer.TypeCheckUtils.literalTypes[Parser.LiteralTokenType.INTEGER].Matches(main._returnType.type)) ? new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RAX, AssemblyExpr.Register.RegisterSize._64Bits) : new AssemblyExpr.Literal(Parser.LiteralTokenType.INTEGER, "0")) },
                    { new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RAX, AssemblyExpr.Register.RegisterSize._64Bits), new AssemblyExpr.Literal(Parser.LiteralTokenType.INTEGER, "60")) },
                    { new AssemblyExpr.Zero(AssemblyExpr.Instruction.SYSCALL) }
                };
            }
        }

        public class Data : List<AssemblyExpr.DataExpr>, ISection
        {
            public static TopLevel GenerateHeaderInstructions()
            {
                return new TopLevel() { new AssemblyExpr.Section("data") };
            }
        }

        IEnumerator<AssemblyExpr> IEnumerable<AssemblyExpr>.GetEnumerator() 
        { 
            return GetEnumerator(); 
        }
    }
}