using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
            public T visitVariableExpr(Variable expr);
            public T visitMemberExpr(Member expr);
            public T visitFunctionExpr(Function expr);
            public T visitClassExpr(Class expr);
            public T visitReturnExpr(Return expr);
            public T visitAssignExpr(Assign expr);
            public T visitPrimitiveExpr(Primitive expr);
            public T visitKeywordExpr(Keyword expr);
            public T visitNewExpr(New expr);
            public T visitDefineExpr(Define expr);
            public T visitIsExpr(Is expr);
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
            public Token name;
            public Expr value;

            public StackData stack = new();

            public Declare(Type type, Token name, Expr value)
            {
                this.stack.type = type;
                this.name = name;
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
            public Get callee;
            public bool constructor;
            public Function internalFunction;
            public List<Expr> arguments;
            public int stackOffset;
            
            public Call(Get callee, List<Expr> arguments)
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
            public Token name;
            public int stackOffset;
            public Get get;

            public Get(Token getter)
            {
                name = getter;
                get = null;
            }

            public Get(Get getter, Get get)
            {
                this.name = getter.name;
                this.get = get;
            }

            public Get(Token getter, Get get)
            {
                this.name = getter;
                this.get = get;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitGetExpr(this);
            }

            public override string ToString()
            {
                return name.lexeme.ToString() + ((get != null) ? ("." + get.ToString()) : "");
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

            public void Extend(Block block2)
            {
                this.block.InsertRange(0, block2.block);
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitBlockExpr(this);
            }
        }

        public class Assembly : Expr
        {
            public List<Instruction> block;
            public Dictionary<Variable, Instruction.Pointer> variables;

            public Assembly(List<Instruction> block,  Dictionary<Variable, Instruction.Pointer> variables)
            {
                this.block = block;
                this.variables = variables;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitAssemblyExpr(this);
            }
        }
        
        public class Member : Expr
        {
            public StackGet variable;
            public Get get;

            public Member(Get get)
            {
                this.get = get;

                GetVariableReference();
            }

            public void GetVariableReference()
            {
                if (get.get == null)
                {
                    get = new Variable(get.name);
                    this.variable = (Variable)get;

                    return;
                }

                Get? x = get;

                while (x.get.get != null)
                {
                    x = x.get;
                }

                x.get = new Variable(x.get.name);
                this.variable = (Variable)x.get;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitMemberExpr(this);
            }
        }

        // Note: Due to C# limitations on multiple inheritance, the StackData class will be a member of the classes that needs its fields 
        public class StackData
        {
            public Type type;
            public int size;
            public int stackOffset;
        }

        public class Variable : StackGet
        {
            public (bool, Literal) define;


            public Variable(Token name) : base(name)
            {
                this.name = name;
            }

            public Variable(Type type, Token name) : base(name)
            {
                this.stack.type = type;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitVariableExpr(this);
            }

            public override string ToString()
            {
                return name.lexeme;
            }
        }

        public class Primitive : Definition
        {
            public List<string> literals;

            public override string QualifiedName { get => name.lexeme; }

            public Primitive(Token name, List<string> literals, int size, Block block) : base (name, block, size)
            {
                this.literals = literals;
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
            public Call call;
            public Class internalClass;

            public New(Call call)
            {
                this.call = call;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitNewExpr(this);
            }
        }

        public class Parameter : Expr
        {
            public Token name;

            public Member member;

            public Parameter(Type type, Token name)
            {
                this.member = new(type.type);
                this.member.variable.stack.type = type;
                this.name = name;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return this.member.variable.stack.type.Accept(visitor);
            }
        }

        public abstract class Definition : Expr
        {
            public Token name;
            public Block block;
            public int size;

            public string path;
            public abstract string QualifiedName { get; }

            public Definition(Token name, Block block)
            {
                this.name = name;
                this.block = block;
            }

            public Definition(Token name, Block block, int size)
                : this (name, block)
            {
                this.size = size;
            }

            public abstract override T Accept<T>(IVisitor<T> visitor);
        }

        public class Function : Definition
        {
            public List<Parameter> parameters;
            public Type _returnType;
            public int _returnSize;
            public int arity
            {
                get { return parameters.Count; }
            }
            public bool leaf = true;
            public Dictionary<string, bool> modifiers;
            public bool constructor;

            public override string QualifiedName
            {
                get { return (this.path ?? "") + (constructor ? "." : "") + ((this.name == null) ? "" : this.name.lexeme); }
            }

            public Function()
                : base(null, null)
            {
                this.modifiers = new(){
                    { "static", false },
                    { "unsafe", false }
                };
            }

            public void Add(Type _returnType, Token name, List<Parameter> parameters, Block block)
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
            public Function constructor;

            public Block topLevelBlock;

            public override string QualifiedName
            {
                get { return this.path + this.name.lexeme; }
            }

            public Class(Token name, Block block) : base(name, new(new())) 
            {
                this.topLevelBlock = new(new());
                FilterTopLevel(block);
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitClassExpr(this);
            }

            public Class(Class @this) : base(@this.name, @this.block)
            {
                this.constructor = @this.constructor;
                this.size = @this.size;
            }

            private void FilterTopLevel(Block block)
            {
                foreach (var blockExpr in block.block)
                {
                    if (blockExpr is Function || blockExpr is Class)
                    {
                        this.block.block.Add(blockExpr);
                    }
                    else
                    {
                        this.topLevelBlock.block.Add(blockExpr);
                    }
                }
            }
        }
        
        public class Return : Expr
        {
            public Expr value;
            public bool _void;
            public int size;

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
            public Member member;
            public Token? op;
            public Expr value;

            public Assign(Member member, Token op, Expr value)
            {
                this.member = member;
                this.op = op;
                this.value = value;
            }

            public Assign(Member member, Expr value)
            {
                this.member = member;
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

        public class Is : Expr
        {
            public Expr left;
            public Type right;

            public string value;

            public Is(Expr left, Type right)
            {
                this.left = left;
                this.right = right;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitIsExpr(this);
            }
        }

        public abstract class StackGet : Get
        {
            public StackData stack = new();

            public StackGet(Token name) : base(name)
            {
                this.name = name;
            }

            public StackGet(Type type, Token name) : base(name)
            {
                this.stack.type = type;
            }
        }

        public class Type : Expr
        {
            public Get type;
            public List<string>? literals = null;

            public Type(Get type)
            {
                this.type = type;
            }

            public Type(Get type, List<string> literals)
            {
                this.type = type;
                this.literals = literals;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return type.Accept(visitor);
            }

            public override string ToString()
            {
                return type.ToString();
            }
        }
    }
}
