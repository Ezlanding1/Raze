using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Analyzer
{
    internal class OptimizationPass : Pass<object?>
    {
        SymbolTable symbolTable = SymbolTableSingleton.SymbolTable;

        public OptimizationPass(List<Expr> expressions) : base(expressions)
        {
        }

        internal override void Run()
        {
            symbolTable.main.Accept(this);
        }

        public override object? VisitFunctionExpr(Expr.Function expr)
        {
            expr.dead = false;

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

        public override object VisitNewExpr(Expr.New expr)
        {
            HandleCall(expr.call);
            return null;
        }
        
        public override object VisitBinaryExpr(Expr.Binary expr)
        {
            if (expr.internalFunction != null)
            {
                HandleCall(expr);
            }
            return base.VisitBinaryExpr(expr);
        }

        public override object VisitUnaryExpr(Expr.Unary expr)
        {
            if (expr.internalFunction != null)
            { 
                HandleCall(expr);
            }
            return base.VisitUnaryExpr(expr);
        }

        public override object VisitCallExpr(Expr.Call expr)
        {
            HandleCall(expr);
            return null;
        }

        public override object? VisitAssignExpr(Expr.Assign expr)
        {
            base.VisitAssignExpr(expr);

            SetInlineRef(expr.member.GetLastData());

            return null;
        }

        public override object VisitAssemblyExpr(Expr.Assembly expr)
        {
            int varIdx = 0;

            foreach (ExprUtils.AssignableInstruction instruction in expr.block)
            {
                var assigningVars = instruction.GetAssigningVars();

                if (assigningVars.Item1 == 0)
                {
                    continue;
                }

                switch (assigningVars.Item2) 
                {
                    case 1 or 2:
                        SetInlineRef(expr.variables[varIdx + (assigningVars.Item2 - 1)].GetLastData());
                        break;
                    case 3:
                        SetInlineRef(expr.variables[varIdx].GetLastData());
                        SetInlineRef(expr.variables[varIdx+1].GetLastData());
                        break;

                }
                varIdx += assigningVars.Item1;
            }

            return null;
        }

        private void HandleCall(Expr.ICall expr)
        {
            if (expr.GetInternalFunction().dead)
            {
                using (new SaveContext())
                    expr.GetInternalFunction().Accept(this);
            }

            if (symbolTable.Current.definitionType == Expr.Definition.DefinitionType.Function && ((Expr.Function)symbolTable.Current).modifiers["inline"])
            {
                for (int i = 0; i < expr.GetInternalFunction().Arity; i++)
                {
                    if (expr.GetInternalFunction().parameters[i].modifiers["ref"] && !expr.GetInternalFunction().parameters[i].modifiers["inlineRef"])
                    {
                        ((Expr.Function)symbolTable.Current).parameters[i].modifiers["inlineRef"] = false;
                    }
                }
            }
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
