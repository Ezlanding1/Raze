using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Espionage.Tools
{
    internal class ASTPrinter
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
            
        }
        static void PrintAST(Token token)
        {
            Console.Write(token.lexeme);
        }
    }
    internal class tASTPrinter : Expr.IVisitor<object?>
    {
        public void PrintAST(List<Expr> exprs)
        {
            foreach (Expr expr in exprs)
            {
                PrintAST(expr);
                Console.WriteLine(" ");
            }
        }
        void PrintAST(Expr expr)
        {
            expr.Accept(this);
        }
        void PrintAST(Token token)
        {
            Console.Write(token.lexeme);
        }
        public object visitAssignExpr(Expr.Assign expr)
        {
            throw new NotImplementedException();
        }

        public object visitBinaryExpr(Expr.Binary expr)
        {
            expr.left.Accept(this);
            PrintAST(expr.op);
            expr.right.Accept(this);
            return null;
        }

        public object visitCallExpr(Expr.Call expr)
        {
            throw new NotImplementedException();
        }

        public object visitGetExpr(Expr.Get expr)
        {
            throw new NotImplementedException();
        }

        public object visitGroupingExpr(Expr.Grouping expr)
        {
            Console.Write("(");
            expr.expression.Accept(this);
            Console.Write(")");
            return null;
        }

        public object visitLiteralExpr(Expr.Literal expr)
        {
            PrintAST(expr.literal);
            return null;
        }

        public object visitLogicalExpr(Expr.Logical expr)
        {
            throw new NotImplementedException();
        }

        public object visitSetExpr(Expr.Set expr)
        {
            throw new NotImplementedException();
        }

        public object visitSuperExpr(Expr.Super expr)
        {
            throw new NotImplementedException();
        }

        public object visitThisExpr(Expr.This expr)
        {
            throw new NotImplementedException();
        }

        public object visitUnaryExpr(Expr.Unary expr)
        {
            throw new NotImplementedException();
        }

        public object visitVariableExpr(Expr.Variable expr)
        {
            throw new NotImplementedException();
        }
    }
}
