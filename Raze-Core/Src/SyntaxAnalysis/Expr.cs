using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public abstract class Expr
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
        public T VisitAmbiguousGetReferenceExpr(AmbiguousGetReference expr); 
        public T VisitInstanceGetReferenceExpr(InstanceGetReference expr); 
        public T VisitGetExpr(Get expr); 
        public T VisitTypeReferenceExpr(TypeReference expr);
        public T VisitLogicalExpr(Logical epxr);
        public T VisitBlockExpr(Block expr);
        public T VisitAssemblyExpr(Assembly expr);
        public T VisitFunctionExpr(Function expr);
        public T VisitClassExpr(Class expr);
        public T VisitReturnExpr(Return expr);
        public T VisitAssignExpr(Assign expr);
        public T VisitPrimitiveExpr(Primitive expr);
        public T VisitKeywordExpr(Keyword expr);
        public T VisitNewExpr(New expr);
        public T VisitIsExpr(Is expr);
        public T VisitNoOpExpr(NoOp expr);
    }

    public class Binary : Expr, ICall
    {
        public Expr left;
        public Token op;
        public Expr right;

        public Function internalFunction;

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

        public IList<Expr> Arguments => new Expr[] { left, right };
        public Function InternalFunction => internalFunction;
    }

    public class Unary : Expr, ICall
    {
        public Token op;
        public Expr operand;

        public Function internalFunction;

        public Unary(Token op, Expr operand)
        {
            this.op = op;
            this.operand = operand;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitUnaryExpr(this);
        }

        public IList<Expr> Arguments => new Expr[] { operand };
        public Function InternalFunction => internalFunction;
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
        public LiteralToken literal;

        public Literal(LiteralToken literal)
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

        internal ExprUtils.QueueList<Token> typeName;

        public StackData stack = new();
        public bool classScoped;

        internal Declare(ExprUtils.QueueList<Token> typeName, Token name, bool _ref, Expr value) : base(name)
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

    public class TypeReference : Expr
    {
        internal ExprUtils.QueueList<Token>? typeName;
        public DataType type;

        private protected TypeReference() { }

        internal TypeReference(ExprUtils.QueueList<Token>? typeName)
        {
            this.typeName = typeName;
        }

        internal TypeReference(ExprUtils.QueueList<Token>? typeName, DataType type) : this(typeName)
        {
            this.type = type;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitTypeReferenceExpr(this);
        }
    }

    public abstract class GetReference : Expr
    {
        public abstract StackData GetLastData();
        public abstract int GetLastSize();
        public abstract bool HandleThis();
        public abstract bool IsMethod();
    }

    public class AmbiguousGetReference : GetReference
    {
        internal ExprUtils.QueueList<Token> typeName;
        public StackData[]? datas;
        public bool classScoped;
        public bool ambiguousCall = true;
        public bool instanceCall
        {
            get => datas != null;
            set { datas = value ? new StackData[typeName.Count] : null; ambiguousCall = false; }
        }

        internal AmbiguousGetReference(ExprUtils.QueueList<Token> typeName)
        {
            this.typeName = typeName;
        }
        internal AmbiguousGetReference(ExprUtils.QueueList<Token> typeName, bool instanceCall) : this(typeName)
        {
            this.instanceCall = instanceCall;
        }
        public AmbiguousGetReference(Token name, bool instanceCall)
        {
            this.typeName = new();
            this.typeName.Enqueue(name);
            this.instanceCall = instanceCall;
        }

        public override StackData GetLastData() => datas[^1];
        public override int GetLastSize() => datas[^1].size;
        public override bool HandleThis()
        {
            if (typeName.Count == 1 && typeName.Peek().lexeme == "this") 
            { 
                typeName.Peek().type = Token.TokenType.IDENTIFIER; 
                return true; 
            }
            return false;
        }
        public override bool IsMethod() => false;

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitAmbiguousGetReferenceExpr(this);
        }
    }

    public class InstanceGetReference : GetReference
    {
        public List<Getter> getters;

        private protected InstanceGetReference() { }

        public InstanceGetReference(List<Getter> getters)
        {
            this.getters = getters;
        }

        public override StackData GetLastData() => ((Get)getters[^1]).data;
        public override int GetLastSize() => (getters[^1] is Get) ? ((Get)getters[^1]).data.size : ((Call)getters[^1]).internalFunction._returnType.type.allocSize;
        public override bool HandleThis()
        {
            if (getters.Count == 1 && getters[0].name.lexeme == "this")
            {
                getters[0].name.type = Token.TokenType.IDENTIFIER;
                return true;
            }
            return false;
        }
        public override bool IsMethod() => getters[^1] is Call;
        public bool HasMethod() => getters.Any(x => x is Call);

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitInstanceGetReferenceExpr(this);
        }
    }

    public abstract class Getter : Expr
    {
        public Token name;
        public Getter(Token name) => this.name = name;
    }

    public interface ICall
    {
        public IList<Expr> Arguments { get; }
        public Function InternalFunction { get; }
    }

    public class Call : Getter, ICall
    {
        public GetReference? callee;

        public bool constructor;
        public bool instanceCall
        {
            get
            {
                if (callee is AmbiguousGetReference ambigGetRef)
                {
                    return ambigGetRef.instanceCall;
                }
                else return true;
            }
        }

        public Function internalFunction;

        public List<Expr> arguments;

        public Call(Token name, List<Expr> arguments, GetReference? callee) : base(name)
        {
            this.arguments = arguments;
            this.callee = callee;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitCallExpr(this);
        }

        public IList<Expr> Arguments => arguments;
        public Function InternalFunction => internalFunction;
    }

    public class Get : Getter
    {
        public StackData data;

        public Get(Token name) : base(name) { }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitGetExpr(this);
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
        internal List<ExprUtils.AssignableInstruction> block;
        public List<(AssemblyExpr.Register.RegisterSize, GetReference)> variables;

        internal Assembly(List<ExprUtils.AssignableInstruction> block, List<(AssemblyExpr.Register.RegisterSize, GetReference)> variables)
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
        internal DataType type { get; set; }
        internal AssemblyExpr.Value value;
        internal int size => type.allocSize;
        internal bool _ref;
        internal bool inlinedData;
        internal AssemblyExpr.Pointer ValueAsPointer => (AssemblyExpr.Pointer)value;
        internal AssemblyExpr.Register ValueAsRegister => (AssemblyExpr.Register)value;

        public StackData()
        {
        }

        public StackData(DataType type, bool _ref)
        {
            this.type = type;
            this._ref = _ref;
        }

        public AssemblyExpr.Pointer CreateValueAsPointer(AssemblyExpr.Register.RegisterName name, int offset)
        {
            value = new AssemblyExpr.Pointer(name, -offset, size);
            return ValueAsPointer;
        }
        public AssemblyExpr.Register CreateValueAsRegister(AssemblyExpr.Register.RegisterName name)
        {
            value = new AssemblyExpr.Register(name, size);
            return ValueAsRegister;
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
        public Class internalClass;

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
        internal ExprUtils.QueueList<Token> typeName;
        public Token name;

        internal ExprUtils.Modifiers modifiers;

        public StackData stack;

        internal Parameter(ExprUtils.QueueList<Token> typeName, Token name, ExprUtils.Modifiers modifiers)
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
        
        public Definition.DefinitionType definitionType;

        public Type(Token name) : base(name)
        {
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("type accepted"));
        }

        public virtual bool Match(Type type)
        {
            return type == this;
        }
        public virtual bool Matches(Type type)
        {
            return type.Match(this);
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
        public int Arity
        {
            get { return parameters.Count; }
        }
        internal ExprUtils.Modifiers modifiers;
        public bool constructor;
        public bool dead = true;
        public Block block;

        internal Function(ExprUtils.Modifiers modifiers, TypeReference _returnType, Token name, List<Parameter> parameters, Block block) : base(name)
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
        public List<Definition> definitions;

        public StackData _this = new();

        public abstract int allocSize { get; }

        public DataType(Token name, List<Definition> definitions) : base(name)
        {
            this.definitions = definitions;
            this._this.type = this;
            this._this.value = new AssemblyExpr.Pointer(8, AssemblyExpr.Register.RegisterSize._64Bits);
        }

        public DataType(Token name, List<Definition> definitions, int size) : this(name, definitions)
        {
            this.size = size;
        }
    }

    public class Class : DataType
    {
        public TypeReference superclass;

        public List<Declare> declarations;

        public override int allocSize => 8;

        public Class(Token name, List<Declare> declarations, List<Definition> definitions, TypeReference superclass) : base(name, definitions)
        {
            this.definitionType = DefinitionType.Class;
            this.declarations = declarations;
            this.superclass = superclass;
        }

        public int CalculateSize() => size == 0? 1 : size;

        public override bool Matches(Type type)
        {
            return type.Match(this) || (this.superclass.type != null && this.superclass.type.Matches(type));
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitClassExpr(this);
        }
    }

    public class Primitive : DataType
    {
        public (string? name, Type type) superclass = new();

        public override int allocSize => size;

        public Primitive(Token name, List<Definition> definitions, int size, string? superclass) : base(name, definitions, size)
        {
            this.definitionType = DefinitionType.Primitive;
            this.superclass.name = superclass;
        }

        public override bool Match(Type type)
        {
            return this == type || this.superclass.type == type;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitPrimitiveExpr(this);
        }
    }

    public class Return : Expr
    {
        public Expr value;

        public Return(Expr value)
        {
            this.value = value;
        }

        public bool IsVoid(Definition current) =>
            Analyzer.Primitives.IsVoidType(current);

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitReturnExpr(this);
        }
    }

    public class Assign : Expr
    {
        public GetReference member;
        public Expr value;

        public bool binary;

        public Assign(GetReference member, Expr value)
        {
            this.member = member;
            this.value = value;
        }
        public Assign(GetReference member, Expr.Binary value)
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
    
    public abstract class NoOp : Expr
    {
        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitNoOpExpr(this);
        }
    }
    public class InvalidExpr : NoOp { }
    public class SynchronizationExpr : NoOp { }
}
