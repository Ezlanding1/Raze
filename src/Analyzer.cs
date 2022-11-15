using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Espionage
{
    internal partial class Analyzer
    {
        List<Expr> expressions;
        Expr.Assign.Function main;

        public Analyzer(List<Expr> expressions)
        {
            this.expressions = expressions;
        }

        internal (List<Expr>, Expr.Function) Analyze(){
            Pass<object?> initialPass = new InitialPass(expressions);
            expressions = initialPass.Run();
            main = ((InitialPass)initialPass).getMain();

            if (main == null)
            {
                throw new Errors.BackendError(ErrorType.BackendException, "Main Not Found", "No Main method for entrypoint found");
            }
            CheckMain();
            Pass<object?> mainPass = new MainPass(expressions, main);
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
            throw new Exception("Invalid TypeOf");
        }

        internal static int SizeOf(string type)
        {
            if (Primitives.PrimitiveSize.ContainsKey(type))
            {
                return Primitives.PrimitiveSize[type];
            }
            return 8;
            throw new Exception("Invalid sizeOf");

        }
    }

    internal class Stack
    {
        private stackObject.Container head;
        private stackObject.Container current;
        private stackObject.Container tempCurrent;

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
            this.tempCurrent = null;
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

        public bool SwitchContext(string type, string to="")
        {
            switch (type)
            {
                case "BACK":
                    return BackContext();
                case "DOWN":
                    AddHistory(to);
                    return DownContext(to, current);
                case "UP":
                    AddHistory(to);
                    return UpContext(to);
                default:
                    return false;
            }
        }

        private bool BackContext()
        {
            RemoveLastHistory();
            if (tempCurrent == null)
            {
                return false;
            }
            current = tempCurrent;
            return true;
        }

        private bool DownContext(string to, stackObject.Container currentContainer)
        {
            foreach (stackObject.Container container in currentContainer.containers)
            {
                if (container.type == "C" && ((stackObject.Container.Class)container).dName == to)
                {
                    tempCurrent = current;
                    current = container;
                    return true;
                }
                if (DownContext(to, container))
                {
                    return true;
                }
            }
            return false;
        }

        private bool UpContext(string to)
        {
            throw new NotImplementedException();
        }

        public void Add(string type, string key, int? value)
        {
            AddHistory("Var");

            stackOffset += (int)value;
            stackObject.Var var = new stackObject.Var(type, key, stackOffset.ToString());
            current.vars.Add(var);
        }

        public void AddFunc(string name, bool _static)
        {
            AddHistory("Container_F");
            callStack.AddFunc(name, _static);

            stackObject.Function func = new stackObject.Function(name, _static);
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

        public void Add(string type, string key, string value)
        {
            AddHistory("Var");
            stackObject.Var var = new stackObject.Var(type, key, value);
            current.vars.Add(var);
        }

        public void AddPrim(string type, string key, string value, int offset)
        {
            AddHistory("Var");
            stackOffset += offset;
            stackObject.Var var = new stackObject.Var(type, key, value);
            current.vars.Add(var);
        }

        public void Modify(string type, string key, int? value)
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
        public bool ContainsKey(string key, out string value, out string type)
        {
            stackObject.Container pointer = current;
            for (; pointer != null; pointer = pointer.enclosing)
            {
                foreach (stackObject.Var var in pointer.vars)
                {
                    if (var.name == key)
                    {
                        value = var.offset;
                        type = var.type;
                        return true;
                    }
                }
            }
            value = "";
            type = "";
            return false;
        }

        public void RemoveLast()
        {
            if (history[history.Count - 1] == "Var")
            {
                RemoveLastVar();
                count--;
                history.RemoveAt(history.Count - 1);
            }
            else if (history[history.Count - 1][..9] == "Container")
            {
                RemoveLastContainer();
                count--;
                history.RemoveAt(history.Count - 1);
            }
        }
        public void RemoveLastContainer()
        {
            var enc = current.enclosing;
            enc.containers.RemoveAt(enc.containers.Count - 1);
            current = enc;
        }
        public void RemoveLastVar()
        {
            current.vars.RemoveAt(current.vars.Count - 1);
        }

        public void RemoveUnderCurrent(int frameEnd)
        {
            if (frameEnd - count == 0)
            {
                return;
            }
            current.containers.Clear();
            current.vars.Clear();
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
                internal List<Container> containers;
                public Container(string type, string name)
                {
                    this.vars = new();
                    this.containers = new();
                    this.type = type;
                    this.name = name;
                    this.enclosing = null;
                }
            }

            internal class Function : Container
            {
                internal bool _static;
                public Function(string name, bool _static)
                    : base("F", name)
                {
                    this._static = _static;
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
                internal string offset;
                public Var(string type, string name, string offset)
                {
                    this.type = type;
                    this.name = name;
                    this.offset = offset;
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

        public void AddFunc(string name, bool _static)
        {
            if (stack.Count == 0 && !_static)
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
