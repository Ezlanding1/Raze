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
        internal class SymbolTable
        {
            private Expr.Definition? current = null;
            public Expr.Definition? Current { get => current; private set => current = value; }

            public Expr.Function main = null;

            private List<Expr.Definition> globals = new();

            private List<(Token, Expr.StackData)> locals = new();
            Stack<int> framePointer = new();

            public void SetContext(Expr.Definition? current)
            {
                this.current = current;
            }


            public void Add(Token name, Expr.StackData variable, Expr.Definition definition)
            {
                current.size += variable.size;
                variable.stackOffset = current.size;
                variable.type = definition;
                
                if (current.definitionType == Expr.Definition.DefinitionType.Function)
                {
                    locals.Add(new(name, variable));
                }
            }

            public void Add(Token name, Expr.StackData parameter, Expr.Definition definition, int i, int arity)
            {
                if (i < InstructionInfo.paramRegister.Length)
                {
                    current.size += parameter.size;
                    parameter.stackOffset = current.size;
                }
                else
                {
                    parameter.plus = true;
                    parameter.stackOffset = (8 * ((arity - i))) + 8;
                }

                parameter.type = definition;

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
                foreach (var duplicate in definition.definitions.GroupBy(x => x).Where(x => x.Count() > 1).Select(x => x.Key))
                {
                    if (duplicate.definitionType == Expr.Definition.DefinitionType.Function)
                    {
                        if (duplicate.name.lexeme == "Main")
                        {
                            throw new Errors.AnalyzerError("Double Declaration", "A Program may have only one 'Main' method");
                        }

                        throw new Errors.AnalyzerError("Double Declaration", $"A function named '{duplicate.name.lexeme}' is already defined in this scope");
                    }
                    else
                    {
                        throw new Errors.AnalyzerError("Double Declaration", $"A class named '{duplicate.name.lexeme}' is already defined in this scope");
                    }
                }
                AddDefinition((Expr.Definition)definition);
            }
            public void AddDefinition(Expr.Class definition)
            {
                foreach (var duplicate in (definition).declarations.GroupBy(x => x).Where(x => x.Count() > 1).Select(x => x.Key))
                {
                    throw new Errors.AnalyzerError("Double Declaration", $"A variable named '{duplicate.name.lexeme}' is already declared in this scope");
                }
                AddDefinition((Expr.DataType)definition);
            }

            public void CreateBlock() => framePointer.Push(locals.Count);

            public void RemoveBlock() => locals.RemoveRange(framePointer.Peek(), locals.Count - framePointer.Pop());

            //public void Add(Expr.Define d)
            //{
            //    var _ = new Symbol.Define(d);
            //    current.variables.Add(_.Name.lexeme, _);
            //}

            // 'GetVariable' Methods:

            private Expr.StackData? _GetVariable(Token key, out bool isClassScoped, bool ignoreEnclosing)
            {
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

            public Expr.StackData GetVariable(Token key, out bool isClassScoped, bool ignoreEnclosing=false)
            {
                return _GetVariable(key, out isClassScoped, ignoreEnclosing) ?? throw new Errors.AnalyzerError("Undefined Reference", $"The variable '{key.lexeme}' does not exist in the current context");
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

            private Expr.Definition? _GetDefinition(Token key, bool func)
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
                    throw new Errors.ImpossibleError("Requested function's definitions");
                }

                if (TryGetValue(((Expr.DataType)current).definitions, key, out var value))
                {
                    return value;
                }

                return null;
            }

            public Expr.Definition GetDefinition(Token key, bool func = false)
            {
                return _GetDefinition(key, func) ?? throw new Errors.AnalyzerError("Undefined Reference", $"The {(func ? "function" : "class")} '{key.lexeme}' does not exist in the current context");
            }
            public bool TryGetDefinition(Token key, out Expr.Definition symbol)
            {
                return (symbol = _GetDefinition(key, false)) != null;
            }

            public Expr.DataType GetClassFullScope(Token key)
            {
                Expr.Definition? x = NearestEnclosingClass();

                while (x != null)
                {
                    if (x.name.lexeme == key.lexeme)
                    {
                        return (Expr.DataType)x;
                    }
                    x = (Expr.Definition)x.enclosing;
                }

                if (TryGetValue(globals, key, out var value))
                {
                    if (value.definitionType == Expr.Definition.DefinitionType.Function)
                    {
                        throw new Errors.AnalyzerError("Undefined Reference", $"The class '{key.lexeme}' does not exist in the current context");
                    }
                    return (Expr.DataType)value;
                }

                throw new Errors.AnalyzerError("Undefined Reference", $"The class '{key.lexeme}' does not exist in the current context");
            }



            public Expr.DataType? NearestEnclosingClass(Expr.Definition definition)
            {
                // Assumes a function is enclosed by a class (no nested functions)
                return (definition.definitionType == Expr.Definition.DefinitionType.Function) ? (Expr.DataType)definition.enclosing : (Expr.DataType)definition;
            }
            public Expr.DataType? NearestEnclosingClass()
            {
                return NearestEnclosingClass(current);
            }


            public void UpContext()
            {
                if (current == null)
                    throw new Errors.ImpossibleError("Up Context Called On 'GLOBAL' context (no enclosing)");

                current = (Expr.Definition)current.enclosing;
            }

            public bool CurrentIsTop() => current == null;

            public void AddGlobal(Expr.Definition definition)
            {
                if (TryGetValue(globals, definition.name, out var duplicate))
                {
                    if (duplicate.definitionType == Expr.Definition.DefinitionType.Function)
                    {
                        if (duplicate.name.lexeme == "Main")
                        {
                            throw new Errors.AnalyzerError("Double Declaration", "A Program may have only one 'Main' method");
                        }

                        throw new Errors.AnalyzerError("Double Declaration", $"A function named '{duplicate.name.lexeme}' is already defined in this scope");
                    }
                    else
                    {
                        throw new Errors.AnalyzerError("Double Declaration", $"A class named '{duplicate.name.lexeme}' is already defined in this scope");
                    }
                }
                globals.Add(definition);
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
        }
    }
}
