using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Analyzer
{
    internal class SymbolTable
    {
        private Expr.Definition? current = null;
        public Expr.Definition? Current { get => current; private set => current = value; }

        public Expr.Function main = null;

        private List<Expr.Definition> globals = new();

        private List<(Token, Expr.StackData)> locals = new();
        Stack<int> framePointer = new();

        private static Expr.StackData VarNotFoundData = new(TypeCheckUtils.anyType, false, false, 0, 0);
        private static Expr.Class ClassNotFoundDefinition = TypeCheckUtils.anyType;
        public Expr.Function FunctionNotFoundDefinition = SpecialObjects.GenerateAnyFunction();

        public void SetContext(Expr.Definition? current)
        {
            this.current = current;
        }


        public void Add(Token name, Expr.StackData variable)
        {
            current.size += variable._ref ? 8 : variable.size;
            variable.stackOffset = current.size;
            
            if (current.definitionType == Expr.Definition.DefinitionType.Function)
            {
                locals.Add(new(name, variable));
            }
        }

        public void Add(Token name, Expr.StackData parameter, int i, int arity)
        {
            if (i < InstructionUtils.paramRegister.Length)
            {
                current.size += parameter._ref? 8 : parameter.size;
                parameter.stackOffset = current.size;
            }
            else
            {
                parameter.plus = true;
                parameter.stackOffset = (8 * ((arity - i))) + 8;
            }

            if (current.definitionType == Expr.Definition.DefinitionType.Function)
            {
                locals.Add(new(name, parameter));
            }
        }

        public void AddDefinition(Expr.Definition definition)
        {
            definition.enclosing = Current;
            SetContext(definition);
        }
        public void AddDefinition(Expr.DataType definition)
        {
            AddDefinition((Expr.Definition)definition);
            CheckDuplicates(definition.definitions);
        }
        public void AddDefinition(Expr.Class definition)
        {
            AddDefinition((Expr.DataType)definition);
            foreach (var duplicate in definition.declarations.GroupBy(x => x.name.lexeme).Where(x => x.Count() > 1).Select(x => x.ElementAt(0)))
            {
                Diagnostics.errors.Push(new Error.AnalyzerError("Double Declaration", $"A variable named '{duplicate.name.lexeme}' is already declared in this scope"));
            }
        }

        public void CreateBlock() => framePointer.Push(locals.Count);

        public void RemoveBlock() => locals.RemoveRange(framePointer.Peek(), locals.Count - framePointer.Pop());

        // 'GetVariable' Methods:

        private Expr.StackData? _GetVariable(Token key, out bool isClassScoped, bool ignoreEnclosing)
        {
            if (current == null)
            {
                isClassScoped = false;
                return null;
            }

            if (current.definitionType == Expr.Definition.DefinitionType.Function)
            {
                for (int i = locals.Count - 1; i >= 0; i--)
                {
                    if (key.lexeme == locals[i].Item1.lexeme)
                    {
                        isClassScoped = false;
                        return locals[i].Item2;
                    }
                }
            }

            if (!ignoreEnclosing && !(current.definitionType == Expr.Definition.DefinitionType.Function && (((Expr.Function)current).modifiers["static"])))
            {
                var x = NearestEnclosingClass();
                switch (x?.definitionType)
                {
                    case Expr.Definition.DefinitionType.Class:
                        {
                            if (TryGetValue(((Expr.Class)x).declarations, key, out var value))
                            {
                                isClassScoped = true;
                                return value.stack;
                            }
                            if (key.lexeme == "this")
                            {
                                isClassScoped = false;
                                return x._this;
                            }
                        }
                        break;
                    case Expr.Definition.DefinitionType.Primitive:
                        {
                            if (key.lexeme == "this")
                            {
                                isClassScoped = false;
                                return x._this;
                            }
                        }
                        break;
                    case null:
                        break;
                }
            }
            isClassScoped = false;
            return null;
        }
        public Expr.StackData? GetVariable(Token key, out bool isClassScoped, bool ignoreEnclosing=false)
        {
            var variable = _GetVariable(key, out isClassScoped, ignoreEnclosing);

            if (variable == null)
            {
                Diagnostics.errors.Push(new Error.AnalyzerError("Undefined Reference", $"The variable '{key.lexeme}' does not exist in the current context"));
                return VarNotFoundData;
            }
            return variable;
        }
        public Expr.StackData GetVariable(Token key)
        {
            return GetVariable(key, out _);
        }
        public bool TryGetVariable(Token key, out Expr.StackData symbol, out bool isClassScoped, bool ignoreEnclosing=false)
        {
            return (symbol = _GetVariable(key, out isClassScoped, ignoreEnclosing)) != null;
        }

        // 'GetDefinition' Methods:

        private Expr.Definition? _GetDefinition(Token key)
        {
            if (current == null)
            {
                if (TryGetValue(globals, key, out var globalValue))
                {
                    return globalValue;
                }
                return null;
            }

            if (current.definitionType == Expr.Definition.DefinitionType.Function)
            {
                Diagnostics.errors.Push(new Error.ImpossibleError("Requested function's definitions"));
            }

            if (TryGetValue(((Expr.DataType)current).definitions, key, out var value))
            {
                return value;
            }

            return null;
        }
        public Expr.Definition GetClass(Token key)
        {
            var _class = _GetDefinition(key);

            if (_class == null)
            {
                Diagnostics.errors.Push(new Error.AnalyzerError("Undefined Reference", $"The class '{key.lexeme}' does not exist in the current context"));
                return new SpecialObjects.Any(key);
            }
            else if (_class.definitionType == Expr.Definition.DefinitionType.Function)
            {
                Diagnostics.errors.Push(new Error.AnalyzerError("Undefined Reference", $"The function '{key.lexeme}' cannot be used as a type"));
                return ClassNotFoundDefinition;
            }
            return _class;
        }
        public bool TryGetDefinition(Token key, out Expr.Definition symbol)
        {
            return (symbol = _GetDefinition(key)) != null;
        }

        // 'GetDefinitionFullScope' methods

        public Expr.Definition? _GetDefinitionFullScope(Token key)
        {
            Expr.Definition? definition = null;

            Expr.Type? x = NearestEnclosingClass();

            while (x != null)
            {
                if (TryGetValue(((Expr.DataType)x).definitions, key, out var xValue))
                {
                    definition = xValue;
                }
                x = x.enclosing;
            }

            if (TryGetValue(globals, key, out var value))
            {
                definition = value;
            }
            
            return definition;
        }
        public Expr.Definition GetClassFullScope(Token key)
        {
            Expr.Definition? _class = _GetDefinitionFullScope(key);

            if (_class == null)
            {
                Diagnostics.errors.Push(new Error.AnalyzerError("Undefined Reference", $"The class '{key.lexeme}' does not exist in the current context"));
                return new SpecialObjects.Any(key);
            }
            else if (_class.definitionType == Expr.Definition.DefinitionType.Function)
            {
                Diagnostics.errors.Push(new Error.AnalyzerError("Undefined Reference", $"The function '{key.lexeme}' cannot be used as a type"));
                return ClassNotFoundDefinition;
            }
            return _class;
        }
        public bool TryGetDefinitionFullScope(Token key, out Expr.Definition symbol)
        {
            return (symbol = _GetDefinitionFullScope(key)) != null;
        }

        // 'GetFunction' Methods:

        private Expr.Function? _GetFunction(string key, Expr.Type[] types)
        {
            if (current == null || NearestEnclosingClass() == null)
            {
                if (TryGetFuncValue(globals, key, types, out var globalValue))
                {
                    return globalValue;
                }
                return null;
            }

            if (TryGetFuncValue(NearestEnclosingClass().definitions, key, types, out var value))
            {
                return value;
            }

            return null;
        }

        public Expr.Function GetFunction(string key, Expr.Type[] types)
        {
            Expr.Function? function = _GetFunction(key, types);

            if (function == null)
            {
                if (!types.Any(x => x == TypeCheckUtils.anyType))
                {
                    Diagnostics.errors.Push(new Error.AnalyzerError("Undefined Reference", $"The function '{key}({string.Join(", ", (object?[])types)})' does not exist in the current context"));
                }
                return FunctionNotFoundDefinition;
            }
            return function;
        }
        public bool TryGetFunction(string key, Expr.Type[] types, out Expr.Function symbol)
        {
            return (symbol = _GetFunction(key, types)) != null;
        }

        public Expr.DataType? NearestEnclosingClass(Expr.Definition definition)
        {
            // Assumes a function is enclosed by a class (no nested functions)
            return (definition?.definitionType == Expr.Definition.DefinitionType.Function) ? (Expr.DataType)definition.enclosing : (Expr.DataType)definition;
        }
        public Expr.DataType? NearestEnclosingClass()
        {
            return NearestEnclosingClass(current);
        }


        public void UpContext()
        {
            if (current == null)
                Diagnostics.errors.Push(new Error.ImpossibleError("Up Context Called On 'GLOBAL' context (no enclosing)"));

            current = (Expr.Definition)current.enclosing;
        }

        public bool CurrentIsTop() => current == null;

        public Expr.Definition? AddGlobal(Expr.Definition definition)
        {
            if (definition != null) 
            {
                globals.Add(definition);
            }
            return definition;
        }

        public void CheckGlobals() { CheckDuplicates(globals); }
        public void CheckDuplicates(List<Expr.Definition> definitions)
        {
            foreach (var duplicate in definitions.GroupBy(x => ToUniqueName(x)).Where(x => x.Count() > 1).Select(x => x.ElementAt(0)))
            {
                if (duplicate.definitionType == Expr.Definition.DefinitionType.Function)
                {
                    if (duplicate.name.lexeme == "Main")
                    {
                        Diagnostics.errors.Push(new Error.AnalyzerError("Double Declaration", "A Program may have only one 'Main' method"));
                    }

                    Diagnostics.errors.Push(new Error.AnalyzerError("Double Declaration", $"A function '{duplicate}' is already defined in this scope"));
                }
                else if (duplicate.definitionType == Expr.Definition.DefinitionType.Primitive)
                {
                    Diagnostics.errors.Push(new Error.AnalyzerError("Double Declaration", $"A primitive class named '{duplicate.name.lexeme}' is already defined in this scope"));
                }
                else
                {
                    Diagnostics.errors.Push(new Error.AnalyzerError("Double Declaration", $"A class named '{duplicate.name.lexeme}' is already defined in this scope"));
                }
            }
        }

        private string ToUniqueName(Expr.Definition definition)
        {
            if (definition.definitionType == Expr.Definition.DefinitionType.Function)
            {
                var function = (Expr.Function)definition;

                return (function.enclosing != null ?
                        function.enclosing.ToString() + "." :
                        "")
                        + function.name.lexeme + getParameters();

                string getParameters()
                {
                    string res = "";
                    if (function.parameters.Count != 0)
                    {
                        foreach (var type in function.parameters)
                        {
                            if (type.typeName.Count == 0)
                            {
                                res += (type.stack.type);
                            }
                            else
                            {
                                res += (string.Join(".", type.typeName.ToList().ConvertAll(x => x.lexeme)) + ", ");
                            }
                        }
                    }
                    return res;
                }
            }
            else
            {
                return definition.name.lexeme;
            }
        }

        private bool TryGetValue<T>(List<T> list, Token key, out T value)  where T : Expr.Named
        {
            foreach (var item in list)
            {
                if (item.name.lexeme == key.lexeme)
                {
                    value = item;
                    return true;
                }
            }
            value = null;
            return false;
        }
        private bool TryGetFuncValue(List<Expr.Definition> list, string key, Expr.Type[] types, out Expr.Function value)
        {
            value = null;
            foreach (var item in list)
            {
                if (item.name.lexeme == key)
                {
                    if (item.definitionType != Expr.Definition.DefinitionType.Function)
                    {
                        Diagnostics.errors.Push(new Error.AnalyzerError("Invalid Call", $"{item.definitionType} is not invokable"));
                        return false;
                    }
                    if (ParamMatch(types, (Expr.Function)item))
                    {
                        if (value == null)
                        { 
                            value = (Expr.Function)item;
                        }
                        else
                        {
                            Diagnostics.errors.Push(new Error.AnalyzerError("Ambiguous Call", $"Call is ambiguous between {value} and {(Expr.Function)item}"));
                            return false;
                        }
                    }
                }
            }

            return value != null;
        }

        private bool ParamMatch(Expr.Type[] a, Expr.Function b)
        {
            if (a.Length != b.Arity)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (!a[i].Matches(b.parameters[i].stack.type))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
