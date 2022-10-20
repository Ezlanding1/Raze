using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Espionage
{
    internal class Analyzer : Expr.IVisitor<object?>
    {
        List<Expr> expressions;
        KeyValueStack stack;
        List<Expr.Call> callStack;
        int frameStart;
        bool mainDeclared;
        public Analyzer(List<Expr> expressions)
        {
            this.expressions = expressions;
            this.stack = new();
            this.callStack = new();
        }

        internal List<Expr> Analyze()
        {
            foreach (Expr expr in expressions)
            {
                expr.Accept(this);
            }
            if (callStack.Count > 0)
            {
                for (int i = 0, stackCount = callStack.Count; i < stackCount; i++)
                {
                    throw new Errors.BackendError(ErrorType.BackendException, "Undefined Reference", $"The function '{callStack[i].callee.literal.ToString()}' does not exist in the current context");
                }
            }
            if (!mainDeclared)
            {
                throw new Errors.BackendError(ErrorType.BackendException, "Entrypoint Not Found", "Program does not contain a Main method");
            }
            return expressions;
        }

        public object? visitBinaryExpr(Expr.Binary expr)
        {
            expr.left.Accept(this);
            expr.right.Accept(this);
            return null;
        }

        public object? visitBlockExpr(Expr.Block expr)
        {
            frameStart = stack.Count;
            foreach (Expr blockExpr in expr.block)
            {
                blockExpr.Accept(this);
            }
            // De-alloc variables
            for (int i = frameStart, frameEnd = stack.Count; i < frameEnd; i++)
            {
                stack.RemoveLast();
            }
            return null;
        }

        public object? visitCallExpr(Expr.Call expr)
        {
            // Important Note: Check if function exists and see if they have the same arity (which should be less than or equal to 6) and the parameter types match
            ResolveCall(expr);

            foreach (Expr argExpr in expr.arguments)
            {
                argExpr.Accept(this);
            }
            return null;
        }

        public object? visitClassExpr(Expr.Class expr)
        {
            return null;
        }

        public object? visitDeclareExpr(Expr.Declare expr)
        {
            // Function Todo Notice:
            // Note: since classes aren't implemented yet, functions are in a very early stage.
            // The flaws with storing functions on the stack, function defitions, function calls, sizeof, and typeof will be resolved in later commits.
            string type = expr.type.literal.ToString();
            string name = expr.name.literal.ToString();
            expr.value.Accept(this);
            int size = SizeOf(expr.type.literal.ToString());

            if (stack.ContainsKey(name))
            {
                throw new Errors.BackendError(ErrorType.BackendException, "Double Declaration", $"A variable named '{name}' is already defined in this scope");
            }
            stack.Add(type, name, size);
            expr.offset = stack.stackOffet;
            return null;
        }

        public object? visitFunctionExpr(Expr.Function expr)
        {
            // Function Todo Notice:
            // Note: since classes aren't implemented yet, functions are in a very early stage.
            // The flaws with storing functions on the stack, function defitions, function calls, sizeof, and typeof will be resolved in later commits.
            ResolveFunction(expr);
            if (!mainDeclared && expr.name.literal.ToString() == "Main")
            {
                mainDeclared = true;
            }
            int paramsCount = expr.parameters.Count;
            if (paramsCount > 6)
            {
                throw new Errors.BackendError(ErrorType.BackendException, "Too Many Parameters", "A function cannot have more than 6 parameters");
            }
            for (int i = 0; i < paramsCount; i++)
            {
                Expr.Parameter paramExpr = expr.parameters[i];
                stack.Add(paramExpr.variable.literal.ToString(), paramExpr.variable.literal.ToString(), InstructionTypes.paramRegister[i]);
            }
            expr.block.Accept(this);
            for (int i = 0; i < paramsCount; i++)
            {
                stack.RemoveLast();
            }
            return null;
        }

        public object? visitGetExpr(Expr.Get expr)
        {
            return null;
        }

        public object? visitGroupingExpr(Expr.Grouping expr)
        {
            expr.expression.Accept(this);
            return null;
        }

        public object? visitConditionalExpr(Expr.Conditional expr)
        {
            expr.condition.Accept(this);
            expr.block.Accept(this);
            return null;
        }

        public object? visitLiteralExpr(Expr.Literal expr)
        {
            return null;
        }

        public object? visitSetExpr(Expr.Set expr)
        {
            return null;
        }

        public object? visitSuperExpr(Expr.Super expr)
        {
            return null;
        }

        public object? visitThisExpr(Expr.This expr)
        {
            return null;
        }

        public object? visitUnaryExpr(Expr.Unary expr)
        {
            expr.operand.Accept(this);
            return null;
        }

        public object? visitVariableExpr(Expr.Variable expr)
        {
            string value;
            string name = expr.variable.literal.ToString();
            if (stack.ContainsKey(name, out value))
            {
                expr.stackPos = value;
                if (char.IsLetter(value[0]))
                {
                    expr.register = true;
                }
            }
            else
            {
                throw new Errors.BackendError(ErrorType.BackendException, "Undefined Reference", $"The variable '{name}' does not exist in the current context");
            }
            return null;
        }

        public object? visitReturnExpr(Expr.Return expr)
        {
            expr.value.Accept(this);
            return null;
        }

        public object? visitAssignExpr(Expr.Assign expr)
        {
            string name = expr.variable.literal.ToString();
            if (stack.ContainsKey(name))
            {
                if (!TypeMatch(stack.Gestring(name), expr.value))
                {
                    throw new Errors.BackendError(ErrorType.BackendException, "Type Mismatch", $"Cannot assign {name} to given type");
                }
            }
            return null;
        }

        internal static string TypeOf(Expr literal)
        {
            return "int";
        }

        internal static int SizeOf(string type)
        {
            if (type == "int")
            {
                return 8;
            }
            return 8;
        }

        private void ResolveFunction(Expr.Function expr)
        {
            Expr.Call call = callStack.Find(x => x.callee.literal.ToString() == expr.name.literal.ToString());
            string value = "";
            foreach (Expr.Parameter paramExpr in expr.parameters)
            {
                value += $"{paramExpr.type.literal.ToString()} {paramExpr.variable.literal.ToString()} ";
            }
            stack.Add("function", expr.name.literal.ToString(), value);

            if (call != null)
            {
                ValidCallCheck(expr.name.literal.ToString(), value, call.arguments);
                callStack.Remove(call);
            }
        }

        private void ResolveCall(Expr.Call expr)
        {
            string value;
            string name = expr.callee.literal.ToString();
            if (stack.ContainsKey(name, out value))
            {
                ValidCallCheck(name, value, expr.arguments);
            }
            else
            {
                callStack.Add(expr);
            }
        }

        private void ValidCallCheck(string name, string value, List<Expr> arguments)
        {
            // Arity Check
            string[] parameters = value.Split();
            int functionArity = ((parameters.Length - 1) / 2);
            if (functionArity != arguments.Count)
            {
                throw new Errors.BackendError(ErrorType.BackendException, "Arity Mismatch", $"Arity of call for {name} ({arguments.Count}) does not match the definition's arity ({functionArity})");
            }

            for (int i = 0, j = 0; i < functionArity; i++, j+=2)
            {
                if (parameters[i*2] != TypeOf(arguments[i]))
                {
                    throw new Errors.BackendError(ErrorType.BackendException, "Type Mismatch", $"In call for {name}, type of '{parameters[i * 2]}' does not match the definition's type '{TypeOf(arguments[i])}'");
                }
            }
        }

        private bool TypeMatch(string type, object input)
        {
            // Important Note: for now return true. In the future return the type of the object
            return true;
        }
    }

    internal class KeyValueStack
    {
        private Dictionary<string, string> dictStack;
        private List<Tuple<string,string>> listStack;
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
        public bool ContainsKey(string key)
        {
            return (dictStack.ContainsKey(key) && dictStack[key] != null);
        }
        public bool ContainsKey(string key, out string value)
        {
            return (dictStack.TryGetValue(key, out value) && value != null);
        }
        public string Gestring(string variable)
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
}
