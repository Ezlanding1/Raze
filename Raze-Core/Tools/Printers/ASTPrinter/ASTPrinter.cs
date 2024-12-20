﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze.Tools;

public class ASTPrinter : Expr.IVisitor<object?>
{
    string offset;
    public ASTPrinter()
    {
        offset = "";
    }

    public void PrintAST()
    {
        PrintAST(SymbolTableSingleton.SymbolTable.GetMainImportData().expressions);
    }
    public void PrintAST(List<Expr> exprs, bool first=true)
    {
        foreach (Expr expr in exprs)
        {
            if (first)
            {
                offset = "";
            }
            PrintAST(expr);
            if (first)
            {
                Console.WriteLine(" ");
            }
        }
    }

    private void PrintAST(Expr? expr)
    {
        if (expr != null)
        {
            string tmp = offset;
            Console.Write(offset);
            Console.WriteLine("├─" + expr);
            offset += "|  ";
            expr.Accept(this);
            offset = tmp;
        }
    }

    private void PrintAST(Token token)
    {
        if (token != null)
        {
            Console.Write(offset);
            Console.WriteLine("├─'" + token.lexeme + "'");
        }
    }

    private void PrintAST(string s)
    {
        Console.Write(offset);
        Console.WriteLine("├─'" + s + "'");
    }

    public object? VisitBinaryExpr(Expr.Binary expr)
    {
        PrintAST(expr.left);
        PrintAST(expr.op);
        PrintAST(expr.right);
        return null;
    }

    public object? VisitCallExpr(Expr.Call expr)
    {
        if (expr.callee != null)
        {
            expr.callee.Accept(this);
        }
        PrintAST(expr.arguments, false);
        return null;
    }

    public object? VisitClassExpr(Expr.Class expr)
    {
        PrintAST(expr.ToString());
        Expr.ListAccept(expr.declarations, this);
        Expr.ListAccept(expr.definitions, this);
        return null;
    }

    public object? VisitFunctionExpr(Expr.Function expr)
    {
        PrintAST(expr.ToString());

        foreach (Expr.Parameter paramExpr in expr.parameters)
        {
            foreach (var type in paramExpr.typeName)
            {
                PrintAST(type);
            }
        }

        expr.block?.Accept(this);

        return null;
    }

    public object? VisitTypeReferenceExpr(Expr.TypeReference expr)
    {
        if (expr.typeName != null)
        {
            foreach (var type in expr.typeName)
            {
                PrintAST(type);
            }
        }
        return null;
    }

    public object? VisitAmbiguousGetReferenceExpr(Expr.AmbiguousGetReference expr)
    {
        foreach (var type in expr.typeName)
        {
            PrintAST(type);
        }
        return null;
    }

    public object? VisitInstanceGetReferenceExpr(Expr.InstanceGetReference expr)
    {
        foreach (var type in expr.getters)
        {
            type.Accept(this);
        }
        return null;
    }

    public object? VisitGetExpr(Expr.Get expr)
    {
        PrintAST(expr.name);
        return null;
    }

    public object? VisitLogicalExpr(Expr.Logical expr)
    {
        PrintAST(expr.left);
        PrintAST(expr.op);
        PrintAST(expr.right);
        return null;
    }

    public object? VisitGroupingExpr(Expr.Grouping expr)
    {
        expr.expression.Accept(this);
        return null;
    }

    public object? VisitLiteralExpr(Expr.Literal expr)
    {
        PrintAST(expr.literal);
        return null;
    }

    public object? VisitUnaryExpr(Expr.Unary expr)
    {
        PrintAST(expr.operand);
        PrintAST(expr.op);
        return null;
    }

    public object? VisitDeclareExpr(Expr.Declare expr)
    {
        foreach (var type in expr.typeName)
        {
            PrintAST(type);
        }

        PrintAST(expr.name);

        if (expr.value != null)
            PrintAST(expr.value);

        return null;
    }

    public object? VisitIfExpr(Expr.If expr)
    {
        PrintConditional(expr.conditionals[0], "if");

        for (int i = 1; i < expr.conditionals.Count; i++)
        {
            PrintConditional(expr.conditionals[i], "else if");
        }

        if (expr._else != null)
        {
            PrintAST("else");
            PrintAST(expr._else);
        }

        return null;
    }

    public object? VisitWhileExpr(Expr.While expr)
    {
        PrintConditional(expr.conditional, "while");
        return null;
    }

    public object? VisitForExpr(Expr.For expr)
    {
        PrintConditional(expr.conditional, "for");
        return null;
    }

    private void PrintConditional(Expr.Conditional expr, string type)
    {
        PrintAST(type);
        PrintAST(expr.condition);
        PrintAST(expr.block);
    }

    public object? VisitBlockExpr(Expr.Block expr)
    {
        PrintAST(expr.block, false);
        return null;
    }

    public object? VisitReturnExpr(Expr.Return expr)
    {
        PrintAST(expr.value);
        return null;
    }

    public object? VisitAssignExpr(Expr.Assign expr)
    {
        PrintAST(expr.value);
        PrintAST(expr.member);
        return null;
    }

    public object? VisitKeywordExpr(Expr.Keyword expr)
    {
        PrintAST(expr.keyword);
        return null;
    }

    public object? VisitPrimitiveExpr(Expr.Primitive expr)
    {
        PrintAST(expr.ToString());
        PrintAST(expr.size.ToString());
        Expr.ListAccept(expr.definitions, this);
        return null;
    }

    public object? VisitNewExpr(Expr.New expr)
    {
        expr.call.Accept(this);
        return null;
    }

    public object? VisitInlineAssemblyExpr(Expr.InlineAssembly expr)
    {
        foreach (var instruction in expr.instructions)
        {
            PrintAST(instruction.ToString());
        }

        return null;
    }

    public object? VisitIsExpr(Expr.Is expr)
    {
        PrintAST(expr.left);
        PrintAST("is");
        PrintAST(expr.right);
        return null;
    }

    public object? VisitAsExpr(Expr.As expr)
    {
        PrintAST(expr._is.left);
        PrintAST("as");
        PrintAST(expr._is.right);
        return null;
    }

    public object? VisitImportExpr(Expr.Import expr)
    {
        PrintAST("import");
        PrintAST(expr.importType.typeRef);
        PrintAST("from");
        PrintAST(expr.fileInfo.ToString());
        return null;
    }

    public object? VisitHeapAllocExpr(Expr.HeapAlloc expr)
    {
        PrintAST("heapalloc");
        PrintAST(expr.size);
        return null;
    }

    public object? VisitNoOpExpr(Expr.NoOp expr)
    {
        PrintAST("Invalid Expr");
        return null;
    }
}
