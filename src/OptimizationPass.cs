using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Raze
{
    internal partial class Analyzer
    {
        internal class OptimizationPass : Pass<object?>
        {
            SymbolTable symbolTable = SymbolTableSingleton.SymbolTable;

            HashSet<Expr.Definition> handledDefs = new();

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

            public override object? visitFunctionExpr(Expr.Function expr)
            {
                handledDefs.Add(expr);

                symbolTable.SetContext(expr);

                base.visitFunctionExpr(expr);

                symbolTable.UpContext();

                return null;
            }

            public override object? visitClassExpr(Expr.Class expr)
            {
                symbolTable.SetContext(expr);

                base.visitClassExpr(expr);

                symbolTable.UpContext();

                return null;
            }

            public override object? visitPrimitiveExpr(Expr.Primitive expr)
            {
                symbolTable.SetContext(expr);

                base.visitPrimitiveExpr(expr);

                symbolTable.UpContext();

                return null;
            }

            public override object visitCallExpr(Expr.Call expr)
            {
                if (!handledDefs.Contains(expr.internalFunction))
                {
                    var context = symbolTable.Current;

                    expr.internalFunction.Accept(this);

                    symbolTable.SetContext(context);
                }

                if (symbolTable.Current.definitionType == Expr.Definition.DefinitionType.Function && ((Expr.Function)symbolTable.Current).modifiers["inline"])
                {
                    for (int i = 0; i < expr.internalFunction.arity; i++)
                    {
                        if (expr.internalFunction.parameters[i].modifiers["ref"] && !expr.internalFunction.parameters[i].modifiers["inlineRef"])
                        {
                            ((Expr.Function)symbolTable.Current).parameters[i].modifiers["inlineRef"] = false;
                        }
                    }
                }
                return null;
            }

            public override object? visitAssignExpr(Expr.Assign expr)
            {
                base.visitAssignExpr(expr);

                SetInlineRef(expr.member.stack);

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
}
