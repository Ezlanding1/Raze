using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

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
        public T VisitBinaryExpr(Binary expr);
        public T VisitUnaryExpr(Unary expr);
        public T VisitGroupingExpr(Grouping expr);
        public T VisitLiteralExpr(Literal expr);
        public T VisitDeclareExpr(Declare expr);
        public T VisitIfExpr(If expr);
        public T VisitForExpr(For expr);
        public T VisitWhileExpr(While expr);
        public T VisitCallExpr(Call expr);
        public T VisitTypeReferenceExpr(TypeReference expr);
        public T VisitGetReferenceExpr(GetReference expr);
        public T VisitLogicalExpr(Logical epxr);
        public T VisitBlockExpr(Block expr);
        public T VisitAssemblyExpr(Assembly expr);
        public T VisitVariableExpr(Variable expr);
        public T VisitFunctionExpr(Function expr);
        public T VisitClassExpr(Class expr);
        public T VisitReturnExpr(Return expr);
        public T VisitAssignExpr(Assign expr);
        public T VisitPrimitiveExpr(Primitive expr);
        public T VisitKeywordExpr(Keyword expr);
        public T VisitNewExpr(New expr);
        public T VisitIsExpr(Is expr);
    }

    public class Binary : Expr
    {
        public Expr left;
        public Token op;
        public Expr right;

        public int encSize;

        public Expr.Function internalFunction;

        public Binary(Expr left, Token op, Expr right)
        {
            this.left = left;
            this.op = op;
            this.right = right;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitBinaryExpr(this);
        }

    }

    public class Unary : Expr
    {

        public Token op;
        public Expr operand;

        public Expr.Function internalFunction;

        public int encSize;

        public Unary(Token op, Expr operand)
        {
            this.op = op;
            this.operand = operand;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitUnaryExpr(this);
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
            return visitor.VisitGroupingExpr(this);
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
            return visitor.VisitLiteralExpr(this);
        }

    }

    public class Declare : Named
    {
        public Expr? value;

        public Queue<Token> typeName;

        public StackData stack = new();
        public bool classScoped;

        public Declare(Queue<Token> typeName, Token name, bool _ref, Expr value) : base(name)
        {
            this.typeName = typeName;
            this.stack._ref = _ref;
            this.value = value;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitDeclareExpr(this);
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
        public List<Conditional> conditionals = new();

        public Block? _else = null;

        public If(Conditional _if)
        {
            conditionals.Add(_if);
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitIfExpr(this);
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
            return visitor.VisitWhileExpr(this);
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
            return visitor.VisitForExpr(this);
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

        public int encSize;

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
            return visitor.VisitCallExpr(this);
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
            return visitor.VisitTypeReferenceExpr(this);
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
            return visitor.VisitGetReferenceExpr(this);
        }
    }

    public class Logical : Expr
    {
        public Expr left;
        public Token op;
        public Expr right;

        public Logical(Expr left, Token op, Expr right)
        {
            this.left = left;
            this.op = op;
            this.right = right;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitLogicalExpr(this);
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
            return visitor.VisitBlockExpr(this);
        }
    }

    public partial class Assembly : Expr
    {
        public List<ExprUtils.AssignableInstruction> block;
        public List<Variable> variables;

        public Assembly(List<ExprUtils.AssignableInstruction> block, List<Variable> variables)
        {
            this.block = block;
            this.variables = variables;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitAssemblyExpr(this);
        }
    }

    public class StackData
    {
        public int stackOffset;
        public Expr.Definition type;
        public bool plus;
        public bool _ref;
        public int size;
        public bool stackRegister;

        public StackData() { }

        public StackData(Definition type, bool _ref, bool plus, int size, int stackOffset)
        {
            (this.stackOffset, this.type, this._ref, this.plus, this.size) = (stackOffset, type, _ref, plus, size);
        }
    }

    public class StackRegister : StackData
    {
        public Instruction.Value register;

        public StackRegister() { }

        public StackRegister(Definition type, bool _ref, bool plus, int size, int stackOffset) : base(type, _ref, plus, size, stackOffset)
        {
        }
    }

    public class Variable : GetReference
    {
        public bool classScoped;

        public StackData Stack
        {
            get => offsets[0];
            set => offsets[0] = value;
        }

        public Variable(Queue<Token> typeName) : base(typeName)
        {
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitVariableExpr(this);
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
            return visitor.VisitKeywordExpr(this);
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
            return visitor.VisitNewExpr(this);
        }
    }

    public class Parameter
    {
        public Queue<Token> typeName;
        public Token name;

        public ExprUtils.Modifiers modifiers;

        public StackData stack;

        public Parameter(Queue<Token> typeName, Token name, ExprUtils.Modifiers modifiers)
        {
            this.typeName = typeName;
            this.name = name;
            this.modifiers = modifiers;
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
            return type._Matches(this) || ((enclosing != null) && enclosing.Matches(type));
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
        public int Arity
        {
            get { return parameters.Count; }
        }
        public ExprUtils.Modifiers modifiers;
        public bool constructor;
        public List<Expr> block;

        public Function(ExprUtils.Modifiers modifiers, TypeReference _returnType, Token name, List<Parameter> parameters, List<Expr> block) : base(name)
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
                res += (parameters[^1].typeName.Count == 0) ?
                    parameters[^1].stack.type :
                    (string.Join(".", parameters[^1].typeName.ToList().ConvertAll(x => x.lexeme)));

                return res;
            }
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitFunctionExpr(this);
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
            return visitor.VisitClassExpr(this);
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
            return visitor.VisitPrimitiveExpr(this);
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
            return visitor.VisitReturnExpr(this);
        }
    }

    public class Assign : Expr
    {
        public Variable member;
        public Expr value;

        public bool binary;

        public Assign(Variable member, Expr value)
        {
            this.member = member;
            this.value = value;
        }
        public Assign(Variable member, Expr.Binary value)
        {
            this.member = member;
            this.value = value;
            this.binary = true;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitAssignExpr(this);
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
            return visitor.VisitIsExpr(this);
        }
    }
}
