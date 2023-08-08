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

            return default(T);
        }

        public virtual T visitClassExpr(Expr.Class expr)
        {
            Expr.ListAccept(expr.declarations, this);
            Expr.ListAccept(expr.definitions, this);
            return default(T);
        }

        public virtual T visitDeclareExpr(Expr.Declare expr)
        {
            if (expr.value != null)
                expr.value.Accept(this);

            return default(T);
        }

        public virtual T visitFunctionExpr(Expr.Function expr)
        {
            Expr.ListAccept(expr.block, this);
            return default(T);
        }

        public virtual T visitTypeReferenceExpr(Expr.TypeReference expr)
        {
            return default(T);
        }

        public virtual T visitGetReferenceExpr(Expr.GetReference expr)
        {
            return default(T);
        }

        public virtual T visitGroupingExpr(Expr.Grouping expr)
        {
            expr.expression.Accept(this);
            return default(T);
        }

        public virtual T visitIfExpr(Expr.If expr)
        {
            expr.conditional.condition.Accept(this);

            expr.conditional.block.Accept(this);
            return default(T);
        }

        public virtual T visitLiteralExpr(Expr.Literal expr)
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
            Expr.ListAccept(expr.definitions, this);
            return default(T);
        }

        public virtual T visitAssemblyExpr(Expr.Assembly expr)
        {
            return default(T);
        }

        public virtual T visitNewExpr(Expr.New expr)
        {
            foreach (Expr argExpr in expr.call.arguments)
            {
                argExpr.Accept(this);
            }

            return default(T);
        }

        public virtual T visitDefineExpr(Expr.Define expr)
        {
            return default(T);
        }

        public virtual T visitIsExpr(Expr.Is expr)
        {
            expr.left.Accept(this);
            expr.right.Accept(this);
            return default(T);
        }

        public virtual T visitWhileExpr(Expr.While expr)
        {
            expr.conditional.condition.Accept(this);

            expr.conditional.block.Accept(this);
            return default(T);
        }

        public virtual T visitForExpr(Expr.For expr)
        {
            expr.initExpr.Accept(this);
            expr.conditional.condition.Accept(this);
            expr.updateExpr.Accept(this);

            expr.conditional.block.Accept(this);
            return default(T);
        }
    }
}