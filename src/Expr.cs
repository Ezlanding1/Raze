﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
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
            public T visitConditionalExpr(Conditional expr);
            public T visitCallExpr(Call expr);
            public T visitGetExpr(Get expr);
            public T visitBlockExpr(Block expr);
            public T visitSuperExpr(Super expr);
            public T visitThisExpr(This expr);
            public T visitVariableExpr(Variable expr);
            public T visitFunctionExpr(Function expr);
            public T visitClassExpr(Class expr);
            public T visitReturnExpr(Return expr);
            public T visitAssignExpr(Assign expr);
            public T visitPrimitiveExpr(Primitive expr);
            public T visitKeywordExpr(Keyword expr);
            public T visitNewExpr(New expr);
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
            public Token type;
            public Token name;
            public Expr value;
            public int offset;
            public Declare(Token type, Token left, Expr right)
            {
                this.type = type;
                this.name = left;
                this.value = right;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitDeclareExpr(this);
            }

        }

        public abstract class Conditional : Expr
        {
            public Token type;
            public Expr condition;
            public Block block;

            public Conditional(Token type, Expr condition, Block block)
            {
                this.type = type;
                this.condition = condition;
                this.block = block;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitConditionalExpr(this);
            }
        }

        public abstract class IfThenElse : Conditional 
        {
            public IfThenElse(Token type, Expr condition, Block block)
                : base(type, condition, block)
            {

            }
        }

        public class If : IfThenElse
        {
            public List<ElseIf> ElseIfs;
            public Else _else;
            public If(Token type, Expr condition, Block block)
                : base(type, condition, block)
            {
                this.ElseIfs = new();
            }
        }
        public class ElseIf : IfThenElse
        {
            public If top;
            public ElseIf(Token type, Expr condition, Block block)
                : base(type, condition, block)
            {

            }
        }
        public class Else : IfThenElse
        {
            public If top;
            public Else(Token type, Expr condition, Block block)
                : base(type, condition, block)
            {

            }
        }

        public class Call : Expr
        {
            public Var callee;
            public Function internalFunction;
            public List<Expr> arguments;
            public Call(Var callee, List<Expr> arguments)
            {
                this.callee = callee;
                this.arguments = arguments;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitCallExpr(this);
            }

        }

        public class Get : Var
        {
            public Var get;
            public Get(Token getter, Var get)
                : base(getter)
            {
                this.get = get;
            }
            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitGetExpr(this);
            }

        }

        public class Block : Expr
        {
            public List<Expr> block;
            public bool _classBlock;

            public Block(List<Expr> block)
            {
                this.block = block;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitBlockExpr(this);
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

        public class Variable : Var
        {
            public string stackPos;
            public bool register;
            public Variable(Token variable)
                :base(variable)
            {

            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitVariableExpr(this);
            }
        }

        public class Primitive : Expr
        {
            public Primitives.PrimitiveType literal;
            public int stackOffset;

            public Primitive(Primitives.PrimitiveType literal)
            {
                this.literal = literal;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitPrimitiveExpr(this);
            }

        }

        public class Keyword : Expr
        {
            public Token keyword;

            public Keyword(Token keyword)
            {
                this.keyword = keyword;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitKeywordExpr(this);
            }
        }

        public class New : Expr
        {
            public Token _className;
            public List<Expr> arguments;

            public Function internalFunction;
            public Class internalClass;

            public New(Token _className, List<Expr> arguments)
            {
                this._className = _className;
                this.arguments = arguments;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitNewExpr(this);
            }
        }

        public class Parameter : Variable
        {
            public Token type;
            public Parameter(Token type, Token variable)
                : base(variable)
            {
                this.type = type;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitVariableExpr(this);
            }
        }

        public abstract class Definition : Expr
        {
            public Token name;
            public Block block;

            public Definition(Token name, Block block)
            {
                this.name = name;
                this.block = block;
            }

            public abstract override T Accept<T>(IVisitor<T> visitor);
        }

        public class Function : Definition {
            public List<Parameter> parameters;
            public int arity
            {
                get { return parameters.Count; }
            }
            public bool _static;
            public bool constructor;
            public bool found;
            public Function(Token name, List<Parameter> parameters, Block block)
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
            public string dName;
            public Class(Token name, Block block) : base(name, block) { }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitClassExpr(this);
            }
        }
        
        public class Return : Expr
        {
            public Expr value;
            public Return(Expr value)
            {
                this.value = value;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitReturnExpr(this);
            }
        }

        public class Assign : Expr
        {
            public Var variable;
            public Expr value;
            public int offset;

            public Assign(Var variable, Expr value)
            {
                this.variable = variable;
                this.value = value;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitAssignExpr(this);
            }
        }

        public abstract class Var : Expr
        {
            public Token variable;
            public string type;
            public Var(Token variable)
            {
                this.variable = variable;
            }

            public abstract override T Accept<T>(IVisitor<T> visitor);
        }
    }
}