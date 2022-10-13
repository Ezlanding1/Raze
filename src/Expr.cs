using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Espionage
{
    internal abstract class Expr
    {
        public abstract T Accept<T>(IVisitor<T> visitor);

        public interface IVisitor<T>
        {
            public T visitBinaryExpr(Binary expr);
            public T visitUnaryExpr(Unary expr);
            public T visitGroupingExpr(Grouping expr);
            public T visitLiteralExpr(Literal expr);
            public T visitDeclareExpr(Declare expr);

            public T visitCallExpr(Call expr);
            public T visitGetExpr(Get expr);
            //public T visitLogicalExpr(Logical expr);
            public T visitSetExpr(Set expr);
            public T visitSuperExpr(Super expr);
            public T visitThisExpr(This expr);
            public T visitVariableExpr(Variable expr);
            public T visitFunctionExpr(Function expr);
            public T visitClassExpr(Class expr);
        }

        public class Binary : Expr
        {

            public Expr left;
            public Token op;
            public Expr right;

            public Binary(Expr left, Token op, Expr right)
            {
                this.left = left;
                this.op = op;
                this.right = right;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitBinaryExpr(this);
            }

        }
        public class Unary : Expr
        {

            public Token op;
            public Expr operand;

            public Unary(Token op, Expr operand)
            {
                this.op = op;
                this.operand = operand;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitUnaryExpr(this);
            }
        }

        public class Grouping : Expr
        {

            public Expr expression;

            public Grouping(Expr expression)
            {
                this.expression = expression;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitGroupingExpr(this);
            }

        }

        public class Literal : Expr
        {
            public Token literal;

            public Literal(Token literal)
            {
                this.literal = literal;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitLiteralExpr(this);
            }

        }

        public class Declare : Expr
        {
            public Expr type;
            public Expr left;
            public Token op;
            public Expr right;

            public Declare(Expr type, Expr left, Token op, Expr right)
            {
                this.type = type;
                this.left = left;
                this.op = op;
                this.right = right;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitDeclareExpr(this);
            }

        }

        //public class Assign : Expr
        //{
        //    public Token name;
        //    public Expr value;

        //    public Assign(Token name, Expr value)
        //    {
        //        this.name = name;
        //        this.value = value;
        //    }

        //    public override T Accept<T>(IVisitor<T> visitor)
        //    {
        //        return visitor.visitAssignExpr(this);
        //    }
        //}

        public class Call : Expr
        {
            public Expr callee;
            public List<Expr> arguments;
            public Call(Expr callee, List<Expr> arguments)
            {
                this.callee = callee;
                this.arguments = arguments;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitCallExpr(this);
            }

        }

        public class Get : Expr
        {
            public Expr obj;
            public Token name;
            public Get(Expr obj, Token name)
            {
                this.obj = obj;
                this.name = name;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitGetExpr(this);
            }

        }

        //public class Logical : Expr
        //{
        //    public Expr left;
        //    public Token op;
        //    public Expr right;
        //    public Logical(Expr left, Token op, Expr right)
        //    {
        //        this.left = left;
        //        this.op = op;
        //        this.right = right;
        //    }

        //    public override T Accept<T>(IVisitor<T> visitor)
        //    {
        //        return visitor.visitLogicalExpr(this);
        //    }

        //}

        public class Set : Expr
        {
            public Expr obj;
            public Token name;
            public Expr value;
            public Set(Expr obj, Token name, Expr value)
            {
                this.obj = obj;
                this.name = name;
                this.value = value;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitSetExpr(this);
            }

        }
        public class Super : Expr
        {
            public Token keyword;
            public Token method;
            public Super(Token keyword, Token method)
            {
                this.keyword = keyword;
                this.method = method;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitSuperExpr(this);
            }

        }
        public class This : Expr
        {
            public Token keyword;
            public This(Token keyword)
            {
                this.keyword = keyword;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitThisExpr(this);
            }

        }

        public class Variable : Expr
        {
            public Token variable;
            public Variable(Token variable)
            {
                this.variable = variable;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitVariableExpr(this);
            }
        }

        public abstract class Definition : Expr
        {
            public Token name;
            public List<Expr> block;

            public Definition(Token name, List<Expr> block)
            {
                this.name = name;
                this.block = block;
            }

            public abstract override T Accept<T>(IVisitor<T> visitor);
        }

        public class Function : Definition {
            public List<Expr> parameters;
            public Function(Token name, List<Expr> parameters, List<Expr> block)
                : base(name, block)
            {
                this.parameters = parameters;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitFunctionExpr(this);
            }
        }

        public class Class : Definition
        {
            public Class(Token name, List<Expr> block) : base(name, block) { }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitClassExpr(this);
            }
        }
    }
}
