﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.Remoting;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Raze
{
    internal partial class Analyzer
    {
        internal class MainPass : Pass<object?>
        {
            SymbolTable symbolTable;

            public MainPass(List<Expr> expressions) : base(expressions)
            {
                this.symbolTable = new();
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
                int startFrame = symbolTable.count;

                foreach (Expr blockExpr in expr.block)
                {
                    blockExpr.Accept(this);
                }

                // De-alloc variables
                if (!expr._classBlock)
                {
                    symbolTable.RemoveUnderCurrent(startFrame);
                }

                return null;
            }

            public override object? visitCallExpr(Expr.Call expr)
            {
                for (int i = 0; i < expr.arguments.Count; i++)
                {
                    expr.arguments[i].Accept(this);
                }

                symbolTable.CurrentCalls();

                if (expr.internalFunction != null && expr.internalFunction.modifiers["static"])
                {
                    return null;
                }

                var context = symbolTable.Current;
                var x = expr.callee;
                while (x is Expr.Get)
                {
                    if (!symbolTable.DownContext(x.name.lexeme))
                    {
                        throw new Errors.BackendError("Undefined Reference", $"The class '{x.name.lexeme}' does not exist in the current context", symbolTable.callStack);
                    }
                    x = ((Expr.Get)x).get;
                }

                if (symbolTable.ContainsContainerKey(x.name.lexeme, out SymbolTable.Symbol.Container symbol, 0))
                {
                    var s = ((SymbolTable.Symbol.Function)symbol).self;
                    if (s.modifiers["static"])
                    {
                        throw new Errors.BackendError("Static Mathod Called From Instance", "You cannot call a static method from an instance");
                    }
                    if (s.constructor)
                    {
                        throw new Errors.BackendError("Constructor Called As Method", "A Constructor may not be called as a method of its class");
                    }
                    expr.internalFunction = s;
                    expr.stackOffset = symbolTable.currentFunction.self.size;
                }
                else
                {
                    throw new Errors.BackendError("Undefined Reference", $"The method '{expr.callee}' does not exist in the current context", symbolTable.callStack);
                }
                symbolTable.Current = context;

                return null;
            }

            public override object? visitClassExpr(Expr.Class expr)
            {
                if (handledClasses.Contains(expr))
                {
                    return null;
                }

                handledClasses.Add(expr);

                if (symbolTable.ContainsContainerKey(expr.name.lexeme, out _, 1))
                {
                    throw new Errors.BackendError("Double Declaration", $"A class named '{expr.name.lexeme}' is already defined in this scope", symbolTable.callStack);
                }
                symbolTable.Add(expr);
                expr.topLevelBlock.Accept(this);
                expr.block.Accept(this);
                if (!symbolTable.UpContext())
                {
                    throw new Exception("Up Context Called On 'GLOBAL' context (no enclosing)");
                }
                return null;
            }

            public override object? visitDeclareExpr(Expr.Declare expr)
            {
                // Function Todo Notice:
                // Note: since classes aren't implemented yet, functions are in a very early stage.
                // The flaws with storing functions on the stack, function defitions, function calls, sizeof, and typeof will be resolved in later commits.
                string type = expr.type.lexeme;
                string name = expr.name.lexeme;

                if (symbolTable.ContainsVariableKey(name))
                {
                    throw new Errors.BackendError("Double Declaration", $"A variable named '{name.lexeme}' is already defined in this scope", symbolTable.callStack);
                }

                if (expr.value is Expr.New)
                {
                    symbolTable.Add(((Expr.New)expr.value));
                }
                else
                {
                    symbolTable.Add(expr);
                }
                base.visitDeclareExpr(expr);
                return null;
            }

            public override object? visitPrimitiveExpr(Expr.Primitive expr)
            {
                return null;
            }

            public override object? visitFunctionExpr(Expr.Function expr)
            {
                symbolTable.Add(expr);
                int arity = expr.arity;
                int frameStart = symbolTable.count;
                for (int i = 0; i < arity; i++)
                {
                    Expr.Parameter paramExpr = expr.parameters[i];
                    symbolTable.Add(paramExpr);
                }

                expr.block.Accept(this);


                symbolTable.RemoveUnderCurrent(frameStart);
                symbolTable.UpContext();
                return null;
            }

            public override object? visitVariableExpr(Expr.Variable expr)
            {
                if (symbolTable.ContainsVariableKey(expr.name.lexeme, out SymbolTable.Symbol symbol))
                {
                    if (symbol.IsPrimitiveClass())
                    {
                        var s = ((SymbolTable.Symbol.PrimitiveClass)symbol).self;
                        expr.size = s.size;

                        // ToDo: Clean Up This Code
                        if (symbolTable.currentFunction.self.modifiers["static"])
                            expr.stackOffset = s.stackOffset;
                        else
                            expr.stackOffset = ((SymbolTable.Symbol.PrimitiveClass)symbol)._classOffset;

                        expr.type = s.type;
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
                        throw new Exception();
                    }
                }
                else
                {
                    // Todo: :( fix starting here (6 fine passes). issue is that curr func is main, b/c it looks for that when calling
                    // getvarkey to let non static func acess class but here Main is asking for s1.id so the curr func is main and it breaks
                    // note: class var acess is allowed here.
                    throw new Errors.BackendError("Undefined Reference", $"The variable '{expr.name.lexeme}' does not exist in the current context", symbolTable.callStack);
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

            public override object? visitNewExpr(Expr.New expr)
            {
                expr.stackOffset = symbolTable.currentFunction.self.size;
                expr.internalClass.block.Accept(this);
                if (!symbolTable.UpContext())
                {
                    throw new Exception("Up Context Called On 'GLOBAL' context (no enclosing)");
                }
                return null;
            }

            public override object? visitGetExpr(Expr.Get expr)
            {
                if (!symbolTable.DownContext(expr.name.lexeme))
                {
                    throw new Errors.BackendError("Undefined Reference", $"The class '{expr.name.lexeme}' does not exist in the current context", symbolTable.callStack);
                }
                expr.get.Accept(this);
                expr.type = expr.get.type;
                expr.stackOffset = expr.get.stackOffset;
                symbolTable.UpContext();
                return null;
            }

            public override object visitIsExpr(Expr.Is expr)
            {
                if (symbolTable.ContainsVariableKey(((Expr.Variable)expr.left).ToString(), out SymbolTable.Symbol symbol))
                {
                    if (symbol.IsPrimitiveClass())
                    {
                        var s = ((SymbolTable.Symbol.PrimitiveClass)symbol).self;

                        expr.value = ((SymbolTable.Symbol.PrimitiveClass)symbol).self.type.lexeme == expr.right.ToString()? "1" : "0";

                    }
                    else if (symbol.IsDefine())
                    {
                        var s = ((SymbolTable.Symbol.Define)symbol).self;
                        
                    }
                    else if (symbol.IsClass())
                    {
                        var s = ((SymbolTable.Symbol.Class)symbol).self;
                        expr.value = (s.FullName == expr.right.ToString())? "1" : "0";
                    }
                    else
                    {
                        throw new Exception();
                    }
                }
                else
                {
                    throw new Errors.BackendError("Undefined Reference", $"The variable '{((Expr.Variable)expr.left).ToString()}' does not exist in the current context", symbolTable.callStack);
                }
                return null;
            }
        }
    }
}
