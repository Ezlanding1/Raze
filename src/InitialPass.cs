﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze
{
    internal partial class Analyzer
    {
        internal partial class InitialPass : Pass<object?>
        {
            SymbolTable symbolTable;

            List<Expr.New> undefClass;
            List<Expr.Call> undefCalls;
            
            string declClassName;

            Tuple<bool, int, Expr.If> waitingIf;

            int index;
            Expr.Function main;

            bool checkFuncs;
            int checkType;

            SymbolTable.Symbol.Container? resolvedContainer;

            public InitialPass(List<Expr> expressions) : base(expressions)
            {
                this.symbolTable = new();

                this.undefClass = new();
                this.undefCalls = new();

                this.index = 0;
            }

            internal override List<Expr> Run()
            {
                foreach (Expr expr in expressions)
                {
                    expr.Accept(this);
                }
                checkFuncs = true;
                ResolveReferences();
                if (main == null)
                {
                    throw new Errors.BackendError(ErrorType.BackendException, "Entrypoint Not Found", "Program does not contain a Main method");
                }
                return expressions;
            }

            internal Expr.Function GetOutput()
            {
                return main;
            }

            public override object? visitBlockExpr(Expr.Block expr)
            {
                foreach (var blockExpr in expr.block)
                {
                    blockExpr.Accept(this);
                    index++;
                }
                return null;
            }

            public override object? visitFunctionExpr(Expr.Function expr)
            {
                // Function Todo Notice:
                // Note: since classes aren't implemented yet, functions are in a very early stage.
                // The flaws with storing functions on the stack, function defitions, function calls, sizeof, and typeof will be resolved in later commits.
                symbolTable.Add(expr);

                if (symbolTable.ContainsContainerKey(expr.name.lexeme))
                {
                    if (expr.name.lexeme == "Main")
                    {
                        throw new Errors.BackendError(ErrorType.BackendException, "Main Declared Twice", "A Program may have only one 'Main' method");
                    }
                    throw new Errors.BackendError(ErrorType.BackendException, "Function Declared Twice", $"Function '{expr.name.lexeme}' was declared twice");
                }

                if (expr.name.lexeme == "Main")
                {
                    if (main != null)
                    {
                        throw new Errors.BackendError(ErrorType.BackendException, "Main Declared Twice", "A Program may have only one 'Main' method");
                    }
                    expr.modifiers["static"] = true;
                    main = expr;
                }
                int paramsCount = expr.parameters.Count;
                if (paramsCount > 6)
                {
                    throw new Errors.BackendError(ErrorType.BackendException, "Too Many Parameters", "A function cannot have more than 6 parameters");
                }
                expr.block.Accept(this);

                symbolTable.UpContext();
                return null;
            }

            public override object? visitCallExpr(Expr.Call expr)
            {
                undefCalls.Add(expr);
                foreach (var argExpr in expr.arguments)
                {
                    argExpr.Accept(this);
                }
                return null;
            }

            public override object? visitDeclareExpr(Expr.Declare expr)
            {
                declClassName = expr.name.lexeme;
                return base.visitDeclareExpr(expr);
            }

            public override object? visitClassExpr(Expr.Class expr)
            {
                symbolTable.Add(expr);

                expr.block.Accept(this);

                symbolTable.UpContext();
                return null;
            }
            public override object? visitConditionalExpr(Expr.Conditional expr)
            {
                if (expr.type.type == "if")
                {
                    waitingIf = new Tuple<bool, int, Expr.If>(true, index, (Expr.If)expr);
                }
                else if (expr.type.type == "else if")
                {
                    if (waitingIf != null && (waitingIf.Item1 == true && waitingIf.Item2 == (index - 1)))
                    {
                        Expr.ElseIf elif = (Expr.ElseIf)expr;
                        elif.top = waitingIf.Item3;
                        waitingIf.Item3.ElseIfs.Add(elif);
                        waitingIf = new(true, waitingIf.Item2+1, waitingIf.Item3);
                    }
                    else
                    {
                        throw new Errors.BackendError(ErrorType.BackendException, "Invalid Else If", "'else if' conditional has no matching 'if'");
                    }
                }
                else if (expr.type.type == "else")
                {
                    if (waitingIf != null && (waitingIf.Item1 == true && waitingIf.Item2 == (index - 1)))
                    {
                        Expr.Else _else = (Expr.Else)expr;
                        _else.top = waitingIf.Item3;
                        waitingIf.Item3._else = _else;
                        waitingIf = new(false, waitingIf.Item2, null);
                    }
                    else
                    {
                        throw new Errors.BackendError(ErrorType.BackendException, "Invalid Else", "'else' conditional has no matching 'if'");
                    }
                }
                int tmpidx = index;
                base.visitConditionalExpr(expr);
                index = tmpidx;
                return null;
            }

            public override object? visitNewExpr(Expr.New expr)
            {
                expr.declName = declClassName;

                undefClass.Add(expr);

                return null;
            }

            public override object? visitAssemblyExpr(Expr.Assembly expr)
            {
                if (symbolTable.CurrentIsTop())
                {
                    throw new Errors.BackendError(ErrorType.BackendException, "Top Level Assembly Block", "Assembly Blocks must be placed in an unsafe function");
                }
                if (!symbolTable.Current.IsFunc())
                {
                    throw new Errors.BackendError(ErrorType.BackendException, "ASM Block Not In Function", "Assembly Blocks must be placed in functions");
                }
                if (!((SymbolTable.Symbol.Function)symbolTable.Current).self.modifiers["unsafe"])
                {
                    throw new Errors.BackendError(ErrorType.BackendException, "Unsafe Code in Safe Function", "Mark a function with 'unsafe' to include unsafe code");
                }
                return base.visitAssemblyExpr(expr);
            }

            public override object? visitGetExpr(Expr.Get expr)
            {
                if (!checkFuncs)
                {
                    return null;
                }

                if (!symbolTable.DownContainerContext(expr.name.lexeme))
                {
                    throw new Errors.BackendError(ErrorType.BackendException, "Undefined Reference", $"The variable '{expr.name.lexeme}' does not exist in the current context");
                }
                expr.get.Accept(this);
                return null;
            }

            public override object visitVariableExpr(Expr.Variable expr)
            {
                if (!checkFuncs)
                {
                    return null;
                }

                if (checkType == 0)
                    symbolTable.ContainsContainerKey(expr.name.lexeme, out resolvedContainer, checkType);
                else if (checkType == 1)
                {
                    symbolTable.ContainsContainerKey(expr.name.lexeme, out resolvedContainer, checkType);
                    symbolTable.Current = resolvedContainer;
                    symbolTable.ContainsContainerKey(expr.name.lexeme, out var constructor, 0);
                    ((SymbolTable.Symbol.Function)constructor).self.constructor = true;
                    ((SymbolTable.Symbol.Class)resolvedContainer).self.constructor = ((SymbolTable.Symbol.Function)constructor).self;
                }

                return null;
            }


            private void ResolveReferences()
            {
                checkType = 0;
                foreach (var call in undefCalls)
                {
                    call.callee.Accept(this);

                    if (resolvedContainer == null)
                        throw new Errors.BackendError(ErrorType.BackendException, "Undefined Reference", $"The function '{call.callee.ToString()}' does not exist in the current context");
                    else
                        call.internalFunction = ((SymbolTable.Symbol.Function)resolvedContainer).self;

                    symbolTable.TopContext();

                    ValidCallCheck(call.internalFunction, call);
                }
                checkType = 1;
                foreach (var @ref in undefClass)
                {
                    @ref._className.Accept(this);

                    if (resolvedContainer == null)
                        throw new Errors.BackendError(ErrorType.BackendException, "Undefined Reference", $"The class '{@ref._className.ToString()}' does not exist in the current context");
                    else
                        @ref.internalClass = ((SymbolTable.Symbol.Class)resolvedContainer).self;

                    symbolTable.TopContext();

                    ValidClassCheck(@ref.internalClass, @ref);
                }
            }

            private void ValidCallCheck(Expr.Function function, Expr.Call call)
            {
                string name = function.name.lexeme;
                if (function.arity != call.arguments.Count)
                {
                    throw new Errors.BackendError(ErrorType.BackendException, "Arity Mismatch", $"Arity of call for {name} ({call.arguments.Count}) does not match the definition's arity ({function.arity})");
                }
            }

            private void ValidClassCheck(Expr.Class _class, Expr.New c)
            {
                c.internalClass = _class;
                c.internalClass.dName = c.declName;
                c.internalClass.block._classBlock = true;
            }
        }
    }
}
