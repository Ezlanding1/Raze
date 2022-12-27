using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;

namespace Raze
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
            public T visitAssemblyExpr(Assembly expr);
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
            public T visitDefineExpr(Define expr);
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

        public class Declare : Variable
        {
            public Expr value;

            public Declare(Variable var, Expr value)
                : base(var)
            {
                this.value = value;
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
            public Else(Token type, Block block)
                : base(type, null, block)
            {

            }
        }

        public class While : Conditional
        {
            public While(Token type, Expr condition, Block block)
                : base(type, condition, block)
            {

            }
        }

        public class Call : Expr
        {
            public Variable callee;
            public bool constructor;
            public Function internalFunction;
            public List<Expr> arguments;
            public bool found;
            public Call(Variable callee, List<Expr> arguments, bool constructor)
            {
                this.callee = callee;
                this.arguments = arguments;
                this.constructor = constructor;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitCallExpr(this);
            }

            public override string ToString()
            {
                return callee.name.ToString() + "." + callee.ToString();
            }

        }

        public class Get : Variable
        {
            public Variable get;

            public Get(Variable getter, Variable get)
                : base(getter)
            {
                this.get = get;
            }
            public Get(Token getter, Variable get)
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

        public class Assembly : Expr
        {
            public List<string> block;

            public Assembly(List<string> block)
            {
                this.block = block;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitAssemblyExpr(this);
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
            public (bool, Expr.Literal) define;

            public Token name;
            public Token type;
            public int size;
            public int stackOffset;

            public Variable(Token type, Token name, int size)
            {
                this.type = type;
                this.name = name;
                this.size = size;
            }

            public Variable(Token type, Token name)
            {
                this.type = type;
                this.name = name;
            }

            public Variable(Token name)
            {
                this.name = name;
            }

            public Variable(Variable @this)
            {
                this.name = @this.name;
                this.type = @this.type;
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
            public string keyword;

            public Keyword(string keyword)
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
            public string declName;

            public Variable _className;
            public List<Expr> arguments;

            public Class internalClass;

            public New(Variable _className, List<Expr> arguments)
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
            public Parameter(Token type, Token name)
                : base(type, name)
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
            public string _returnType;
            public int arity
            {
                get { return parameters.Count; }
            }
            public bool keepStack;
            public Dictionary<string, bool> modifiers;
            public bool constructor;
            public int size;

            public Function()
                : base(null, null)
            {
                this.modifiers = new(){
                    { "static", false },
                    { "unsafe", false }
                };
            }

            public void Add(string _returnType, Token name, List<Parameter> parameters, Block block)
            {
                base.name = name;
                base.block = block;
                this._returnType = _returnType;
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

            public Expr.Function constructor;

            public Class(Token name, Block block) : base(name, block) { }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitClassExpr(this);
            }
        }
        
        public class Return : Expr
        {
            public Expr value;
            public bool _void;
            public Return(Expr value)
            {
                this.value = value;
            }
            public Return(Expr value, bool _void)
            {
                this.value = value;
                this._void = _void;
            }
            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitReturnExpr(this);
            }
        }

        public class Assign : Expr
        {
            public Variable variable;
            public Token? op;
            public Expr value;

            public Assign(Variable variable, Token op, Expr value)
            {
                this.variable = variable;
                this.op = op;
                this.value = value;
            }

            public Assign(Variable variable, Expr value)
            {
                this.variable = variable;
                this.value = value;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitAssignExpr(this);
            }
        }

        public class Define : Expr
        {
            public Token name;
            public Literal value;

            public Define(Token name, Literal value)
            {
                this.name = name;
                this.value = value;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitDefineExpr(this);
            }
        }
    }
}
