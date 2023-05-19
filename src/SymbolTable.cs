using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze
{
    internal partial class Analyzer
    {
        internal class SymbolTable
        {
            public Type global = new(null, null);

            private Symbol.Class head;
            private Symbol.Container current;
            public Symbol.Container Current
            {
                get { return this.current; }
                private set
                {
                    this.current = value;
                }
            }

            private Block block;

            public Expr.Function main = null;

            public SymbolTable()
            {
                this.head = new Symbol.Class(null);
                this.Current = this.head;
                this.block = new(null);
            }

            public void Add(Expr.StackData v, Token name, Symbol.Container definition)
            {
                block.keys.Add(name.lexeme);

                var _ = new Symbol.Variable(v, name, definition);

                Current.self.size += v.size;
                v.stackOffset = Current.self.size;

                Current.variables.Add(_.Name.lexeme, _);
            }

            public void Add(Expr.Class c)
            {
                var _ = new Symbol.Class(c);
                Current.containers.Add(_.Name.lexeme, _);
                _.enclosing = Current;
                Current = _;
            }

            public void Add(Expr.Primitive p)
            {
                var _ = new Symbol.Primitive(p);
                Current.containers.Add(_.Name.lexeme, _);
                _.enclosing = Current;
                Current = _;
            }

            public void Add(Expr.Function f)
            {
                var _ = new Symbol.Function(f);
                Current.containers.Add(_.Name.lexeme, _);
                _.enclosing = Current;
                Current = _;
            }

            public void Add(Expr.Parameter p, Symbol.Container definition, int i, int arity)
            {
                block.keys.Add(p.name.lexeme);

                var _ = new Symbol.Variable(p.stack, p.name, definition);

                if (i < InstructionInfo.paramRegister.Length)
                {
                    Current.self.size += p.stack.size;
                    p.stack.stackOffset = Current.self.size;
                }
                else
                {
                    p.stack.plus = true;
                    p.stack.stackOffset = (8 * ((arity - i))) + 8;
                }

                Current.variables.Add(_.Name.lexeme, _);
            }

            //public void Add(Expr.Define d)
            //{
            //    var _ = new Symbol.Define(d);
            //    Current.variables.Add(_.Name.lexeme, _);
            //}

            // 'Get' Methods:

            public Symbol.Variable GetVariable(string key)
            {
                return GetVariable(key, out _);
            }
            public Symbol.Variable GetVariable(string key, out bool isClassScoped)
            {
                if(Current.variables.TryGetValue(key, out var value))
                {
                    isClassScoped = Current.IsClass();
                    return value;
                }

                if (Current.IsFunc() && (!((Symbol.Function)Current).self.modifiers["static"]))
                {
                    if (Current.enclosing.variables.TryGetValue(key, out var classValue))
                    {
                        isClassScoped = true;
                        return classValue;
                    }
                }
                throw new Errors.AnalyzerError("Undefined Reference", $"The variable '{key}' does not exist in the current context");
            }
            public Symbol.Container GetContainer(string key, bool func = false)
            {
                if (Current.containers.TryGetValue(key, out var value))
                {
                    return value;
                }
                throw new Errors.AnalyzerError("Undefined Reference", $"The {(func ? "function" : "class")} '{key}' does not exist in the current context");
            }
            public Symbol.Container GetClassFullScope(string key)
            {
                var x = Current;

                if (x.IsFunc())
                {
                    x = x.enclosing;
                }

                while (!(x == head))
                {

                    if (x.Name.lexeme == key)
                    {
                        return x;
                    }
                    x = x.enclosing;
                }

                if (x.containers.TryGetValue(key, out Symbol.Container value))
                {
                    if (value.IsFunc())
                    {
                        throw new Errors.AnalyzerError("Undefined Reference", $"The class '{key}' does not exist in the current context");
                    }
                    return value;
                }

                throw new Errors.AnalyzerError("Undefined Reference", $"The class '{key}' does not exist in the current context");


            }

            // 'TryGet' Methods:

            public bool TryGetVariable(string key, out Symbol.Variable symbol, out bool isClassScoped, bool ignoreEnclosing = false)
            {
                if (Current.variables.TryGetValue(key, out var value))
                {
                    symbol = value;
                    isClassScoped = Current.IsClass();
                    return true;
                }

                if (!ignoreEnclosing && Current.IsFunc() && (!((Symbol.Function)Current).self.modifiers["static"]))
                {
                    if (Current.enclosing.variables.TryGetValue(key, out var classValue))
                    {
                        symbol = classValue;
                        isClassScoped = true;
                        return true;
                    }
                }

                symbol = null;
                isClassScoped = false;
                return false;
            }

            public bool TryGetContainer(string key, out Symbol.Container symbol)
            {
                if (Current.containers.TryGetValue(key, out var value))
                {
                    symbol = value;
                    return true;
                }
                symbol = null;
                return false;
            }


            public Symbol.Container NearestEnclosingClass()
            {
                // Assumes a function is enclosed by a class (no nested functions)
                return Current.IsFunc() ? Current.enclosing : Current;
            }


            public void UpContext()
            {
                Current = Current.enclosing
                    ?? throw new Errors.ImpossibleError("Up Context Called On 'GLOBAL' context (no enclosing)");
            }

            public void CreateBlock()
            {
                Block nBlock = new(block);
                block = nBlock;
            }

            public void RemoveUnderCurrent()
            {
                foreach (var key in block.keys)
                {
                    Current.variables.Remove(key);
                }
                block = block.enclosing;
            }


            public void TopContext() => Current = head;

            public bool CurrentIsTop() => Current == head;

            public void SetContext(Symbol.Container container) => Current = container;

            class Block
            {
                public Block enclosing;
                public List<string> keys;

                public Block(Block enclosing)
                {
                    this.enclosing = enclosing;
                    this.keys = new();
                }
            }

            abstract internal class Symbol
            {
                public abstract Token Name { get; }

                public enum SymbolType
                {
                    Class,
                    Function,
                    Primitive,
                    Variable,
                    Define
                }

                private SymbolType type;


                public bool IsClass() => type == SymbolType.Class;
                public bool IsFunc() => type == SymbolType.Function;
                public bool IsPrimitive() => type == SymbolType.Primitive;
                public bool IsVariable() => type == SymbolType.Variable;
                public bool IsDefine() => type == SymbolType.Define;

                public Symbol(SymbolType type)
                {
                    this.type = type;
                }

                abstract internal class Container : Symbol
                {
                    internal Container enclosing;

                    internal abstract Dictionary<string, Variable> variables { get; }
                    internal abstract Dictionary<string, Container> containers { get; }

                    abstract internal Expr.Definition self
                    {
                        get;
                    }

                    public Container(SymbolType type) : base(type)
                    {
                        this.enclosing = null;
                    }
                }

                internal class Class : Container
                {
                    public override Token Name { get { return self.name; } }

                    internal override Dictionary<string, Variable> variables
                    {
                        get => _variables;
                    }
                    internal override Dictionary<string, Container> containers
                    {
                        get => _containers;
                    }

                    internal Dictionary<string, Variable> _variables;
                    internal Dictionary<string, Container> _containers;


                    override internal Expr.Class self
                    {
                        get
                        {
                            return _self;
                        }
                    }
                    internal Expr.Class _self;

                    public Class(Expr.Class _self) : base(SymbolType.Class)
                    {
                        this._variables = new();
                        this._containers = new();
                        this._self = _self;
                    }

                }

                internal class Function : Container
                {
                    public override Token Name { get { return self.name; } }

                    internal override Dictionary<string, Variable> variables
                    {
                        get => _variables;
                    }
                    internal override Dictionary<string, Container> containers
                    {
                        get => throw new Errors.ImpossibleError("Requested Access of function's containers (null)");
                    }

                    internal Dictionary<string, Variable> _variables;

                    override internal Expr.Function self
                    {
                        get
                        {
                            return _self;
                        }
                    }
                    internal Expr.Function _self;

                    public Function(Expr.Function _self) : base(SymbolType.Function)
                    {
                        this._variables = new();
                        this._self = _self;
                    }
                }

                internal class Primitive : Container
                {
                    public override Token Name { get { return self.name; } }

                    internal override Dictionary<string, Container> containers
                    {
                        get => _containers;
                    }
                    internal override Dictionary<string, Variable> variables
                    {
                        get => throw new Errors.ImpossibleError("Requested Access of primitive's variables (null)");
                    }

                    internal Dictionary<string, Container> _containers;

                    override internal Expr.Primitive self
                    {
                        get
                        {
                            return _self;
                        }
                    }
                    internal Expr.Primitive _self;

                    public Primitive(Expr.Primitive _self) : base(SymbolType.Primitive)
                    {
                        this._containers = new();
                        this._self = _self;
                    }
                }

                internal class Variable : Symbol
                {
                    private Token name;
                    public override Token Name { get { return name; } }
                    public Container definition;

                    internal Expr.StackData self;

                    public Variable(Expr.StackData self, Token name, Container definition) : base(SymbolType.Variable)
                    {
                        this.self = self;
                        this.name = name;
                        this.definition = definition;
                    }
                }

                internal class Define : Symbol
                {
                    public override Token Name { get { return self.name; } }

                    internal Expr.Define self;

                    public Define(Expr.Define self) : base(SymbolType.Define)
                    {
                        this.self = self;
                    }
                }
            }
        }
    }
}
