﻿using Raze.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

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

        public virtual T VisitBinaryExpr(Expr.Binary expr)
        {
            expr.left.Accept(this);
            expr.right.Accept(this);
            return default;
        }

        public virtual T VisitBlockExpr(Expr.Block expr)
        {
            foreach (var blockExpr in expr.block)
            {
                blockExpr.Accept(this);
            }
            return default;
        }

        public virtual T VisitCallExpr(Expr.Call expr)
        {
            foreach (Expr argExpr in expr.arguments)
            {
                argExpr.Accept(this);
            }

            return default;
        }

        public virtual T VisitClassExpr(Expr.Class expr)
        {
            Expr.ListAccept(expr.declarations, this);
            Expr.ListAccept(expr.definitions, this);
            return default;
        }

        public virtual T VisitDeclareExpr(Expr.Declare expr)
        {
            if (expr.value != null)
                expr.value.Accept(this);

            return default;
        }

        public virtual T VisitFunctionExpr(Expr.Function expr)
        {
            Expr.ListAccept(expr.block, this);
            return default;
        }

        public virtual T VisitTypeReferenceExpr(Expr.TypeReference expr) => default;

        public virtual T VisitGetReferenceExpr(Expr.GetReference expr)
        {
            foreach (Expr.Getter getter in expr.getters)
            {
                getter.Accept(this);
            }
            return default;
        }

        public virtual T VisitGetExpr(Expr.Get expr) => default;

        public virtual T VisitLogicalExpr(Expr.Logical expr)
        {
            expr.left.Accept(this);
            expr.right.Accept(this);
            return default;
        }

        public virtual T VisitGroupingExpr(Expr.Grouping expr)
        {
            expr.expression.Accept(this);
            return default;
        }

        public virtual T VisitIfExpr(Expr.If expr)
        {
            expr.conditionals[0].condition.Accept(this);
            expr.conditionals[0].block.Accept(this);

            return default;
        }

        public virtual T VisitLiteralExpr(Expr.Literal expr) => default;

        public virtual T VisitUnaryExpr(Expr.Unary expr)
        {
            expr.operand.Accept(this);
            return default;
        }

        public virtual T VisitReturnExpr(Expr.Return expr)
        {
            expr.value.Accept(this);
            return default;
        }

        public virtual T VisitAssignExpr(Expr.Assign expr)
        {
            expr.value.Accept(this);
            return default;
        }

        public virtual T VisitKeywordExpr(Expr.Keyword expr) => default;

        public virtual T VisitPrimitiveExpr(Expr.Primitive expr)
        {
            Expr.ListAccept(expr.definitions, this);
            return default;
        }

        public virtual T VisitAssemblyExpr(Expr.Assembly expr) => default;

        public virtual T VisitNewExpr(Expr.New expr)
        {
            foreach (Expr argExpr in expr.call.arguments)
            {
                argExpr.Accept(this);
            }

            return default;
        }

        public virtual T VisitIsExpr(Expr.Is expr)
        {
            expr.left.Accept(this);
            expr.right.Accept(this);
            return default;
        }

        public virtual T VisitWhileExpr(Expr.While expr)
        {
            expr.conditional.condition.Accept(this);

            expr.conditional.block.Accept(this);
            return default;
        }

        public virtual T VisitForExpr(Expr.For expr)
        {
            expr.initExpr.Accept(this);
            expr.conditional.condition.Accept(this);
            expr.updateExpr.Accept(this);

            expr.conditional.block.Accept(this);
            return default;
        }
    }
}
