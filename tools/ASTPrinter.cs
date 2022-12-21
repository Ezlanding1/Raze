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
                PrintAST(((Expr.Variable)expr).type);
                PrintAST(((Expr.Variable)expr).name);
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
        private void PrintAST(List<Token> tokens)
        {
            foreach (Token token in tokens)
            {
                Console.Write(offset);
                Console.WriteLine("├─'" + token.lexeme + "'");
            }
            
        }

        public void PrintAST(List<string> exprs)
        {
            foreach (string s in exprs)
            {
                Console.Write(offset);
                Console.WriteLine("├─'" + s+ "'");
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
            expr.block.Accept(this);
            return null;
        }

        public object? visitFunctionExpr(Expr.Function expr)
        {
            PrintAST(expr.name);
            foreach (Expr paramExpr in expr.parameters)
            {
                PrintAST(paramExpr);
            }
            expr.block.Accept(this);
            return null;
        }

        public object? visitGetExpr(Expr.Get expr)
        {
            PrintAST(expr.type);
            PrintAST(expr.name);
            PrintAST(expr.get);
            return null;
        }

        public object? visitGroupingExpr(Expr.Grouping expr)
        {
            expr.expression.Accept(this);
            return null;
        }

        public object? visitLiteralExpr(Expr.Literal expr)
        {
            PrintAST(expr.literal);
            return null;
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
            PrintAST(expr.type);
            PrintAST(expr.name);
            return null;
        }

        public object? visitDeclareExpr(Expr.Declare expr)
        {
            PrintAST(expr.type);
            PrintAST(expr.name);
            PrintAST(expr.value);
            return null;
        }

        public object? visitConditionalExpr(Expr.Conditional expr)
        {
            PrintAST(expr.type);
            PrintAST(expr.condition);
            PrintAST(expr.block);
            return null;
        }

        public object? visitBlockExpr(Expr.Block expr)
        {
            PrintAST(expr.block, false);
            return null;
        }

        public object? visitReturnExpr(Expr.Return expr)
        {
            PrintAST(expr.value);
            return null;
        }

        public object? visitAssignExpr(Expr.Assign expr)
        {
            PrintAST(expr.value);
            PrintAST(expr.variable);
            return null;
        }

        public object? visitKeywordExpr(Expr.Keyword expr)
        {
            PrintAST(expr.keyword);
            return null;
        }

        public object? visitPrimitiveExpr(Expr.Primitive expr)
        {
            PrintAST(expr.literal.value);
            return null;
        }

        public object? visitNewExpr(Expr.New expr)
        {
            PrintAST(expr._className);
            return null;
        }

        public object? visitAssemblyExpr(Expr.Assembly expr)
        {
            PrintAST(expr.block);
            return null;
        }

        public object? visitDefineExpr(Expr.Define expr)
        {
            PrintAST(expr.name);
            PrintAST(expr.value);
            return null;
        }
    }
}
