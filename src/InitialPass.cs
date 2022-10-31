using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace Espionage
{
    internal partial class Analyzer
    {
        internal partial class InitialPass : Pass
        {
            Dictionary<string, Expr.Function> functions;
            List<Expr.Call> undefCalls;
            Expr.Function main;
            public InitialPass(List<Expr> expressions) : base(expressions)
            {
                this.undefCalls = new();
                this.functions = new();
            }

            internal override List<Expr> Run()
            {
                foreach (Expr expr in expressions)
                {
                    expr.Accept(this);
                }
                if (undefCalls.Count > 0)
                {
                    for (int i = 0, stackCount = undefCalls.Count; i < stackCount; i++)
                    {
                        throw new Errors.BackendError(ErrorType.BackendException, "Undefined Reference", $"The function '{undefCalls[i].callee.lexeme}' does not exist in the current context");
                    }
                }
                if (main == null)
                {
                    throw new Errors.BackendError(ErrorType.BackendException, "Entrypoint Not Found", "Program does not contain a Main method");
                }
                return expressions;
            }

            internal Expr.Function getMain()
            {
                return main;
            }

            public override object? visitFunctionExpr(Expr.Function expr)
            {
                // Function Todo Notice:
                // Note: since classes aren't implemented yet, functions are in a very early stage.
                // The flaws with storing functions on the stack, function defitions, function calls, sizeof, and typeof will be resolved in later commits.

                ResolveFunction(expr);
                if (expr.name.lexeme == "Main")
                {
                    if (main != null)
                    {
                        throw new Errors.BackendError(ErrorType.BackendException, "Main Declared Twice", "A Program may have only one 'Main' method");
                    }
                    expr._static = true;
                    main = expr;
                }
                int paramsCount = expr.parameters.Count;
                if (paramsCount > 6)
                {
                    throw new Errors.BackendError(ErrorType.BackendException, "Too Many Parameters", "A function cannot have more than 6 parameters");
                }
                expr.block.Accept(this);
                return null;
            }

            private void ResolveFunction(Expr.Function expr)
            {
                List<Expr.Call> resolvedCalls = undefCalls.FindAll(x => x.callee.lexeme == expr.name.lexeme);

                functions.Add(expr.name.lexeme, expr);

                if (resolvedCalls != null && resolvedCalls.Count != 0)
                {
                    ValidCallCheck(expr, resolvedCalls);
                    undefCalls.RemoveAll(x => resolvedCalls.Contains(x));
                }
            }

            private Expr.Function ResolveCall(Expr.Call expr)
            {
                Expr.Function value;
                string name = expr.callee.lexeme;
                if (functions.TryGetValue(name, out value))
                {
                    ValidCallCheck(value, new List<Expr.Call>() { expr });
                }
                else
                {
                    undefCalls.Add(expr);
                }
                return value;
            }

            private void ValidCallCheck(Expr.Function function, List<Expr.Call> resolvedCalls)
            {
                string name = function.name.lexeme;
                int arity = function.arity;
                foreach (var call in resolvedCalls)
                {
                    if (arity != call.arguments.Count)
                    {
                        throw new Errors.BackendError(ErrorType.BackendException, "Arity Mismatch", $"Arity of call for {name} ({call.arguments.Count}) does not match the definition's arity ({arity})");
                    }

                    for (int i = 0; i < arity; i++)
                    {
                        var param = function.parameters[i].type.lexeme;
                        if (param != TypeOf(call.arguments[i]))
                        {
                            throw new Errors.BackendError(ErrorType.BackendException, "Type Mismatch", $"In call for {name}, type of '{param}' does not match the definition's type '{TypeOf(call.arguments[i])}'");
                        }
                    }

                    call.internalFunction = function;
                }
            }

            public override object? visitCallExpr(Expr.Call expr)
            {
                var function = ResolveCall(expr);
                expr.internalFunction = function;
                return null;
            }
        }
    }
}
