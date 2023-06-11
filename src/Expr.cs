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

        public static void ListAccept<T, T2>(List<T> list, IVisitor<T2> visitor) where T : Expr
        {
            foreach (var expr in list)
            {
                expr.Accept(visitor);
            }
        }

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

        public class Declare : Named
        {
            public Expr? value;

            public Queue<Token> typeName;

            public StackData stack = new();
            public bool classScoped;

            public Declare(Queue<Token> typeName, Token name, Expr value) : base(name)
            {
                this.typeName = typeName;
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
            public bool instanceCall = false;

            public Definition funcEnclosing;
            public Function internalFunction { get => (Function)funcEnclosing; set => funcEnclosing = value; }

            public List<Expr> arguments;

            public Call(Token name, Queue<Token> callee, GetReference get, List<Expr> arguments)
            {
                this.name = name;
                this.callee = callee;
                this.offsets = typeName != null ? new StackData[typeName.Count] : null;
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
            public Type type;

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
            public StackData[] offsets;

            private protected GetReference() { }

            public GetReference(Queue<Token> typeName) : base(typeName)
            {
                offsets = new StackData[typeName.Count];
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitGetReferenceExpr(this);
            }
        }

        public class Block : Expr
        {
            public List<Expr> block;

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

        public class StackData
        {
            public int stackOffset;
            public Expr.Definition type;
            public bool plus;
            public int size;

            public StackData() { }

            public StackData(Definition type, bool plus, int size, int stackOffset)
            {
                (this.stackOffset, this.type, this.plus, this.size) = (stackOffset, type, plus, size);
            }
        }

        public class Variable : GetReference
        {
            public bool classScoped;

            public StackData stack
            {
                get => offsets[0];
                set => offsets[0] = value;
            }

            public Variable(Queue<Token> typeName) : base(typeName)
            {
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitVariableExpr(this);
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
            public DataType internalClass;

            public New(Call call)
            {
                this.call = call;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitNewExpr(this);
            }
        }

        public class Parameter
        {
            public Queue<Token> typeName;
            public Token name;

            public StackData stack = new();

            public Parameter(Queue<Token> typeName, Token name)
            {
                this.typeName = typeName;
                this.name = name;
            }
        }

        public abstract class Named : Expr
        {
            public Token name;

            public Named(Token name)
            {
                this.name = name;
            }
        }

        public class Type : Named
        {
            public Type? enclosing;

            public Func<Type, bool> _Matches;
            
            public Definition.DefinitionType definitionType;

            public Type(Token name) : base(name)
            {
                _Matches =
                    (x) =>
                    {
                        return x == this;
                    };
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                throw new Errors.ImpossibleError("type accepted");
            }

            public bool Matches(Type type)
            {
                return type._Matches(this) || ((enclosing != null) ? enclosing.Matches(type) : false);
            }

            public override string ToString()
            {
                return name.lexeme != "" ?
                        (enclosing != null ?
                            enclosing.ToString() + "." :
                            "")
                            + name.lexeme :
                        name.type.ToString();
            }
        }

        public abstract class Definition : Type
        {
            public enum DefinitionType
            {
                Function,
                Class,
                Primitive
            }

            public int size;
            public bool leaf = true;

            public Definition(Token name) : base(name)
            {
                
            }

            public Definition(Token name, int size)
                : this (name)
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
            public Dictionary<string, bool> modifiers;
            public bool constructor;
            public List<Expr> block;

            public Function(Dictionary<string, bool> modifiers, TypeReference _returnType, Token name, List<Parameter> parameters, List<Expr> block) : base(name)
            {
                this.definitionType = DefinitionType.Function;
                this.modifiers = modifiers;
                this._returnType = _returnType;
                this.parameters = parameters;
                this.block = block;
            }

            public override string ToString()
            {
                return (enclosing != null ?
                            enclosing.ToString() + "." :
                            "")
                            + name.lexeme + "(" + getParameters() + ")";
                string getParameters()
                {
                    if (parameters.Count == 0)
                    {
                        return "";
                    }

                    string res = "";

                    foreach (Parameter parameter in parameters.SkipLast(1))
                    {
                        if (parameter.typeName.Count == 0)
                        {
                            res += (parameter.stack.type + ", ");
                        }
                        else
                        {
                            res += (string.Join(".", parameter.typeName.ToList().ConvertAll(x => x.lexeme)) + ", ");
                        }
                    }
                    res += (parameters[parameters.Count - 1].typeName.Count == 0)? 
                        parameters[parameters.Count - 1].stack.type :
                        (string.Join(".", parameters[parameters.Count - 1].typeName.ToList().ConvertAll(x => x.lexeme)));

                    return res;
                }
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitFunctionExpr(this);
            }
        }

        public abstract class DataType : Definition
        {
            public TypeReference superclass;

            public List<Definition> definitions;

            public StackData _this = new();

            public DataType(Token name, List<Definition> definitions, TypeReference superclass) : base(name)
            {
                this.superclass = superclass;
                this.definitions = definitions;
                (_this.stackOffset, _this.size, _this.type) = (8, 8, this);
            }

            public DataType(Token name, List<Definition> definitions, int size, TypeReference superclass) : this(name, definitions, superclass)
            {
                this.size = size;
            }
        }

        public class Class : DataType
        {
            public List<Declare> declarations;

            public Class(Token name, List<Declare> declarations, List<Definition> definitions, TypeReference type) : base(name, definitions, type)
            {
                this.definitionType = DefinitionType.Class;
                this.declarations = declarations;
            }

            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitClassExpr(this);
            }
        }

        public class Primitive : DataType
        {
            public Primitive(Token name, List<Definition> definitions, int size, TypeReference type) : base(name, definitions, size, type)
            {
                this.definitionType = DefinitionType.Primitive;
            }


            public override T Accept<T>(IVisitor<T> visitor)
            {
                return visitor.visitPrimitiveExpr(this);
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
