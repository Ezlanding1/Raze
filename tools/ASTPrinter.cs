using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Espionage.Tools
{
    internal class bASTPrinter
    {
        public static void PrintAST(List<Expr> exprs)
        {
            
            foreach (Expr expr in exprs)
            {
                PrintAST(expr);
                Console.WriteLine(" ");
            }
        }
        static void PrintAST(Expr expr)
        {
            
            if (expr is Expr.Grouping)
            {
                Console.Write("(");
                PrintAST(((Expr.Grouping)expr).expression);
                Console.Write(")");
            }
            else if (expr is Expr.Binary)
            {
                PrintAST(((Expr.Binary)expr).left);
                PrintAST(((Expr.Binary)expr).op);
                PrintAST(((Expr.Binary)expr).right);
            }
            else if (expr is Expr.Literal)
            {
                PrintAST(((Expr.Literal)expr).literal);
            }
            else if (expr is Expr.Variable)
            {
                PrintAST(((Expr.Variable)expr).variable);
            }
            else if (expr is Expr.Unary)
            {
                PrintAST(((Expr.Unary)expr).operand);
                PrintAST(((Expr.Unary)expr).op);
            }

        }
        static void PrintAST(Token token)
        {
            Console.Write(token.lexeme);
        }
    }
    internal class ASTPrinter : Expr.IVisitor<object?>
    {
        string offset;
        public ASTPrinter()
        {
            offset = "";
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

        private void PrintAST(Expr expr)
        {
            string tmp = offset;
            Console.Write(offset);
            Console.WriteLine("├─" + expr);
            offset += "|  ";
            expr.Accept(this);
            offset = tmp;
        }

        private void PrintAST(Token token)
        {
            Console.Write(offset);
            Console.WriteLine("├─'" + token.lexeme + "'");
        }

        public object? visitBinaryExpr(Expr.Binary expr)
        {
            PrintAST(expr.left);
            PrintAST(expr.op);
            PrintAST(expr.right);
            return null;
        }

        public object? visitCallExpr(Expr.Call expr)
        {
            PrintAST(expr.callee);
            PrintAST(expr.arguments, false);
            return null;
        }

        public object? visitClassExpr(Expr.Class expr)
        {
            PrintAST(expr.name);
            PrintAST(expr.block, false);
            return null;
        }

        public object? visitFunctionExpr(Expr.Function expr)
        {
            PrintAST(expr.name);
            PrintAST(expr.parameters, false);
            PrintAST(expr.block, false);
            return null;
        }

        public object? visitGetExpr(Expr.Get expr)
        {
            throw new NotImplementedException();
        }

        public object? visitGroupingExpr(Expr.Grouping expr)
        {
            throw new NotImplementedException();
        }

        public object? visitLiteralExpr(Expr.Literal expr)
        {
            PrintAST(expr.literal);
            return null;
        }

        public object? visitSetExpr(Expr.Set expr)
        {
            throw new NotImplementedException();
        }

        public object? visitSuperExpr(Expr.Super expr)
        {
            throw new NotImplementedException();
        }

        public object? visitThisExpr(Expr.This expr)
        {
            throw new NotImplementedException();
        }

        public object? visitUnaryExpr(Expr.Unary expr)
        {
            PrintAST(expr.operand);
            PrintAST(expr.op);
            return null;
        }

        public object? visitVariableExpr(Expr.Variable expr)
        {
            PrintAST(expr.variable);
            return null;
        }

        public object? visitDeclareExpr(Expr.Declare expr)
        {
            PrintAST(expr.type);
            PrintAST(expr.left);
            PrintAST(expr.op);
            PrintAST(expr.right);
            return null;
        }
    }
}
