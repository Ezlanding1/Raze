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
                set
                {
                    this.current = value;
                }
            }

            public int count;

            public Other other = new();

            public SymbolTable()
            {
                this.head = new Symbol.Function(new Expr.Function());
                this.Current = this.head;
                this.count = 0;
            }

            public void Add(Expr.Variable v)
            {
                var _ = new Symbol.Variable(v);

                Current.self.size += v.size;
                v.stackOffset = Current.self.size;

                Current.variables.Add(_);
            }

            public void Add(Expr.Class c)
            {
                var _ = new Symbol.Class(c);
                Current.containers.Add(_);
                _.enclosing = Current;
                Current = _;
                other.classToSymbol[c]  = _;
            }

            public void Add(Expr.New n, Token name)
            {
                count++;
                var _ = new Symbol.New(n, Current.self.size, name);
                Current.variables.Add(_);
                _.enclosing = Current;
                _.variables = other.classToSymbol[_.self].variables;
                _.containers = other.classToSymbol[_.self].containers;
                _.self.size = other.classToSymbol[_.self].self.size;
                Current.self.size += _.self.size;
            }

            public void Add(Expr.Function f)
            {
                var _ = new Symbol.Function(f);
                Current.containers.Add(_);
                _.enclosing = Current;
                Current = _;
            }

            public void Add(Expr.Define d)
            {
                var _ = new Symbol.Define(d);
                Current.variables.Add(_);
            }

            public bool DownContext(string to)
            {
                foreach (var container in Current.variables)
                {
                    if ((container.IsClass()) && container is Symbol.New && ((Symbol.Class)container).Name.lexeme == to)
                    {
                        Current = ((Symbol.New)container);
                        return true;
                    }
                }
                return false;
            }

            public bool DownContainerContext(string to)
            {
                foreach (var container in Current.containers)
                {
                    if ((container.IsClass() || container.IsFunc()) && ((Symbol.Container)container).Name.lexeme == to)
                    {
                        Current = (Symbol.Container)container;
                        return true;
                    }
                }
                return false;
            }

            public bool DownContainerContextFullScope(string to)
            {
                var x = Current;
                while (x != null)
                {
                    foreach (var container in x.containers)
                    {
                        if ((container.IsClass() || container.IsFunc()) && ((Symbol.Container)container).Name.lexeme == to)
                        {
                            Current = (Symbol.Container)container;
                            return true;
                        }
                    }
                    x = x.enclosing;
                }
                return false;
            }

            public bool DownNewContext(string to, out Symbol.New n)
            {
                foreach (var container in Current.variables)
                {
                    if ((container is Symbol.New) && ((Symbol.New)container).Name.lexeme == to)
                    {
                        Current = other.classToSymbol[((Symbol.New)container).self];
                        n = (Symbol.New)container;
                        return true;
                    }
                }
                n = null;
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

            public bool ContainsVariableKey(string key, bool classAccess, out Symbol symbol, out bool isClassScoped)
            {
                if (!classAccess)
                {
                    var res = QueryVariable(key);
                    if (res.Any())
                    {
                        symbol = res.FirstOrDefault();
                        isClassScoped = Current.IsClass();
                        return true;
                    }
                }
                else
                {
                    var x = Current;
                    while (x != null)
                    {
                        var res = QueryVariable(key, x);
                        if (res.Any())
                        {
                            symbol = res.FirstOrDefault();
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
                var topRes = QueryVariable(key, head);
                if (topRes.Any())
                {
                    symbol = topRes.FirstOrDefault();
                    isClassScoped = head.IsClass();
                    return true;
                }
                symbol = null;
                isClassScoped = false;
                return false;
            }

            public bool ContainsVariableKey(string key, out Symbol symbol, out bool isClassScoped)
            {
                return ContainsVariableKey(key, false, out symbol, out isClassScoped);
            }

            public bool ContainsVariableKey(string key, out Symbol symbol)
            {
                return ContainsVariableKey(key, out symbol, out _);
            }

            public bool ContainsVariableKey(string key)
            {
                return ContainsVariableKey(key, out _, out _);
            }

            private IEnumerable<Symbol> QueryVariable(string key)
            {
                return QueryVariable(key, Current);
            }
            private IEnumerable<Symbol> QueryVariable(string key, Symbol.Container x)
            {
                return x.variables.Where(x => x.Name.lexeme == key);
            }

            public bool ContainsContainerKey(string key, out Symbol.Container symbol, int constraint)
            {
                var x = Current;

                symbol = null;
                while (symbol == null && x != null)
                {
                    var res = QueryContainer(key, x).Where(x => (constraint == 0) ? x.IsFunc() : (constraint == 1) ? x.IsClass() : false);
                    symbol = res.FirstOrDefault();
                    x = x.enclosing;
                }
                return symbol != null;
            }

            public bool ContainsContainerKey(string key, out Symbol.Container symbol)
            {
                symbol = null;
                var x = Current;
                while (symbol == null && x != null)
                {
                    var res = QueryContainer(key, x);
                    symbol = res.FirstOrDefault();
                    x = x.enclosing;
                }
                return symbol != null;
            }

            public bool ContainsLocalContainerKey(string key)
            {
                var res = QueryContainer(key);
                return res.Count() != 0;
            }

            private IEnumerable<Symbol.Container> QueryContainer(string key)
            {
                return QueryContainer(key, Current);
            }

            private IEnumerable<Symbol.Container> QueryContainer(string key, Symbol.Container x)
            {
                return x.containers.Where(x => x.Name.lexeme == key);
            }

            public bool ContainsContainerKey(string key)
            {
                return ContainsContainerKey(key, out _);
            }

            public void RemoveUnderCurrent(int startFrame)
            {
                if (startFrame - count == 0)
                {
                    return;
                }
                int x = count - startFrame;

                Current.variables.RemoveRange(Current.variables.Count-x, x);
                count -= x;
            }

            public void TopContext() => Current = head;

            public bool CurrentIsTop() => Current == head;

            public void SetContext(Symbol.Container container) => Current = container;

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

                    internal List<Container> containers;
                    internal List<Symbol> variables;

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
