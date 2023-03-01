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
            public Dictionary<string, Expr.Primitive> primitives = new();
            public HashSet<Expr.Variable> classScopedVars = new();
            public int? globalClassVarOffset = null;
            public Dictionary<Expr.Class, SymbolTable.Symbol.Class> classToSymbol = new();
            public Expr.Function main = null;
            public Other()
            {
            }
        }

        internal class SymbolTable
        {
            private Symbol.Function head;
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
                this.head = new Symbol.Function(new Expr.Function());
                this.Current = this.head;
                //
                this.block = new(null);
            }

            public void Add(Expr.Variable v)
            {
                block.keys.Add(v.name.lexeme);

                var _ = new Symbol.Variable(v);

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
                other.classToSymbol[c]  = _;
            }

            public void Add(Expr.New n, Token name)
            {
                var _ = new Symbol.New(n, Current.self.size, name);
                Current.variables.Add(_.Name.lexeme, _);
                _.enclosing = Current;
                _.variables = other.classToSymbol[_.self].variables;
                _.containers = other.classToSymbol[_.self].containers;
                _.self.size = other.classToSymbol[_.self].self.size;
                Current.self.size += _.self.size;
            }

            public void Add(Expr.Function f)
            {
                var _ = new Symbol.Function(f);
                Current.containers.Add(_.Name.lexeme, _);
                _.enclosing = Current;
                Current = _;
            }

            public void Add(Expr.Define d)
            {
                var _ = new Symbol.Define(d);
                Current.variables.Add(_.Name.lexeme, _);
            }

            // 'Get' Methods:

            public Symbol GetVariable(string key)
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

            // 'TryGet' Methods:

            public bool TryGetVariable(string key, out Symbol symbol, out bool isClassScoped, bool classAccess = false)
            {
                if (!classAccess)
                {
                    if (Current.variables.TryGetValue(key, out var value))
                    {
                        symbol = value;
                        isClassScoped = Current.IsClass();
                        return true;
                    }
                }
                else
                {
                    var x = Current;

                    while (x != null)
                    {

                        if (x.variables.TryGetValue(key, out var value))
                        {
                            symbol = value;
                            isClassScoped = x.IsClass();
                            return true;
                        }

                        if (x.IsClass())
                        {
                            break;
                        }

                        x = x.enclosing;
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

            public bool TryGetContainerFullScope(string key, out Symbol.Container symbol)
            {
                var x = Current;

                while (x != null)
                {
                    if (x.containers.TryGetValue(key, out var value))
                    {
                        symbol = value;
                        return true;
                    }
                    x = x.enclosing;
                }

                symbol = null;
                return false;
            }


            public bool UpContext()
            {
                if (Current.enclosing == null)
                {
                    return false;
                }
                Current = Current.enclosing;
                return true;
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

                // 0 = Class, 1 = Func, 2 = Variable, 3 = Define
                private int type;

                public bool IsClass() => type == 0;
                public bool IsFunc() => type == 1;
                public bool IsVariable() => type == 2;
                public bool IsDefine() => type == 3;

                public Symbol(int type)
                {
                    this.type = type;
                }

                abstract internal class Container : Symbol
                {
                    internal Container enclosing;

                    internal Dictionary<string, Container> containers;
                    internal Dictionary<string, Symbol> variables;

                    internal Expr.Definition self {
                        get
                        {
                            if (this.IsClass()) { return ((Class)this).self; }
                            else if (this.IsFunc()) { return ((Function)this).self; }
                            else { throw new Errors.ImpossibleError("Type of symbol not recognized"); }
                        }
                    }

                    public Container(int type) : base(type)
                    {
                        this.containers = new();
                        this.variables = new();
                        this.enclosing = null;
                    }
                }

                internal class Class : Container
                {
                    public override Token Name { get { return self.name; } }

                    new internal Expr.Class self;

                    public Class(Expr.Class self) : base(0)
                    {
                        this.self = self;
                    }
                }

                internal class New : Class
                {
                    private Token name;
                    public override Token Name { get { return name; } }

                    public Expr.New newSelf;

                    public New(Expr.New self, int stackOffset, Token name) : base(self.internalClass)
                    {
                        this.newSelf = self;
                        this.newSelf.call.stackOffset = stackOffset;
                        this.name = name;
                    }
                }

                internal class Function : Container
                {
                    public override Token Name { get { return self.name; } }

                    new internal Expr.Function self;

                    public Function(Expr.Function self) : base(1)
                    {
                        this.self = self;
                    }
                }

                abstract internal class Var : Symbol
                {
                    public Var(int type) : base(type)
                    {

                    }
                }

                internal class Variable : Var
                {
                    public override Token Name { get { return self.name; } }

                    internal Expr.Variable self;

                    public Variable(Expr.Variable self) : base(2)
                    {
                        this.self = self;
                    }
                }

                internal class Define : Var
                {
                    public override Token Name { get { return self.name; } }

                    internal Expr.Define self;

                    public Define(Expr.Define self) : base(3)
                    {
                        this.self = self;
                    }
                }
            }
        }
    }
}
