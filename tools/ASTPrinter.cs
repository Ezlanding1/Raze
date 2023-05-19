using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze.Tools
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
                //PrintAST(((Expr.Variable)expr).stack.type);
                //PrintAST(((Expr.Variable)expr).name);
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

        public void PrintAST(IEnumerable<string> exprs)
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
            this.visitTypeReferenceExpr(expr);
            PrintAST(expr.arguments, false);
            return null;
        }

        public object? visitClassExpr(Expr.Class expr)
        {
            PrintAST(expr.name);
            expr.topLevelBlock.Accept(this);
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

        public object? visitTypeReferenceExpr(Expr.TypeReference expr)
        {
            if (expr.typeName == null) return null;

            foreach (var type in expr.typeName)
            {
                PrintAST(type);
            }
            return null;
        }

        public object? visitGetReferenceExpr(Expr.GetReference expr)
        {
            return this.visitTypeReferenceExpr(expr);
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

        public object? visitUnaryExpr(Expr.Unary expr)
        {
            PrintAST(expr.operand);
            PrintAST(expr.op);
            return null;
        }

        public object? visitVariableExpr(Expr.Variable expr)
        {
            foreach (var type in expr.typeName)
            {
                PrintAST(type);
            }
            return null;
        }

        public object? visitDeclareExpr(Expr.Declare expr)
        {
            PrintAST(expr.type.ToString());
            PrintAST(expr.name);

            if (expr.value != null)
                PrintAST(expr.value);

            return null;
        }

        public object? visitIfExpr(Expr.If expr)
        {
            PrintConditional(expr.conditional, "if");

            expr.ElseIfs.ForEach(x => PrintConditional(x.conditional, "else if"));

            PrintConditional(expr._else.conditional, "else");
            return null;
        }

        public object? visitWhileExpr(Expr.While expr)
        {
            PrintConditional(expr.conditional, "while");
            return null;
        }

        public object? visitForExpr(Expr.For expr)
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
            PrintAST(expr.member);
            return null;
        }

        public object? visitKeywordExpr(Expr.Keyword expr)
        {
            PrintAST(expr.keyword);
            return null;
        }

        public object? visitPrimitiveExpr(Expr.Primitive expr)
        {
            PrintAST(expr.name);
            PrintAST(string.Join(", ", expr.literals));
            PrintAST(expr.size.ToString());
            PrintAST(expr.block);
            return null;
        }

        public object? visitNewExpr(Expr.New expr)
        {
            expr.call.Accept(this);
            return null;
        }

        public object? visitAssemblyExpr(Expr.Assembly expr)
        {
            foreach (var instruction in expr.block)
            {
                PrintAST(instruction.ToString());
            }

            return null;
        }

        public object? visitDefineExpr(Expr.Define expr)
        {
            PrintAST(expr.name);
            PrintAST(expr.value);
            return null;
        }

        public object? visitIsExpr(Expr.Is expr)
        {
            PrintAST(expr.left);
            PrintAST("is");
            PrintAST(expr.right);
            return null;
        }
    }
}
