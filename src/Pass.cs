using Espionage.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Espionage
{
    internal partial class Analyzer
    {
        internal abstract class Pass<T> : Expr.IVisitor<T>
        {
            internal List<Expr> expressions;
            public Pass(List<Expr> expressions)
            {
                this.expressions = expressions;
            }

            internal abstract List<Expr> Run();

            public virtual T visitBinaryExpr(Expr.Binary expr)
            {
                expr.left.Accept(this);
                expr.right.Accept(this);
                return default(T);
            }

            public virtual T visitBlockExpr(Expr.Block expr)
            {
                foreach (var blockExpr in expr.block)
                {
                    blockExpr.Accept(this);
                }
                return default(T);
            }

            public virtual T visitCallExpr(Expr.Call expr)
            {
                foreach (Expr argExpr in expr.arguments)
                {
                    argExpr.Accept(this);
                }
                expr.internalFunction.found = true;
                expr.internalFunction.Accept(this);
                expr.internalFunction.found = false;

                return default(T);
            }

            public virtual T visitClassExpr(Expr.Class expr)
            {
                expr.block.Accept(this);
                return default(T);
            }

            public virtual T visitDeclareExpr(Expr.Declare expr)
            {
                expr.value.Accept(this);
                return default(T);
            }

            public virtual T visitFunctionExpr(Expr.Function expr)
            {
                if (!expr.found)
                {
                    return default(T);
                }
                foreach (Expr.Parameter paramExpr in expr.parameters)
                {
                    paramExpr.Accept(this);
                }
                expr.block.Accept(this);
                return default(T);
            }

            public virtual T visitGetExpr(Expr.Get expr)
            {
                return default(T);
            }

            public virtual T visitGroupingExpr(Expr.Grouping expr)
            {
                expr.expression.Accept(this);
                return default(T);
            }

            public virtual T visitConditionalExpr(Expr.Conditional expr)
            {
                if (expr.condition != null)
                    expr.condition.Accept(this);

                expr.block.Accept(this);
                return default(T);
            }

            public virtual T visitLiteralExpr(Expr.Literal expr)
            {
                return default(T);
            }

            public virtual T visitSuperExpr(Expr.Super expr)
            {
                return default(T);
            }

            public virtual T visitThisExpr(Expr.This expr)
            {
                return default(T);
            }

            public virtual T visitUnaryExpr(Expr.Unary expr)
            {
                expr.operand.Accept(this);
                return default(T);
            }

            public virtual T visitVariableExpr(Expr.Variable expr)
            {
                return default(T);
            }

            public virtual T visitReturnExpr(Expr.Return expr)
            {
                expr.value.Accept(this);
                return default(T);
            }

            public virtual T visitAssignExpr(Expr.Assign expr)
            {
                expr.value.Accept(this);
                return default(T);
            }

            public virtual T visitKeywordExpr(Expr.Keyword expr)
            {
                return default(T);
            }

            public virtual T visitPrimitiveExpr(Expr.Primitive expr)
            {
                expr.literal.value.Accept(this);
                return default(T);
            }

            public T visitAssemblyExpr(Expr.Assembly expr)
            {
                return default(T);
            }

            public virtual T visitNewExpr(Expr.New expr)
            {

                expr.internalClass.Accept(this);


                foreach (Expr argExpr in expr.arguments)
                {
                    argExpr.Accept(this);
                }
                expr.internalFunction.found = true;
                expr.internalFunction.Accept(this);
                expr.internalFunction.found = false;

                return default(T);
            }

        }
    }
}
