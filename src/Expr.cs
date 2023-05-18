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
            public T visitIfExpr(If expr);
            public T visitForExpr(For expr);
            public T visitWhileExpr(While expr);
            public T visitCallExpr(Call expr);
            public T visitTypeReferenceExpr(TypeReference expr);
            public T visitGetReferenceExpr(GetReference expr);
            public T visitBlockExpr(Block expr);
            public T visitAssemblyExpr(Assembly expr);
            public T visitVariableExpr(Variable expr);
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

            public Expr? value;

            public TypeReference type;

            public StackData stack = new();

            public Declare(TypeReference type, Token name, Expr value)
            {
                this.type = type;
                this.name = name;
                this.value = value;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitDeclareExpr(this);
            }

        }

        public class Conditional
        {
            public Expr condition;
            public Block block;

            public Conditional(Expr condition, Block block)
            {
                this.condition = condition;
                this.block = block;
            }
        }

        public class If : Expr
        {
            public Conditional conditional;

            public List<ElseIf> ElseIfs;
            public Else _else;
            public If(Expr condition, Block block)
            {
                this.conditional = new(condition, block);
                this.ElseIfs = new();
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitIfExpr(this);
            }
        }
        public class ElseIf
        {
            public Conditional conditional;

            public ElseIf(Expr condition, Block block)
            {
                this.conditional = new(condition, block);
            }
        }
        public class Else
        {
            public Conditional conditional;

            public Else(Block block)
            {
                this.conditional = new(null, block);
            }
        }

        public class While : Expr
        {
            public Conditional conditional;

            public While(Expr condition, Block block)
            {
                conditional = new(condition, block);
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitWhileExpr(this);
            }
        }

        public class For : Expr
        {
            public Conditional conditional;

            public Expr initExpr;
            public Expr updateExpr;

            public For(Expr condition, Block block, Expr initExpr, Expr updateExpr)
            {
                this.conditional = new(condition, block);
                this.initExpr = initExpr;
                this.updateExpr = updateExpr;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitForExpr(this);
            }
        }

        public class Call : GetReference
        {
            public Token name;
            public Queue<Token> callee { get => typeName; set => typeName = value; } 
            public TypeReference get;
            public bool constructor;
            public Function internalFunction;
            public List<Expr> arguments;
            public int stackOffset;

            public Call(Token name, Queue<Token> callee, GetReference get, List<Expr> arguments)
            {
                this.name = name;
                this.callee = callee;
                this.offsets = typeName != null ? new LimitedStackData[typeName.Count] : null;
                this.get = get;
                this.arguments = arguments;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitCallExpr(this);
            }

        }

        public class TypeReference : Expr
        {
            public Queue<Token> typeName;
            public Analyzer.Type type;

            private protected TypeReference() { }

            public TypeReference(Queue<Token> typeName)
            {
                this.typeName = typeName;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitTypeReferenceExpr(this);
            }
        }

        public class GetReference : TypeReference
        {
            public LimitedStackData[] offsets;

            private protected GetReference() { }

            public GetReference(Queue<Token> typeName) : base(typeName)
            {
                offsets = new LimitedStackData[typeName.Count];
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitGetReferenceExpr(this);
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

        public class LimitedStackData
        {
            public int stackOffset;

            public LimitedStackData() { }

            public LimitedStackData(int stackOffset)
            {
                this.stackOffset = stackOffset;
            }
        }

        public class StackData : LimitedStackData
        {
            public Analyzer.Type type;
            public bool plus;
            public int size;
            public bool classScoped;

            public StackData() { }

            public StackData(int stackOffset) : base(stackOffset)
            {
            }

            public StackData(Analyzer.Type type, bool plus, int size, int stackOffset, bool classScoped) : base(stackOffset)
            {
                (this.type, this.plus, this.size, this.classScoped) = (type, plus, size, classScoped);
            }
        }

        public class Variable : GetReference
        {
            public StackData stack
            {
                get => (StackData)offsets[0];
                set => offsets[0] = value;
            }

            public (bool, Literal) define;

            public Variable(Queue<Token> typeName) : base(typeName)
            {
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitVariableExpr(this);
            }
        }

        public class Primitive : Definition
        {
            public List<string> literals;

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
            public TypeReference type;
            public Token name;

            public StackData stack = new();

            public Parameter(TypeReference type, Token name)
            {
                this.type = type;
                this.name = name;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitTypeReferenceExpr(this.type);
            }
        }

        public abstract class Definition : Expr
        {
            public Token name;
            public Block block;
            public int size;

            public Analyzer.Type type;

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
            public TypeReference _returnType;
            public int _returnSize;
            public int arity
            {
                get { return parameters.Count; }
            }
            public bool leaf = true;
            public Dictionary<string, bool> modifiers;
            public bool constructor;

            public Function()
                : base(null, null)
            {
                this.modifiers = new(){
                    { "static", false },
                    { "unsafe", false }
                };
            }

            public void Add(TypeReference _returnType, Token name, List<Parameter> parameters, Block block)
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
            public Variable member;
            public Token? op;
            public Expr value;

            public Assign(Variable member, Token op, Expr value)
            {
                this.member = member;
                this.op = op;
                this.value = value;
            }

            public Assign(Variable member, Expr value)
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
            public TypeReference right;

            public string value;

            public Is(Expr left, TypeReference right)
            {
                this.left = left;
                this.right = right;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitIsExpr(this);
            }
        }
    }
}
