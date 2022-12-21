using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Espionage
{
    internal partial class Analyzer
    {
        List<Expr> expressions;
        Expr.Assign.Function main;
        List<Expr.Define> globalDefines;

        public Analyzer(List<Expr> expressions)
        {
            this.expressions = expressions;
        }

        internal (List<Expr>, Expr.Function) Analyze(){
            Pass<object?> initialPass = new InitialPass(expressions);
            expressions = initialPass.Run();
            (main, globalDefines) = ((InitialPass)initialPass).GetOutput();

            if (main == null)
            {
                throw new Errors.BackendError(ErrorType.BackendException, "Main Not Found", "No Main method for entrypoint found");
            }
            CheckMain();
            Pass<object?> mainPass = new MainPass(expressions, main, globalDefines);
            expressions = mainPass.Run();

            Pass<string> TypeChackPass = new TypeCheckPass(expressions);
            expressions = TypeChackPass.Run();

            return (expressions, main);
        }

        private void CheckMain()
        {
            if (main._returnType != "void" && main._returnType != "number")
            {
                throw new Errors.BackendError(ErrorType.BackendException, "Main Invalid Return Type", $"Main can only return types 'number', and 'void'. Got '{main._returnType}'");
            }
            foreach (var item in main.modifiers)
            {
                if (item.Key != "static" && item.Value)
                {
                    throw new Errors.BackendError(ErrorType.BackendException, "Main Invalid Modifier", $"Main cannot have the '{item.Key}' modifier");
                }
            } 
        }

        internal static string TypeOf(Expr literal)
        {
            if (literal is Expr.Class)
            {
                return ((Expr.Class)literal).name.lexeme;
            }
            if (literal is Expr.Function)
            {
                return ((Expr.Function)literal).name.lexeme;
            }
            if (literal is Expr.Variable)
            {
                return ((Expr.Variable)literal).type.lexeme;
            }
            if (literal is Expr.Literal)
            {
                var l = (Expr.Literal)literal;
                if (l.literal.type == "NUMBERDOT")
                {
                    return "number";
                }

                if (l.literal.type == "STRING")
                {
                    return "string";
                }

                if (l.literal.type == "NUMBER")
                {
                    return "number";
                }
            }
            if (literal is Expr.Keyword)
            {
                var l = (Expr.Keyword)literal;
                if (l.keyword == "null" || l.keyword == "void")
                {
                    return l.keyword;
                }
                if (l.keyword == "true" || l.keyword == "false")
                {
                    return "bool";
                }
            }
            throw new Exception("Invalid TypeOf");
        }

        internal static int SizeOf(string type, Expr value=null)
        {
            if (value != null)
            {
                if (value is Expr.New)
                {
                    return 8;
                }
            }
            if (Primitives.PrimitiveSize.ContainsKey(type))
            {
                return Primitives.PrimitiveSize[type];
            }
            throw new Exception("Invalid sizeOf");
        }
    }

    internal class SymbolTable
    {
        
        public Symbol.Class head;
        private Symbol.Container Current;
        public Symbol.Container current { get { return this.Current; } set { this.Current = value; if (value.IsFunc()) { currentFunction = (Symbol.Function)value; } } }
        private Symbol.Function currentFunction;
        public int count;

        public CallStack callStack;

        public SymbolTable()
        {
            this.head = new Symbol.Class(null);
            this.current = this.head;
            this.count = 0;

            this.callStack = new();
        }

        public void Add(Expr.Variable v)
        {
            count++;
            current.vars.Add(new Symbol.PrimitiveClass(v));
            ((Expr.Function)currentFunction.self).size += v.size;
            v.stackOffset = ((Expr.Function)currentFunction.self).size;
        }

        public void Add(Expr.Class c)
        {
            callStack.Add(c);

            count++;
            var _ = new Symbol.Class(c);
            current.containers.Add(_);
            _.enclosing = current;
            current = _;
        }

        public void Add(Expr.Function f)
        {
            callStack.Add(f);
            if (f.constructor)
            {
                f.size = ((Expr.Function)currentFunction.self).size;
            }
            count++;
            var _ = new Symbol.Function(f);
            current.containers.Add(_);
            _.enclosing = current;
            current = _;
        }

        public void Add(Expr.Define d)
        {
            count++;
            var _ = new Symbol.Define(d);
            current.defines.Add(_);
        }

        public bool DownContext(string to)
        {
            foreach (var container in current.containers)
            {
                if (container.IsClass() && ((Expr.Class)container.self).dName == to)
                {
                    current = container;
                    return true;
                }
            }
            return false;
        }

        public bool UpContext()
        {
            if (current.enclosing == null)
            {
                return false;
            }
            current = current.enclosing;
            return true;
        }

        public bool ContainsKey(string key, out int value, out Token type, bool ctor = false)
        {
            foreach (var var in current.vars)
            {
                if (var.self.name.lexeme == key)
                {
                    value = var.self.stackOffset;
                    type = var.self.type;
                    return true;
                }
            }
            if (ctor)
            {
                foreach (var var in current.enclosing.vars)
                {
                    if (var.self.name.lexeme == key)
                    {
                        value = var.self.stackOffset;
                        type = var.self.type;
                        return true;
                    }
                }
            }
            value = 0;
            type = null;
            return false;
        }

        public bool ContainsKey(string key)
        {
            foreach (var var in current.vars)
            {
                if (var.self.name.lexeme == key)
                {
                    return true;
                }
            }
            return false;
        }

        public bool ContainsDefine(string key, out Expr.Literal value)
        {
            return _ContainsDefine(current, key, out value);
        }
        private bool _ContainsDefine(Symbol.Container container, string key, out Expr.Literal value)
        {
            foreach (var def in container.defines)
            {
                if (def.self.name == key)
                {
                    value = def.self.value;
                    return true;
                }
            }
            if (container.enclosing != null)
            {
                return _ContainsDefine(container.enclosing, key, out value);
            }
            value = null;
            return false;
        }

        public void RemoveUnderCurrent()
        {
            var x = (current.containers.Count + current.vars.Count + current.defines.Count);

            callStack.RemoveRange(current.containers.Count);

            current.containers.Clear();
            current.vars.Clear();
            current.defines.Clear();
            count -= x;
        }

        internal class Symbol
        {
            internal class Container : Symbol
            {
                internal Container enclosing;

                internal List<PrimitiveClass> vars;
                internal List<Define> defines;
                internal List<Container> containers;
                
                internal Expr.Definition self;

                private int type;

                public bool IsClass() => type == 0;
                public bool IsFunc() => type == 1;

                public Container(int type, Expr.Definition self)
                {
                    this.type = type;
                    this.self = self;
                    this.vars = new();
                    this.defines = new();
                    this.containers = new();
                    this.enclosing = null;
                }

                
            }

            internal class Class : Container
            {
                public Class(Expr.Class self) : base(0, self)
                {
                }
            }

            internal class Function : Container
            {
                public Function(Expr.Function self) : base(1, self)
                {
                }
            }


            internal class PrimitiveClass : Symbol 
            {
                internal Expr.Variable self;

                public PrimitiveClass(Expr.Variable self)
                {
                    this.self = self;
                }
            }

            internal class Define : Symbol
            {
                internal Expr.Define self;

                public Define(Expr.Define self)
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
            if (stack.Count == 0 && !f.modifiers["static"])
            {
                throw new Errors.BackendError(ErrorType.BackendException, "Top-Level Function", $"function {f.name.lexeme} must have an enclosing class");
            }
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
            foreach (var call in stack)
            {
                if (call.Item2)
                {
                    _classPath += call.Item1 + ".";
                }
                else
                {
                    _strings.Add((_classPath + call.Item1 + "();"));
                }
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
