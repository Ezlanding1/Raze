using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Raze.Analyzer;

namespace Raze
{
    internal partial class Analyzer
    {
        internal class MainPass : Pass<object?>
        {
            SymbolTable symbolTable = SymbolTableSingleton.SymbolTable;
            HashSet<Expr.Class> handledClasses;
            bool classAccess;

            public MainPass(List<Expr> expressions) : base(expressions)
            {
                this.handledClasses = new();
            }

            internal override List<Expr> Run()
            {
                foreach (var expr in expressions)
                {
                    expr.Accept(this);
                }
                return expressions;
            }

            public override object? visitBlockExpr(Expr.Block expr)
            {
                symbolTable.CreateBlock();
                
                foreach (Expr blockExpr in expr.block)
                {
                    blockExpr.Accept(this);
                }
                return null;
            }

            public override object? visitCallExpr(Expr.Call expr)
            {
                for (int i = 0; i < expr.arguments.Count; i++)
                {
                    expr.arguments[i].Accept(this);
                }

                CurrentCalls();

                var context = symbolTable.Current;

                var x = expr.callee;

                bool instanceCall;

                if (symbolTable.TryGetContainerFullScope(x.name.lexeme, out SymbolTable.Symbol.Container topSymbol))
                {
                    symbolTable.SetContext(topSymbol);
                    instanceCall = false;

                    if (x is Expr.Get)
                    {
                        x = ((Expr.Get)x).get;
                    }
                    while (x is Expr.Get)
                    {
                        symbolTable.SetContext(symbolTable.GetContainer(x.name.lexeme));
                        x = ((Expr.Get)x).get;
                    }

                    if (!topSymbol.IsFunc())
                    {
                        symbolTable.SetContext(symbolTable.GetContainer(x.name.lexeme, true));
                    }
                }
                else if (symbolTable.TryGetVariable(x.name.lexeme, out SymbolTable.Symbol topSymbol_I, out _, classAccess))
                {
                    if (topSymbol_I.IsClass())
                    {
                        symbolTable.SetContext((SymbolTable.Symbol.New)topSymbol_I);
                        expr.stackOffset = ((SymbolTable.Symbol.New)topSymbol_I).newSelf.call.stackOffset;
                        instanceCall = true;

                        if (x is Expr.Get)
                        {
                            x = ((Expr.Get)x).get;
                        }
                        while (x is Expr.Get)
                        {
                            var symbol = symbolTable.GetVariable(x.name.lexeme);

                            if (!symbol.IsClass())
                            {
                                //
                                throw new Exception();
                            }

                            symbolTable.SetContext((SymbolTable.Symbol.New)symbol);
                            x = ((Expr.Get)x).get;
                        }
                        symbolTable.SetContext(symbolTable.GetContainer(x.name.lexeme, true));
                    }
                    else
                    {
                        // This Error is hit when calling a variable like an instance of a type. For Example:
                        // string i = ""; i.FUNC();
                        throw new Exception();
                    }
                }
                else
                {
                    throw new Errors.AnalyzerError("Undefined Reference", $"The { ( (x is Expr.Get)? "class" : "function" ) } '{x.name.lexeme}' does not exist in the current context");
                }

                if (!symbolTable.Current.IsFunc())
                {
                    //
                    throw new Exception();
                }

                var s = ((SymbolTable.Symbol.Function)symbolTable.Current).self;

                if (instanceCall && s.modifiers["static"])
                {
                    throw new Errors.AnalyzerError("Static Mathod Called From Instance", "You cannot call a static method from an instance");
                }
                if (!instanceCall && !s.modifiers["static"])
                {
                    throw new Errors.AnalyzerError("Mathod Called From Static Context", "You cannot call an instance method from a static context");
                }

                if (s.constructor)
                {
                    throw new Errors.AnalyzerError("Constructor Called As Method", "A Constructor may not be called as a method of its class");
                }
                expr.internalFunction = s;

                symbolTable.SetContext(context);

                return null;
            }

            public override object? visitClassExpr(Expr.Class expr)
            {
                if (handledClasses.Contains(expr))
                {
                    return null;
                }

                handledClasses.Add(expr);

                symbolTable.SetContext(symbolTable.GetContainer(expr.name.lexeme));

                expr.topLevelBlock.Accept(this);
                expr.block.Accept(this);


                if (!symbolTable.UpContext())
                {
                    throw new Errors.ImpossibleError("Up Context Called On 'GLOBAL' context (no enclosing)");
                }
                return null;
            }

            public override object? visitDeclareExpr(Expr.Declare expr)
            {
                var name = expr.name;

                if (symbolTable.TryGetVariable(name.lexeme, out _, out _))
                {
                    throw new Errors.AnalyzerError("Double Declaration", $"A variable named '{name.lexeme}' is already defined in this scope");
                }

                if (expr.value is Expr.New)
                {
                    base.visitDeclareExpr(expr);
                    symbolTable.Add(((Expr.New)expr.value), name);
                }
                else
                {
                    SetSize(expr.type.lexeme, ref expr.size);
                    base.visitDeclareExpr(expr);
                    symbolTable.Add(expr);
                }
                
                return null;
            }

            public override object? visitPrimitiveExpr(Expr.Primitive expr)
            {
                return null;
            }

            public override object? visitFunctionExpr(Expr.Function expr)
            {
                symbolTable.SetContext(symbolTable.GetContainer(expr.name.lexeme, true));

                classAccess = !expr.modifiers["static"];
                int arity = expr.arity;
                for (int i = 0; i < arity; i++)
                {
                    Expr.Parameter paramExpr = expr.parameters[i];
                    SetSize(paramExpr.type.lexeme, ref paramExpr.size);
                    symbolTable.Add(paramExpr);
                }

                expr.block.Accept(this);


                symbolTable.RemoveUnderCurrent();
                symbolTable.UpContext();
                return null;
            }

            public override object? visitVariableExpr(Expr.Variable expr)
            {
                if (symbolTable.TryGetVariable(expr.name.lexeme, out SymbolTable.Symbol symbol, out bool isClassScoped, classAccess))
                {
                    if (symbol.IsVariable())
                    {
                        var s = ((SymbolTable.Symbol.Variable)symbol).self;

                        expr.size = s.size;
                        expr.stackOffset = s.stackOffset;
                        expr.type = s.type;

                        if (isClassScoped || symbolTable.other.classScopedVars.Contains(s))
                        {
                            symbolTable.other.classScopedVars.Add(expr);
                        }

                    }
                    else if (symbol.IsDefine())
                    {
                        var s = ((SymbolTable.Symbol.Define)symbol).self;
                        expr.define.Item1 = true;
                        expr.define.Item2 = s.value;
                    }
                    else if (symbol.IsClass())
                    {
                        throw new Exception("'Classes as variables' is not implemented in this version of the Raze Compiler");
                    }
                    else
                    {
                        throw new Errors.ImpossibleError($"Type of symbol '{symbol.Name}' not recognized");
                    }
                }
                else
                {
                    throw new Errors.AnalyzerError("Undefined Reference", $"The variable '{expr.name.lexeme}' does not exist in the current context");
                }
                return null;
            }

            public override object? visitDefineExpr(Expr.Define expr)
            {
                symbolTable.Add(expr);
                return null;
            }

            public override object? visitAssignExpr(Expr.Assign expr)
            {
                expr.variable.Accept(this);

                base.visitAssignExpr(expr);
                return null;
            }

            public override object visitAssemblyExpr(Expr.Assembly expr)
            {
                foreach (var variable in expr.variables.Keys)
                {
                    variable.Accept(this);
                }
                return null;
            }

            public override object? visitNewExpr(Expr.New expr)
            {
                CurrentCalls();

                var context = symbolTable.Current;

                DownGet(expr.call.callee);
                
                if (!symbolTable.Current.IsClass())
                {
                    throw new Exception();
                }

                expr.internalClass = ((SymbolTable.Symbol.Class)symbolTable.Current).self;

                expr.call.internalFunction = expr.internalClass.constructor;

                symbolTable.SetContext(symbolTable.other.classToSymbol[expr.internalClass].enclosing);
                expr.internalClass.Accept(this);
                symbolTable.SetContext(context);

                return null;
            }

            public override object? visitGetExpr(Expr.Get expr)
            {
                var instanceContainer = symbolTable.GetVariable(expr.name.lexeme);

                //
                if (!instanceContainer.IsClass())
                {
                    throw new Exception();
                }

                symbolTable.SetContext((SymbolTable.Symbol.New)instanceContainer);

                classAccess = true;
                expr.get.Accept(this);
                expr.type = expr.get.type;
                expr.stackOffset = expr.get.stackOffset;
                expr.size = expr.get.size;
                symbolTable.UpContext();
                return null;
            }

            public override object visitIsExpr(Expr.Is expr)
            {
                if (symbolTable.TryGetVariable(((Expr.Variable)expr.left).ToString(), out SymbolTable.Symbol symbol, out _))
                {
                    if (symbol.IsVariable())
                    {
                        expr.value = ((SymbolTable.Symbol.Variable)symbol).self.type.lexeme == expr.right.ToString()? "1" : "0";

                    }
                    else if (symbol.IsDefine())
                    {
                    }
                    else if (symbol.IsClass())
                    {
                        var s = ((SymbolTable.Symbol.Class)symbol).self;
                        expr.value = (s.QualifiedName == expr.right.ToString())? "1" : "0";
                    }
                    else
                    {
                        throw new Errors.ImpossibleError($"Type of symbol '{symbol.Name}' not recognized");
                    }
                }
                else
                {
                    throw new Errors.AnalyzerError("Undefined Reference", $"The variable '{((Expr.Variable)expr.left).ToString()}' does not exist in the current context");
                }
                return null;
            }

            private void CurrentCalls()
            {
                if (symbolTable.Current.IsFunc())
                {
                    ((SymbolTable.Symbol.Function)symbolTable.Current).self.leaf = false;
                }
            }

            private void DownGet(Expr.Variable get, bool first=true)
            {
                DownContainer(get, first);
                if (get is Expr.Get)
                {
                    DownGet(((Expr.Get)get).get, false);
                }
                else
                {
                    return;
                }
            }


            private void DownContainer(Expr.Variable get, bool first)
            {
                switch (first)
                {
                    case true:
                        {
                            if (symbolTable.TryGetContainerFullScope(get.name.lexeme, out var symbol))
                            {
                                symbolTable.SetContext(symbol);
                            }
                            else
                            {
                                throw new Errors.AnalyzerError("Undefined Reference", $"The class '{get.name.lexeme}' does not exist in the current context");
                            }
                        }
                        break;
                    case false:
                        symbolTable.SetContext(symbolTable.GetContainer(get.name.lexeme));
                        break;
                }
            }

            private void SetSize(string type, ref int size)
            {
                if (symbolTable.other.primitives.ContainsKey(type))
                {
                    size = symbolTable.other.primitives[type].size;
                }
                else
                {
                    throw new Errors.AnalyzerError("Undefined Reference", $"The primitive class '{type}' does not exist in the current context");
                }
            }
        }
    }
}
