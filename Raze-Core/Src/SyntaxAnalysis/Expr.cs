using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public abstract partial class Expr
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
        public T VisitInlineAssemblyExpr(InlineAssembly expr);
        public T VisitFunctionExpr(Function expr);
        public T VisitClassExpr(Class expr);
        public T VisitReturnExpr(Return expr);
        public T VisitAssignExpr(Assign expr);
        public T VisitPrimitiveExpr(Primitive expr);
        public T VisitKeywordExpr(Keyword expr);
        public T VisitNewExpr(New expr);
        public T VisitIsExpr(Is expr);
        public T VisitAsExpr(As expr);
        public T VisitImportExpr(Import expr);
        public T VisitHeapAllocExpr(HeapAlloc expr);
        public T VisitNoOpExpr(NoOp expr);
    }

    public class Binary : Invokable
    {
        public Expr left;
        public Token op => name;
        public Expr right;

        public Binary(Expr left, Token op, Expr right) : base(op)
        {
            this.left = left;
            this.right = right;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitBinaryExpr(this);
        }

        public override IList<Expr> Arguments => [left, right];
        public override bool IsMethodCall => op.type != Token.TokenType.LBRACKET;
    }

    public class Unary : Invokable
    {
        public Token op => name;
        public Expr operand;

        public Unary(Token op, Expr operand) : base(op)
        {
            this.operand = operand;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitUnaryExpr(this);
        }

        public override IList<Expr> Arguments => [operand];
    }

    public class Grouping : Getter
    {
        public Expr expression;
        public Type type;

        public Grouping(Expr expression) : base(null!)
        {
            this.expression = expression;
        }

        public override DataType Type => Analyzer.TypeCheckUtils.ToDataTypeOrDefault(type);
        public override bool IsMethodCall => true;

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

        internal Declare(ExprUtils.QueueList<Token> typeName, Token name, bool _ref, Expr? value) : base(name)
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

    public abstract class GetReference(bool _ref) : Expr
    {
        public bool _ref = _ref;
        public abstract StackData? GetLastData();
        public abstract Token GetLastName();
        public abstract DataType GetLastType();
        public abstract int GetLastSize();
        public abstract bool IsMethodCall();
        public abstract bool HandleThis();
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

        internal AmbiguousGetReference(ExprUtils.QueueList<Token> typeName, bool _ref) : base(_ref)
        {
            this.typeName = typeName;
        }
        internal AmbiguousGetReference(ExprUtils.QueueList<Token> typeName, bool instanceCall, bool _ref) : this(typeName, _ref)
        {
            this.instanceCall = instanceCall;
        }
        public AmbiguousGetReference(Token name, bool instanceCall) : base(false)
        {
            this.typeName = new();
            this.typeName.Enqueue(name);
            this.instanceCall = instanceCall;
        }

        public override StackData? GetLastData() => datas[^1];
        public override Token GetLastName() => typeName[^1];
        public override DataType GetLastType() => GetLastData().type;
        public override int GetLastSize() => GetLastData().size;
        public override bool IsMethodCall() => false;
        public override bool HandleThis()
        {
            if (typeName.Count == 1 && typeName.Peek().lexeme == "this")
            {
                typeName.Peek().type = Token.TokenType.IDENTIFIER;
                return true;
            }
            return false;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitAmbiguousGetReferenceExpr(this);
        }

    }

    public class InstanceGetReference : GetReference
    {
        // getters[0].name may be null if getters.Count > 1
        public List<Getter> getters;

        public InstanceGetReference(List<Getter> getters, bool _ref) : base(_ref)
        {
            this.getters = getters;
        }

        public override StackData? GetLastData() => (getters[^1] as Get)?.data;
        public override Token GetLastName() => getters[^1].name;
        public override DataType GetLastType() => getters[^1].Type;
        public override int GetLastSize() => getters[^1].Type.allocSize;
        public override bool IsMethodCall() => getters[^1].IsMethodCall;
        public override bool HandleThis()
        {
            if (getters.Count == 1 && getters[0].name.lexeme == "this")
            {
                getters[0].name.type = Token.TokenType.IDENTIFIER;
                return true;
            }
            return false;
        }
        public bool HasMethod() => getters.Any(x => x is Call);

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitInstanceGetReferenceExpr(this);
        }
    }

    public abstract class Getter(Token name) : Expr
    {
        public Token name = name;
        public abstract DataType Type { get; }
        public abstract bool IsMethodCall { get; }
    }

    public abstract class Invokable(Token name) : Getter(name)
    {
        public abstract IList<Expr> Arguments { get; }
        public Function internalFunction;
        public override DataType Type => internalFunction._returnType.type;
        public override bool IsMethodCall => true;
    }

    public class Call : Invokable
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
        public List<Expr> arguments;

        public Call(Token name, List<Expr> arguments, GetReference? callee) : base(name)
        {
            this.arguments = arguments;
            this.callee = callee;
        }

        public static string CallNameToString(string name, Type[] types) =>
            name + "(" + string.Join(", ", (object?[])types) + ")";

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitCallExpr(this);
        }

        public override IList<Expr> Arguments => arguments;
    }

    public class Get : Getter
    {
        public StackData data;
        public override DataType Type => data.type;
        public override bool IsMethodCall => false;

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

    public partial class InlineAssembly : Expr
    {
        public List<InlineAssemblyExpr> instructions;

        public InlineAssembly(List<InlineAssemblyExpr> instructions)
        {
            this.instructions = instructions;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitInlineAssemblyExpr(this);
        }
    }

    public class StackData
    {
        internal DataType type { get; set; }
        internal AssemblyExpr.IValue value;
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
            value = new AssemblyExpr.Pointer(name, -offset, (AssemblyExpr.Register.RegisterSize)size);
            return ValueAsPointer;
        }
        public AssemblyExpr.Register CreateValueAsRegister(AssemblyExpr.Register.RegisterName name)
        {
            value = new AssemblyExpr.Register(name, size);
            return ValueAsRegister;
        }
    }

    public class ThisStackData : StackData
    {
        public ThisStackData(int size)
        {
            value = new AssemblyExpr.Pointer(AssemblyExpr.Register.RegisterName.RBP, -size, (AssemblyExpr.Register.RegisterSize)size);
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

    public class New : Getter
    {
        public Call call;
        public Class internalClass;

        public New(Call call) : base(null!)
        {
            this.call = call;
        }

        public override DataType Type => internalClass;
        public override bool IsMethodCall => true;

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

        public override string ToString()
        {
            string result = modifiers["ref"]? "ref " : "";

            result += (typeName.Count == 0) ?
                stack.type :
                string.Join(".", typeName.ToList().ConvertAll(x => x.lexeme));

            return result;
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
        public int size;

        public Definition(Token name) : base(name)
        {

        }

        public Definition(Token name, int size)
            : this(name)
        {
            this.size = size;
        }

        public abstract override T Accept<T>(IVisitor<T> visitor);
    }

    public class Function : Definition
    {
        public List<Parameter> parameters;
        public bool refReturn;
        public TypeReference _returnType;
        public int Arity
        {
            get { return parameters.Count; }
        }
        internal ExprUtils.Modifiers modifiers;
        public bool constructor;
        public bool dead = true;
        public Block? block;
        public bool Abstract => block == null;

        internal Function(ExprUtils.Modifiers modifiers, bool refReturn, TypeReference _returnType, Token name, List<Parameter> parameters, Block? block) : base(name)
        {
            this.modifiers = modifiers;
            this.refReturn = refReturn;
            this._returnType = _returnType;
            this.parameters = parameters;
            this.block = block;
        }

        public override string ToString()
        {
            return (enclosing != null ?
                        enclosing.ToString() + "." :
                        "")
                        + name.lexeme + "(" + GetParameters() + ")";

            string GetParameters() =>
                string.Join(", ", parameters);
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitFunctionExpr(this);
        }
    }

    public abstract class DataType : Definition
    {
        abstract public Type SuperclassType { get; }

        public List<Definition> definitions;

        private static Dictionary<int, ThisStackData> _thisTable = new()
        {
            { 1, new(1) },
            { 2, new(2) },
            { 4, new(4) },
            { 8, new(8) }
        };
        public static ThisStackData This(int size) => _thisTable[size];

        public abstract int allocSize { get; }

        public DataType(Token name, List<Definition> definitions) : base(name)
        {
            this.definitions = definitions;
        }

        public DataType(Token name, List<Definition> definitions, int size) : this(name, definitions)
        {
            this.size = size;
        }

        private List<Function>? virtualMethods;
        public List<Function> GetVirtualMethods()
        {
            if (virtualMethods != null)
            {
                return virtualMethods;
            }
            virtualMethods = new List<Function>((SuperclassType as DataType)?.GetVirtualMethods() ?? []);

            foreach (var function in definitions.Where(x => x is Function func && (func.modifiers["virtual"] || func.modifiers["override"])).Cast<Function>())
            {
                int idx = virtualMethods.FindIndex(x => Analyzer.SymbolTable.MatchFunction(function, x));
                if (idx == -1)
                {
                    virtualMethods.Add(function);
                }
                else
                {
                    function.dead = function.dead && virtualMethods[idx].dead;
                    virtualMethods[idx] = function;
                }
            }
            return virtualMethods;
        }

        public int GetOffsetOfVTableMethod(Function function) =>
            (GetVirtualMethods().ToList().IndexOf(function) + 1) * 8;
    }

    public class Class : DataType
    {
        public TypeReference superclass;
        public override Type SuperclassType => superclass.type;
        public List<Declare> declarations;
        public bool trait;
        public override int allocSize => 8;
        public bool emitVTable;

        public Class(Token name, List<Declare> declarations, List<Definition> definitions, TypeReference superclass, bool trait=false) : base(name, definitions)
        {
            this.declarations = declarations;
            this.superclass = superclass;
            this.superclass.typeName ??= new([new Token(Token.TokenType.IDENTIFIER, "object", Location.NoLocation)]);
            this.trait = trait;
        }

        public override bool Matches(Type type)
        {
            return type.Match(this) || (this.superclass.type != null && this.superclass.type.Matches(type));
        }

        public void CalculateSizeAndAllocateVariables()
        {
            size = (emitVTable || GetVirtualMethods().Count != 0) ? 8 : 0;
            foreach (var declaration in declarations)
            {
                CodeGen.RegisterAlloc.AllocateHeapVariable(this, declaration.stack);
            }
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitClassExpr(this);
        }
    }

    public class Primitive : DataType
    {
        public (string? typeName, Type type) superclass = new();
        public override Type SuperclassType => superclass.type;

        public override int allocSize => size;

        public Primitive(Token name, List<Definition> definitions, int size, string? superclass) : base(name, definitions, size)
        {
            this.superclass.typeName = superclass;
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

    public class Is(Expr left, TypeReference right) : Expr
    {
        public Expr left = left;
        public TypeReference right = right;
        // true = true, false = false, null = unresolved (check vTables)
        public bool? value;

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitIsExpr(this);
        }
    }

    public class As(Expr left, TypeReference right) : Expr
    {
        public readonly Is _is = new(left, right);

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitAsExpr(this);
        }
    }

    public class Import : Expr
    {
        internal FileInfo fileInfo;
        internal bool customPath;
        internal ImportType importType;

        internal Import(FileInfo fileInfo, bool customPath, ImportType importType, bool validate=true)
        {
            this.fileInfo = fileInfo;
            this.customPath = customPath;
            this.importType = importType;

            if (!FindImportPath() && validate)
            {
                Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.ImportNotFound, fileInfo.FullName));
            }
        }

        private bool FindImportPath()
        {
            if (!customPath)
            {
                foreach (DirectoryInfo directory in Diagnostics.importDirs)
                {
                    var path = Path.Join(directory.FullName, fileInfo.Name);
                    if (File.Exists(path))
                    {
                        fileInfo = new(path);
                        return true;
                    }
                }
            }
            return fileInfo.Exists;
        }

        public static List<Import> GenerateAutoImports()
        {
            var runtimeImports = Analyzer.SymbolTable.runtimeImports;
            var stdImports = Analyzer.SymbolTable.standardLibraryImports;

            var autoImports = runtimeImports.Values.Concat(stdImports).ToList();
            return autoImports;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitImportExpr(this);
        }

        internal class ImportType(TypeReference typeRef, bool importAll)
        {
            internal TypeReference typeRef = typeRef;
            internal bool importAll = importAll;
        }

        public readonly struct FileInfo
        {
            internal readonly System.IO.FileInfo _fileInfo;

            public FileInfo(string fileName)
            {
                this._fileInfo = new System.IO.FileInfo(fileName);
            }
            public FileInfo(System.IO.FileInfo _fileInfo)
            {
                this._fileInfo = _fileInfo;
            }

            internal bool Exists => _fileInfo.Exists;
            internal string Name => _fileInfo.Name;
            internal string FullName => _fileInfo.FullName;

            public override int GetHashCode() =>
                _fileInfo.FullName.GetHashCode();
            public override bool Equals(object? obj)
                => obj is FileInfo fileInfo && fileInfo._fileInfo.FullName == this._fileInfo.FullName;

            public static bool operator ==(FileInfo left, FileInfo right) => left.Equals(right);
            public static bool operator !=(FileInfo left, FileInfo right) => !(left == right);
        }
    }

    public class HeapAlloc(Expr size) : Getter(null!)
    {
        public Expr size = size;

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitHeapAllocExpr(this);
        }

        public override DataType Type => Analyzer.TypeCheckUtils.heapallocType.Value;
        public override bool IsMethodCall => true;
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
