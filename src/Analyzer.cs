﻿using System;
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

        public Analyzer(List<Expr> expressions)
        {
            this.expressions = expressions;
        }

        internal List<Expr> Analyze(){
            Pass initialPass = new InitialPass(expressions);
            expressions = initialPass.Run();
            Expr.Function main = ((InitialPass)initialPass).getMain();

            if (main == null)
            {
                throw new Errors.BackendError(ErrorType.BackendException, "Main Not Found", "No Main method for entrypoint found");
            }
            Pass mainPass = new MainPass(expressions, main);
            expressions = mainPass.Run();

            return expressions;
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
            if (literal is Expr.Literal)
            {
                var l = (Expr.Literal)literal;
                if (Regex.IsMatch(l.literal.lexeme, TokenList.Tokens["NUMBERDOT"]))
                {
                    return "number";
                }

                if (Regex.IsMatch(l.literal.lexeme, TokenList.Tokens["STRING"]))
                {
                    return "string";
                }

                if (Regex.IsMatch(l.literal.lexeme, TokenList.Tokens["NUMBER"]))
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

    internal class KeyValueStack
    {
        private stackObject.Container head;
        private stackObject.Container current;
        private stackObject.Container tempCurrent;

        private List<string> history;
        public int stackOffset;
        public int count;
        public KeyValueStack()
        {
            this.history = new();
            InitHead();
            current = head;
            this.stackOffset = 0;
            this.tempCurrent = null;
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

            stackObject.Function func = new stackObject.Function(name, _static);
            func.enclosing = current;
            current.containers.Add(func);
            current = func;
        }

        public void AddClass(string type, string name)
        {
            AddHistory("Container_C");

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
            //dictStack[key].offset = stackOffset.ToString();
            //stackObject.Container pointer = current;
            //for (; pointer != null; pointer = pointer.enclosing)
            //{
            //    foreach (stackObject.Var var in pointer.vars)
            //    {
            //        if (var.name == key)
            //        {
            //            var.
            //        }
            //    }
            //}
            //var newVal = new Tuple<string, string>(key, type);
            //for (int i = 0; i < listStack.Count; i++)
            //{
            //    if (listStack[i].Item1 == key)
            //    {
            //        listStack[i] = newVal;
            //        break;
            //    }
            //}
        }

        public bool ContainsKey(string key)
        {
            stackObject.Container pointer = current;
            for (; pointer != null; pointer = pointer.enclosing)
            {
                foreach (stackObject.Var var in pointer.vars)
                {
                    if (var.name == key)
                    {
                        return true;
                    }
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
        List<Expr> stack;
        // True = class, False = fucntion
        List<Tuple<string, bool>> stackString;
        public CallStack()
        {
            this.stack = new();
            this.stackString = new();
        }

        public void Add(Expr.Class c)
        {
            stack.Add(c);
            stackString.Add(new Tuple<string, bool>(c.name.lexeme, true));
        }

        public void Add(Expr.Function f)
        {
            if (stack.Count == 0 && !(f._static))
            {
                throw new Errors.BackendError(ErrorType.BackendException, "Top-Level Function", $"function {f.name.lexeme} must have an enclosing class");
            }
            stack.Add(f);
            stackString.Add(new Tuple<string, bool>(f.name.lexeme, false));
        }

        public void RemoveLast()
        {
            stack.RemoveAt(stack.Count - 1);
            stackString.RemoveAt(stackString.Count - 1);
        }

        public override string ToString()
        {
            string str = "at:\n";
            string _classPath = "";
            List<string> _strings = new();
            foreach (var call in stackString)
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
            foreach (var call in stackString)
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
