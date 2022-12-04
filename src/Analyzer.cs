using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
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
            if (literal is Expr.Var)
            {
                return ((Expr.Var)literal).type;
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

    internal class Stack
    {
        private stackObject.Container head;
        private stackObject.Container current;

        private List<string> history;
        public int stackOffset;
        public int count;

        public CallStack callStack;

        public Stack()
        {
            this.history = new();
            InitHead();
            current = head;
            this.stackOffset = 0;
            this.callStack = new();
        }

        private void InitHead()
        {
            count = 0;
            AddHistory("Container_C");
            head = new stackObject.Class("GLOBAL", "");
        }

        private void AddHistory(string action)
        {
            count++;
            history.Add(action);
        }

        private void RemoveLastHistory()
        {
            count--;
            history.RemoveAt(history.Count - 1);
        }

        public void RemoveLastParam()
        {
            count--;
            history.RemoveAt(history.Count - 1);
        }


        public bool DownContext(string to)
        {
            foreach (stackObject.Container container in current.containers)
            {
                if (container.type == "C" && ((stackObject.Container.Class)container).dName == to)
                {
                    current = container;
                    return true;
                }
            }
            return false;
        }

        public bool UpContext()
        {
            current = current.enclosing;
            return true;
        }

        public void Add(string type, string name, int size)
        {
            AddHistory("Var");

            stackOffset += size;
            stackObject.Var var = new stackObject.Var(type, name, stackOffset);
            current.vars.Add(var);
        }

        public void AddDefine(string name, Expr.Literal value)
        {
            AddHistory("Define");

            stackObject.Define define = new stackObject.Define(name, value);
            current.defines.Add(define);
        }

        public void AddFunc(string name, Dictionary<string, bool> modifiers)
        {
            AddHistory("Container_F");
            callStack.AddFunc(name, modifiers);

            stackObject.Function func = new stackObject.Function(name, modifiers);
            func.enclosing = current;
            current.containers.Add(func);
            current = func;
        }

        public void AddClass(string type, string name)
        {
            AddHistory("Container_C");
            callStack.AddClass(name);

            stackObject.Class _class = new stackObject.Class(type, name);
            _class.enclosing = current;
            current.containers.Add(_class);
            current = _class;
        }

        public void CurrentUp()
        {
            current = current.enclosing;
        }

        public void Modify(string type, string key, int value)
        {
            
        }

        public bool ContainsKey(string key)
        {
            foreach (stackObject.Var var in current.vars)
            {
                if (var.name == key)
                {
                    return true;
                }
            }
            return false;
        }
        public bool ContainsKey(string key, out int value, out string type, bool ctor=false)
        {
            foreach (stackObject.Var var in current.vars)
            {
                if (var.name == key)
                {
                    value = var.offset;
                    type = var.type;
                    return true;
                }
            }
            if (ctor)
            {
                foreach (stackObject.Var var in current.enclosing.vars)
                {
                    if (var.name == key)
                    {
                        value = var.offset;
                        type = var.type;
                        return true;
                    }
                }
            }
            value = 0;
            type = "";
            return false;
        }

        public bool ContainsDefine(string key, out Expr.Literal value)
        {
            return _ContainsDefine(current, key, out value);
        }
        private bool _ContainsDefine(stackObject.Container container, string key, out Expr.Literal value)
        {
            foreach (stackObject.Define def in container.defines)
            {
                if (def.name == key)
                {
                    value = def.value;
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

        public void RemoveLast()
        {
            if (history[history.Count - 1] == "Var")
            {
                current.vars.RemoveAt(current.vars.Count - 1);
            }
            else if (history[history.Count - 1] == "Define")
            {
                current.defines.RemoveAt(current.defines.Count - 1);
            }
            else if (history[history.Count - 1][..9] == "Container")
            {
                var enc = current.enclosing;
                enc.containers.RemoveAt(enc.containers.Count - 1);
                current = enc;
            }
            count--;
            history.RemoveAt(history.Count - 1);
        }

        public void RemoveUnderCurrent(int frameEnd)
        {
            if (frameEnd - count == 0)
            {
                return;
            }
            current.containers.Clear();
            current.vars.Clear();
            current.defines.Clear();
            history.RemoveRange(frameEnd, (history.Count) - frameEnd);
            count = frameEnd;
        }



        class stackObject
        {
            internal class Container : stackObject
            {
                internal Container enclosing;
                internal string type;
                internal string name;
                internal List<Var> vars;
                internal List<Define> defines;
                internal List<Container> containers;
                public Container(string type, string name)
                {
                    this.vars = new();
                    this.defines = new();
                    this.containers = new();
                    this.type = type;
                    this.name = name;
                    this.enclosing = null;
                }
            }

            internal class Function : Container
            {
                internal Dictionary<string, bool> modifiers;
                public Function(string name, Dictionary<string, bool> modifiers)
                    : base("F", name)
                {
                    this.modifiers = modifiers;
                }
            }

            internal class Class : Container
            {
                internal string dName;
                public Class(string type, string name)
                    : base("C", type)
                {
                    this.dName = name;
                }
            }

            internal class Var : stackObject
            {
                internal string type;
                internal string name;
                internal int offset;

                public Var(string type, string name, int offset)
                {
                    this.type = type;
                    this.name = name;
                    this.offset = offset;
                }
            }

            internal class Define : stackObject
            {
                internal string name;
                internal Expr.Literal value;

                public Define(string name, Expr.Literal value)
                {
                    this.name = name;
                    this.value = value;
                }
            }
        }
    }

    internal class CallStack
    {
        // True = class, False = fucntion
        List<Tuple<string, bool>> stack;
        public CallStack()
        {
            this.stack = new();
        }

        public void AddClass(string name)
        {
            stack.Add(new Tuple<string, bool>(name, true));
        }

        public void AddFunc(string name, Dictionary<string, bool> modifiers)
        {
            if (stack.Count == 0 && !modifiers["static"])
            {
                throw new Errors.BackendError(ErrorType.BackendException, "Top-Level Function", $"function {name} must have an enclosing class");
            }
            stack.Add(new Tuple<string, bool>(name, false));
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
