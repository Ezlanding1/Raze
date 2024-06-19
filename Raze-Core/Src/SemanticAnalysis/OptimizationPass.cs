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
        HashSet<Expr.Class> classesToCalculateSize = new();

        public OptimizationPass(List<Expr> expressions) : base(expressions)
        {
        }

        internal override void Run()
        {
            symbolTable.main?.Accept(this);
            classesToCalculateSize.ToList().ForEach(_class => _class.CalculateSizeAndAllocateVariables());
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

            Expr.ListAccept(expr.declarations, this);
            classesToCalculateSize.Add(expr);

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
            if (expr.call.internalFunction.dead)
            {
                expr.call.Accept(this);
                using (new SaveContext())
                    expr.internalClass.Accept(this);
            }
            else
            {
                expr.call.Accept(this);
            }
            return null;
        }
        
        public override object VisitBinaryExpr(Expr.Binary expr)
        {
            if (expr.internalFunction != null)
            {
                HandleInvokable(expr);
            }
            return base.VisitBinaryExpr(expr);
        }

        public override object VisitUnaryExpr(Expr.Unary expr)
        {
            if (expr.internalFunction != null)
            { 
                HandleInvokable(expr);
            }
            return base.VisitUnaryExpr(expr);
        }

        public override object VisitCallExpr(Expr.Call expr)
        {
            expr.callee?.Accept(this);
            HandleInvokable(expr);

            if (symbolTable.Current is Expr.Function function && expr.internalFunction.modifiers["inline"])
            {
                for (int i = 0; i < expr.internalFunction.Arity; i++)
                {
                    var parameter = expr.internalFunction.parameters[i];
                    if ((parameter.modifiers["ref"] || !parameter.modifiers["inlineRef"]) && expr.arguments[i] is Expr.AmbiguousGetReference ambiguousGetRef)
                    {
                        function.parameters
                            .Where(x => x.stack == ambiguousGetRef.GetLastData())
                            .ToList()
                            .ForEach(x => x.modifiers["inlineRef"] = false);
                    }
                }
            }
            return null;
        }

        public override object? VisitDeclareExpr(Expr.Declare expr)
        {
            if (symbolTable.Current is not Expr.Class)
            {
                using (new SaveContext())
                    expr.stack.type.Accept(this);
            }
            expr.value?.Accept(this);
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
                    continue;

                switch (assigningVars.Item2) 
                {
                    case 1 or 2:
                        SetInlineRef(expr.variables[varIdx + (assigningVars.Item2 - 1)].Item2.GetLastData());
                        break;
                    case 3:
                        SetInlineRef(expr.variables[varIdx].Item2.GetLastData());
                        SetInlineRef(expr.variables[varIdx+1].Item2.GetLastData());
                        break;

                }
                varIdx += assigningVars.Item1;
            }
            return null;
        }

        private void HandleInvokable(Expr.Invokable expr)
        {
            if (expr.internalFunction.dead)
            {
                using (new SaveContext())
                    expr.internalFunction.Accept(this);
            }

            foreach (var arg in expr.Arguments)
            {
                arg.Accept(this);
            }
        }

        private void SetInlineRef(Expr.StackData? stack)
        {
            if (stack == null)
                return;

            if (symbolTable.Current is Expr.Function function && function.modifiers["inline"])
            {
                if (symbolTable.VariableIsParameter(function, stack, out var parameter))
                {
                    parameter.modifiers["inlineRef"] = false;
                }
            }
        }
    }
}
