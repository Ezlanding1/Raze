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
        internal struct Other
        {
            public HashSet<Expr.StackData> classScopedVars = new();
            public Expr.Function main = null;
            public Other()
            {
            }
        }

        internal class SymbolTable
        {
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

            public Other other = new();

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

            //public void Add(Expr.Define d)
            //{
            //    var _ = new Symbol.Define(d);
            //    Current.variables.Add(_.Name.lexeme, _);
            //}

            // 'Get' Methods:

            public Symbol.Variable GetVariable(string key)
            {
                if (Current.variables.TryGetValue(key, out var value))
                {
                    return value;
                }
                throw new Errors.AnalyzerError("Undefined Reference", $"The variable '{key}' does not exist in the current context");
            }
            public Symbol.Container GetContainer(string key, bool func=false)
            {
                if (Current.containers.TryGetValue(key, out var value))
                {
                    return value;
                }
                throw new Errors.AnalyzerError("Undefined Reference", $"The {(func ? "function" : "class")} '{key}' does not exist in the current context");
            }
            public Symbol.Container GetContainer(string key, Symbol.Container x, bool func = false)
            {
                if (x.containers.TryGetValue(key, out var value))
                {
                    return value;
                }
                throw new Errors.AnalyzerError("Undefined Reference", $"The {(func ? "function" : "class")} '{key}' does not exist in the current context");
            }

            // 'TryGet' Methods:

            public bool TryGetVariable(string key, out Symbol.Variable symbol, out bool isClassScoped, bool ignoreEnclosing=false)
            {
                if (Current.variables.TryGetValue(key, out var value))
                {
                    symbol = value;
                    isClassScoped = false;
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

            // 'TryGetFullScope' Methods:

            public bool TryGetContainerFullScope(string key, out Symbol.Container symbol, bool notFunc=false)
            {
                var x = Current;

                while (x != null)
                {
                    if (x.IsFunc())
                    {
                        x = x.enclosing;
                        continue;
                    }

                    if (x.containers.TryGetValue(key, out var value) && (notFunc? !value.IsFunc() : true))
                    {
                        symbol = value;
                        return true;
                    }
                    x = x.enclosing;
                }

                symbol = null;
                return false;
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

                    internal Expr.Definition self {
                        get
                        {
                            if (this.IsClass()) { return ((Class)this).self; }
                            else if (this.IsFunc()) { return ((Function)this).self; }
                            else if (this.IsPrimitive()) { return ((Primitive)this).self; }
                            else { throw new Errors.ImpossibleError("Type of symbol not recognized"); }
                        }
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

                    new internal Expr.Class self;

                    public Class(Expr.Class self) : base(SymbolType.Class)
                    {
                        this._variables = new();
                        this._containers = new();
                        this.self = self;
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

                    new internal Expr.Function self;

                    public Function(Expr.Function self) : base(SymbolType.Function)
                    {
                        this._variables = new();
                        this.self = self;
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

                    new internal Expr.Primitive self;

                    public Primitive(Expr.Primitive self) : base(SymbolType.Primitive)
                    {
                        this._containers = new();
                        this.self = self;
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
