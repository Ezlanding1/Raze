using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

internal partial class Analyzer
{
    internal class OptimizationPass : Pass<object?>
    {
        SymbolTable symbolTable = SymbolTableSingleton.SymbolTable;

        HashSet<Expr.Definition> handledDefs = new();

        public OptimizationPass(List<Expr> expressions) : base(expressions)
        {
        }

        internal override void Run()
        {
            foreach (Expr expr in expressions)
            {
                expr.Accept(this);
            }
        }

        public override object? VisitFunctionExpr(Expr.Function expr)
        {
            handledDefs.Add(expr);

            symbolTable.SetContext(expr);

            base.VisitFunctionExpr(expr);

            symbolTable.UpContext();

            return null;
        }

        public override object? VisitClassExpr(Expr.Class expr)
        {
            symbolTable.SetContext(expr);

            base.VisitClassExpr(expr);

            symbolTable.UpContext();

            return null;
        }

        public override object? VisitPrimitiveExpr(Expr.Primitive expr)
        {
            symbolTable.SetContext(expr);

            base.VisitPrimitiveExpr(expr);

            symbolTable.UpContext();

            return null;
        }

        public override object VisitCallExpr(Expr.Call expr)
        {
            if (!handledDefs.Contains(expr.internalFunction))
            {
                using (new SaveContext())
                    expr.internalFunction.Accept(this);
            }

            if (symbolTable.Current.definitionType == Expr.Definition.DefinitionType.Function && ((Expr.Function)symbolTable.Current).modifiers["inline"])
            {
                for (int i = 0; i < expr.internalFunction.Arity; i++)
                {
                    if (expr.internalFunction.parameters[i].modifiers["ref"] && !expr.internalFunction.parameters[i].modifiers["inlineRef"])
                    {
                        ((Expr.Function)symbolTable.Current).parameters[i].modifiers["inlineRef"] = false;
                    }
                }
            }
            return null;
        }

        public override object? VisitAssignExpr(Expr.Assign expr)
        {
            base.VisitAssignExpr(expr);

            SetInlineRef(expr.member.GetLastData());

            return null;
        }

        private void SetInlineRef(Expr.StackData stack)
        {
            if (symbolTable.Current.definitionType == Expr.Definition.DefinitionType.Function && ((Expr.Function)symbolTable.Current).modifiers["inline"])
            {
                foreach (var paramExpr in ((Expr.Function)symbolTable.Current).parameters)
                {
                    if (paramExpr.stack == stack)
                    {
                        paramExpr.modifiers["inlineRef"] = false;
                        return;
                    }
                }
            }
        }
    }
}
