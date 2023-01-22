using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Raze.Analyzer;

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
                    if (value.IsFunc())
                    {
                        currentFunction = (Symbol.Function)value;
                        newClass = null;
                    }
                    else if (value.IsClass())
                    {
                        newClass = value is Symbol.New;
                    }
                }
            }
            public Symbol.Function currentFunction;
            public int count;
            private bool? newClass;

            public static Other other = new();

            public CallStack callStack;

            public SymbolTable()
            {
                this.head = new Symbol.Function(new Expr.Function());
                this.Current = this.head;
                this.count = 0;

                this.callStack = new();
            }

            public void Add(Expr.Variable v)
            {
                var _ = new Symbol.PrimitiveClass(v);

                if (newClass == null)
                {
                    count++;
                    currentFunction.self.size += v.size;
                    v.stackOffset = currentFunction.self.size;
                }
                else if (!(bool)newClass)
                {
                    Current.self.size += v.size;
                    v.stackOffset = Current.self.size;
                }
                else
                {
                    currentFunction.self.size += v.size;
                }

                Current.variables.Add(_);
            }

            public void Add(Expr.Class c)
            {
                callStack.Add(c);
                var _ = new Symbol.Class(c);
                Current.containers.Add(_);
                _.enclosing = Current;
                Current = _;
                other.classToSymbol[c]  = _;
            }

            public void Add(Expr.New n, Token name)
            {
                //count++;
                callStack.Add(n.internalClass);
                var _ = new Symbol.New(n, currentFunction.self.size, name);
                Current.variables.Add(_);
                _.enclosing = Current;
                Current = _;
            }

            public void Add(Expr.Function f)
            {
                callStack.Add(f);
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
                    if ((container.IsClass()) && ((Symbol.Class)container).Name.lexeme == to)
                    {
                        Current = (Symbol.Class)container;
                        callStack.Add(((Symbol.Class)Current).self);
                        return true;
                    }
                }
                return false;
            }

            public bool DownContainerContext(string to)
            {
                foreach (var container in Current.containers)
                {
                    if (container.IsClass() && ((Symbol.Class)container).Name.lexeme == to)
                    {
                        Current = (Symbol.Class)container;
                        return true;
                    }
                }
                return false;
            }

            public bool UpContext()
            {
                if (Current.enclosing == null)
                {
                    return false;
                }
                Current = Current.enclosing;
                callStack.RemoveLast();
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

                    res = QueryVariable(key, head);
                    if (res.Any())
                    {
                        symbol = res.FirstOrDefault();
                        isClassScoped = head.IsClass();
                        return true;
                    }
                }
                else
                {
                    // clean up like the rest 
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
                if (Current is Symbol.New)
                {
                    x = other.classToSymbol[((Symbol.New)Current).self];
                }

                // ToDo : throw outofbounds constraint if out of bounds?
                var res = QueryContainer(key).Where(x => (constraint == 0)? x.IsFunc() : (constraint == 1)? x.IsClass() : false);
                symbol = res.FirstOrDefault();
                return res.Count() != 0;
            }

            public bool ContainsContainerKey(string key, out Symbol.Container symbol)
            {
                var res = QueryContainer(key);
                symbol = res.FirstOrDefault();
                return res.Count() != 0; 
            }

            private IEnumerable<Symbol.Container> QueryContainer(string key)
            {
                return QueryContainer(key, Current);
            }

            private IEnumerable<Symbol.Container> QueryContainer(string key, Symbol.Container x)
            {
                if (Current is Symbol.New)
                {
                    x = other.classToSymbol[((Symbol.New)Current).self];
                }
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

            abstract internal class Symbol
            {
                public abstract Token Name { get; }

                // 0 = Class, 1 = Func, 2 = Primitive, 3 = Define
                private int type;

                public bool IsClass() => type == 0;
                public bool IsFunc() => type == 1;
                public bool IsPrimitiveClass() => type == 2;
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
                            else { throw new Exception(); }
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

                abstract internal class Variable : Symbol
                {
                    public Variable(int type) : base(type)
                    {

                    }
                }

                internal class PrimitiveClass : Variable
                {
                    public override Token Name { get { return self.name; } }

                    internal Expr.Variable self;

                    public PrimitiveClass(Expr.Variable self) : base(2)
                    {
                        this.self = self;
                    }
                }

                internal class Define : Variable
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

        internal class CallStack
        {
            // True = class, False = fucntion
            private List<Tuple<string, bool>> stack;
            public CallStack()
            {
                this.stack = new();
            }

            public void Add(Expr.Class c)
            {
                stack.Add(new Tuple<string, bool>(c.name.lexeme, true));
            }

            public void Add(Expr.Function f)
            {
                stack.Add(new Tuple<string, bool>(f.name.lexeme, false));
            }

            public void RemoveRange(int x)
            {
                stack.RemoveRange(stack.Count - x, x);
            }

            public void RemoveLast()
            {
                stack.RemoveAt(stack.Count - 1);
            }

            public override string ToString()
            {
                string str = "at:\n";
                string _classPath = "";
                List<string> _strings = new();

                int count = 1;
                foreach (var call in stack)
                {
                    if (call.Item2)
                    {
                        if (count >= stack.Count)
                        {
                            _strings.Add(call.Item1 + "{}");
                        }
                        else
                        {
                            _classPath += call.Item1 + ".";
                        }
                    }
                    else
                    {
                        _strings.Add((_classPath + call.Item1 + "();"));
                    }
                    count++;
                }
                _strings.Reverse();
                foreach (var item in _strings)
                {
                    str += ("\t" + item + "\n");
                }
                return str;
            }

            public string Current()
            {
                string str = "";
                string _classPath = "";
                foreach (var call in stack)
                {
                    if (call.Item2)
                    {
                        _classPath += call.Item1 + ".";
                    }
                    else
                    {
                        str += (_classPath + call.Item1 + "();");
                    }
                }
                return str;
            }
        }
    }
}
