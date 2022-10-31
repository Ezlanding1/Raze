using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Espionage
{
    internal partial class Analyzer
    {
        internal abstract class Pass : Expr.IVisitor<object?>
        {
            internal List<Expr> expressions;
            public Pass(List<Expr> expressions)
            {
                this.expressions = expressions;
            }

            internal abstract List<Expr> Run();

            public virtual object? visitBinaryExpr(Expr.Binary expr)
            {
                expr.left.Accept(this);
                expr.right.Accept(this);
                return null;
            }

            public virtual object? visitBlockExpr(Expr.Block expr)
            {
                foreach (var blockExpr in expr.block)
                {
                    blockExpr.Accept(this);
                }
                return null;
            }

            public virtual object? visitCallExpr(Expr.Call expr)
            {
                foreach (Expr argExpr in expr.arguments)
                {
                    argExpr.Accept(this);
                }
                expr.internalFunction.found = true;
                expr.internalFunction.Accept(this);
                expr.internalFunction.found = false;

                return null;
            }

            public virtual object? visitClassExpr(Expr.Class expr)
            {
                expr.block.Accept(this);
                return null;
            }

            public virtual object? visitDeclareExpr(Expr.Declare expr)
            {
                expr.value.Accept(this);
                return null;
            }

            public virtual object? visitFunctionExpr(Expr.Function expr)
            {
                if (!expr.found)
                {
                    return null;
                }
                foreach (Expr.Parameter paramExpr in expr.parameters)
                {
                    paramExpr.Accept(this);
                }
                expr.block.Accept(this);
                return null;
            }

            public virtual object? visitGetExpr(Expr.Get expr)
            {
                return null;
            }

            public virtual object? visitGroupingExpr(Expr.Grouping expr)
            {
                expr.expression.Accept(this);
                return null;
            }

            public virtual object? visitConditionalExpr(Expr.Conditional expr)
            {
                expr.condition.Accept(this);
                expr.block.Accept(this);
                return null;
            }

            public virtual object? visitLiteralExpr(Expr.Literal expr)
            {
                return null;
            }

            public virtual object? visitSetExpr(Expr.Set expr)
            {
                return null;
            }

            public virtual object? visitSuperExpr(Expr.Super expr)
            {
                return null;
            }

            public virtual object? visitThisExpr(Expr.This expr)
            {
                return null;
            }

            public virtual object? visitUnaryExpr(Expr.Unary expr)
            {
                expr.operand.Accept(this);
                return null;
            }

            public virtual object? visitVariableExpr(Expr.Variable expr)
            {
                return null;
            }

            public virtual object? visitReturnExpr(Expr.Return expr)
            {
                expr.value.Accept(this);
                return null;
            }

            public virtual object? visitAssignExpr(Expr.Assign expr)
            {
                expr.value.Accept(this);
                return null;
            }

            public virtual object? visitKeywordExpr(Expr.Keyword expr)
            {
                return null;
            }
        }
    }
}
