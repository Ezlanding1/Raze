using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze
{
    internal partial class Analyzer
    {
        internal class SymbolTable
        {

            public Symbol.Function head;
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
                    }
                    else if (value.IsClass())
                    {
                        newClass = ((Symbol.Class)value).newClass;
                    }
                }
            }
            private Symbol.Function currentFunction;
            public int count;
            private bool newClass;

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
                if (!newClass)
                    count++;

                Current.variables.Add(new Symbol.PrimitiveClass(v));
                currentFunction.self.size += v.size;
                v.stackOffset = currentFunction.self.size;
            }

            public void Add(Expr.Class c)
            {
                callStack.Add(c);
                var _ = new Symbol.Class(c);
                Current.containers.Add(_);
                _.enclosing = Current;
                Current = _;
            }

            public void Add(Expr.New n)
            {
                count++;
                callStack.Add(n.internalClass);
                var _ = new Symbol.Class(n.internalClass, true);
                Current.variables.Add(_);
                _.enclosing = Current;
                Current = _;
            }

            public void Add(Expr.Function f)
            {
                callStack.Add(f);
                if (f.constructor)
                {
                    f.size = currentFunction.self.size;
                }
                var _ = new Symbol.Function(f);
                Current.containers.Add(_);
                _.enclosing = Current;
                Current = _;
            }

            public void Add(Expr.Define d)
            {
                if (!newClass)
                    count++;
                var _ = new Symbol.Define(d);
                Current.variables.Add(_);
            }

            public bool DownContext(string to)
            {
                foreach (var container in Current.variables)
                {
                    if (container.IsClass() && ((Symbol.Class)container).self.dName == to)
                    {
                        Current = (Symbol.Class)container;
                        callStack.Add(((Symbol.Class)Current).self);
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

            public bool ContainsVariableKey(string key, out Symbol symbol)
            {
                var x = Current;
                while (x.enclosing != null)
                {
                    foreach (var var in x.variables)
                    {
                        if (var.Name.lexeme == key)
                        {
                            symbol = var;
                            return true;
                        }
                        if (var.IsClass())
                        {
                            if (((Symbol.Class)var).self.dName == key)
                            {
                                symbol = var;
                                return true;
                            }
                        }
                    }
                    x = x.enclosing;
                }
                symbol = null;
                return false;
            }

            public bool ContainsVariableKey(string key)
            {
                var x = Current;
                while (x != null)
                {
                    foreach (var var in x.variables)
                    {
                        if (var.Name.lexeme == key)
                        {
                            return true;
                        }
                    }
                    x = x.enclosing;
                }
                return false;
            }

            public void RemoveUnderCurrent(int startFrame)
            {
                if (startFrame - count == 0)
                {
                    return;
                }
                int x = count - startFrame;

                Current.variables.RemoveRange(startFrame, x);
                count -= x;
            }

            public void CurrentCalls() => currentFunction.self.keepStack = true;

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

                    internal Expr.Definition self;

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

                    public bool newClass;

                    public Class(Expr.Class self, bool newClass = false) : base(0)
                    {
                        this.self = self;
                        this.newClass = newClass;
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
