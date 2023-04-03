using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze
{
    internal partial class Analyzer
    {
        internal class MainPass : Pass<object?>
        {
            SymbolTable symbolTable = SymbolTableSingleton.SymbolTable;
            HashSet<Expr.Definition> handledClasses;

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

                if (!expr._classBlock)
                {
                    symbolTable.RemoveUnderCurrent();
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

                bool instanceCall = false;
                bool first = true;

                int nOff = 0;
                
                while (x.get != null)
                {
                    if (first)
                    {
                        if (symbolTable.TryGetContainerFullScope(x.name.lexeme, out SymbolTable.Symbol.Container topSymbol))
                        {
                            instanceCall = false;
                            symbolTable.SetContext(topSymbol);
                        }
                        else if (symbolTable.TryGetVariable(x.name.lexeme, out SymbolTable.Symbol.Variable topSymbol_I, out _))
                        {
                            instanceCall = true;
                            nOff = topSymbol_I.self.stackOffset;
                            symbolTable.SetContext(topSymbol_I.definition);
                        }
                        else
                        {
                            throw new Errors.AnalyzerError("Undefined Reference", $"The class '{x.name.lexeme}' does not exist in the current context");
                        }
                        first = false;
                    }
                    else
                    {
                        if (!instanceCall)
                        {
                            symbolTable.SetContext(symbolTable.GetContainer(x.name.lexeme));
                        }
                        else
                        {
                            var symbol_I = symbolTable.GetVariable(x.name.lexeme);

                            if (!symbol_I.IsClass())
                            {
                                //
                                throw new Exception();
                            }
                            nOff = symbol_I.self.stackOffset;
                            symbolTable.SetContext(symbol_I.definition);
                        }
                    }

                    x = x.get;
                }

                if (first)
                {
                    if (symbolTable.TryGetContainerFullScope(x.name.lexeme, out SymbolTable.Symbol.Container symbol))
                    {
                        symbolTable.SetContext(symbol);
                    }
                    else
                    {
                        throw new Errors.AnalyzerError("Undefined Reference", $"The function '{x.name.lexeme}' does not exist in the current context");
                    }
                }
                else
                {
                    symbolTable.SetContext(symbolTable.GetContainer(x.name.lexeme, true));
                }

                if (expr.constructor)
                {
                    if (symbolTable.TryGetContainerFullScope(x.name.lexeme, out SymbolTable.Symbol.Container symbol))
                    {
                        symbolTable.SetContext(symbol);
                    }
                }

                // 
                if (!symbolTable.Current.IsFunc())
                {
                    throw new Exception();
                }


                if (instanceCall)
                {
                    expr.stackOffset = nOff;
                }

                var s = ((SymbolTable.Symbol.Function)symbolTable.Current).self;

                if (!expr.constructor && s.constructor)
                {
                    throw new Errors.AnalyzerError("Constructor Called As Method", "A Constructor may not be called as a method of its class");
                }
                else if (expr.constructor && !s.constructor)
                {
                    throw new Errors.AnalyzerError("Method Called As Constructor", "A Method may not be called as a constructor of its class");
                }


                if (instanceCall && s.modifiers["static"])
                {
                    throw new Errors.AnalyzerError("Static Method Called From Instance", "You cannot call a static method from an instance");
                }
                if (!instanceCall && !s.modifiers["static"] && !expr.constructor)
                {
                    throw new Errors.AnalyzerError("Instance Method Called From Static Context", "You cannot call an instance method from a static context");
                }

                if (expr.arguments.Count != s.arity)
                {
                    throw new Errors.BackendError("Arity Mismatch", $"Arity of call for {s.QualifiedName} ({expr.arguments.Count}) does not match the definition's arity ({s.arity})");
                }

                expr.internalFunction = s;

                if (!s.constructor) 
                { 
                    symbolTable.SetContext(context); 
                }

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


                symbolTable.UpContext();

                return null;
            }

            public override object? visitDeclareExpr(Expr.Declare expr)
            {
                var name = expr.name;

                if (symbolTable.TryGetVariable(name.lexeme, out _, out _, true))
                {
                    throw new Errors.AnalyzerError("Double Declaration", $"A variable named '{name.lexeme}' is already defined in this scope");
                }


                base.visitDeclareExpr(expr);
                (expr.stack.size, expr.stack.type.literals, var definition) = GetTypeAndSize(expr.stack.type);
                symbolTable.Add(expr.stack, name, definition);

                return null;
            }

            public override object? visitPrimitiveExpr(Expr.Primitive expr)
            {
                if (handledClasses.Contains(expr))
                {
                    return null;
                }

                handledClasses.Add(expr);

                symbolTable.SetContext(symbolTable.GetContainer(expr.name.lexeme));

                expr.block.Accept(this);


                symbolTable.UpContext();

                return null;
            }

            public override object? visitFunctionExpr(Expr.Function expr)
            {
                symbolTable.SetContext(symbolTable.GetContainer(expr.name.lexeme, true));

                if (expr._returnType.type.name.type != "void")
                {
                    (expr._returnSize, expr._returnType.literals, _) = GetTypeAndSize(expr._returnType);
                }


                symbolTable.CreateBlock();

                if (!expr.modifiers["static"])
                {
                    symbolTable.Current.self.size += 8;
                }

                for (int i = 0; i < expr.arity; i++)
                {
                    Expr.Parameter paramExpr = expr.parameters[i];
                    (paramExpr.member.variable.stack.size, paramExpr.member.variable.stack.type.literals, var definition) = GetTypeAndSize(paramExpr.member.variable.stack.type);
                    symbolTable.Add(paramExpr.member.variable.stack, paramExpr.name, definition);
                }

                expr.block.Accept(this);


                symbolTable.RemoveUnderCurrent();
                symbolTable.UpContext();

                if (expr.constructor)
                {
                    // Assumes a function is enclosed by a class (no nested functions)
                    expr.block.Extend(((SymbolTable.Symbol.Class)symbolTable.Current).self.topLevelBlock);
                }

                return null;
            }

            public override object? visitVariableExpr(Expr.Variable expr)
            {
                if (symbolTable.TryGetVariable(expr.name.lexeme, out SymbolTable.Symbol.Variable symbol, out bool isClassScoped))
                {
                    expr.stack.size = symbol.self.size;
                    expr.stack.stackOffset = symbol.self.stackOffset;
                    expr.stack.type = symbol.self.type;

                    if (isClassScoped)
                    {
                        symbolTable.other.classScopedVars.Add(expr.stack);
                        symbolTable.other.classScopedVars.Add(symbol.self);
                    }
                }
                else
                {
                    throw new Errors.AnalyzerError("Undefined Reference", $"The variable '{expr.name.lexeme}' does not exist in the current context");
                }
                return null;
            }

            //public override object? visitDefineExpr(Expr.Define expr)
            //{
            //    //symbolTable.Add(expr);
            //    //return null;
            //}

            public override object? visitAssignExpr(Expr.Assign expr)
            {
                expr.member.Accept(this);
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
                var context = symbolTable.Current;

                CurrentCalls();

                expr.call.constructor = true;

                expr.call.Accept(this);

                expr.internalClass = ((SymbolTable.Symbol.Class)symbolTable.Current.enclosing).self;

                symbolTable.SetContext(context);

                return null;
            }

            public override object visitMemberExpr(Expr.Member expr)
            {
                var context = symbolTable.Current;

                base.visitMemberExpr(expr);

                symbolTable.SetContext(context);

                return null;
            }

            public override object? visitGetExpr(Expr.Get expr)
            {
                var variable = symbolTable.GetVariable(expr.name.lexeme);

                if (variable.definition.IsPrimitive())
                {
                    throw new Errors.AnalyzerError("Primitive Field Access", "Primitive classes cannot contain fields");
                }

                expr.stackOffset = variable.self.stackOffset;

                symbolTable.SetContext(variable.definition);

                expr.get.Accept(this);

                return null;
            }

            public override object visitThisExpr(Expr.This expr)
            {
                expr.type = NearestEnclosingClass().self.QualifiedName;

                if (expr.get == null) { return null; }

                var context = symbolTable.Current;


                symbolTable.SetContext(NearestEnclosingClass());

                expr.get.Accept(this);

                symbolTable.SetContext(context);

                return null;
            }

            public override object visitIsExpr(Expr.Is expr)
            {
                if (symbolTable.TryGetVariable(((Expr.Variable)expr.left).ToString(), out SymbolTable.Symbol.Variable symbol, out _))
                {
                    expr.value = symbol.self.type.ToString() == expr.right.ToString()? "1" : "0";
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

            private (int, List<string>, SymbolTable.Symbol.Container) GetTypeAndSize(Expr.Type type)
            {
                if (symbolTable.TryGetContainerFullScope(type.type.name.lexeme, out var container, true))
                {
                    var x = type.type.get;
                    while (x != null)
                    {
                        container = symbolTable.GetContainer(x.name.lexeme, container);
                        x = x.get;
                    }
                }
                else
                {
                    throw new Errors.AnalyzerError("Undefined Reference", $"The class '{type.type.name.lexeme}' does not exist in the current context");
                }


                if (!(container.IsClass() || container.IsPrimitive()))
                {
                    //
                    throw new Exception();
                }

                if (container.IsPrimitive())
                {
                    var self = ((SymbolTable.Symbol.Primitive)container).self;
                    return (self.size, self.literals, container);
                }
                return (8, null, container);
            }

            private SymbolTable.Symbol.Container NearestEnclosingClass()
            {
                // Assumes a function is enclosed by a class (no nested functions)
                return symbolTable.Current.IsFunc() ? symbolTable.Current.enclosing : symbolTable.Current;
            }
        }
    }
}
