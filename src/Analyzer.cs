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
        private Dictionary<string, string> dictStack;
        private List<Tuple<string, string>> listStack;
        public int stackOffet;
        public int Count { get { return listStack.Count; } }
        public KeyValueStack()
        {
            this.dictStack = new();
            this.listStack = new();
            this.stackOffet = 0;
        }

        public void Add(string type, string key, int? value)
        {
            stackOffet += (int)value;
            dictStack[key] = stackOffet.ToString();
            listStack.Add(new Tuple<string, string>(key, type));
        }


        public void Add(string type, string key, string value)
        {
            dictStack[key] = value;
            listStack.Add(new Tuple<string, string>(key, type));
        }

        public void Modify(string type, string key, int? value)
        {
            dictStack[key] = stackOffet.ToString();
            listStack.Remove(listStack.Find(x => x.Item1 == key));
            listStack.Add(new Tuple<string, string>(key, type));
        }

        public bool ContainsKey(string key)
        {
            return (dictStack.ContainsKey(key) && dictStack[key] != null);
        }
        public bool ContainsKey(string key, out string value)
        {
            return (dictStack.TryGetValue(key, out value) && value != null);
        }
        public string GetType(string variable)
        {
            return listStack.Find(x => x.Item1.Equals(variable)).Item2;
        }
        public string this[string index]
        {
            get { return dictStack[index]; }
            set { dictStack[index] = value; }
        }
        public Tuple<string, string> this[int index]
        {
            get { return listStack[index]; }
        }
        public void RemoveLast()
        {
            dictStack[listStack[listStack.Count - 1].Item1] = null;
            listStack.RemoveAt(listStack.Count - 1);
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
