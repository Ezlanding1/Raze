using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static Raze.Analyzer.SymbolTable.Symbol;
using static Raze.Expr;

namespace Raze
{
    internal partial class Analyzer
    {
        internal partial class InitialPass : Pass<object?>
        {
            SymbolTable symbolTable;

            List<Expr.Call> undefCalls;
            List<Expr.Is> undefIs;
            List<(Expr.Variable?, Expr.New?)> undefVariables;

            Dictionary<string, Expr.Primitive> primitives;

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

                this.undefCalls = new();
                this.undefIs = new();
                this.undefVariables = new();

                this.primitives = new();

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

            internal (Expr.Function, Dictionary<string, Expr.Primitive>) GetOutput()
            {
                return (main, primitives);
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
                if (paramsCount > InstructionInfo.paramRegister.Length)
                {
                    throw new Errors.BackendError(ErrorType.BackendException, "Too Many Parameters", $"A function cannot have more than { InstructionInfo.paramRegister.Length } parameters");
                }

                foreach (Expr.Parameter paramExpr in expr.parameters)
                {
                    paramExpr.Accept(this);
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
                if (!checkFuncs)
                {
                    undefVariables.Add((expr, null));
                }

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

                undefVariables.RemoveAt(undefVariables.Count - 1);
                undefVariables.Add((null, expr));

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
                    return null;
                }

                expr.get.Accept(this);
                return null;
            }

            public override object? visitVariableExpr(Expr.Variable expr)
            {
                if (!checkFuncs)
                {
                    return null;
                }

                symbolTable.ContainsContainerKey(expr.name.lexeme, out resolvedContainer, checkType);

                return null;
            }

            public override object visitAssignExpr(Expr.Assign expr)
            {
                expr.variable.Accept(this);
                return base.visitAssignExpr(expr);
            }

            public override object visitPrimitiveExpr(Expr.Primitive expr)
            {
                if (!primitives.ContainsKey(expr.name.lexeme))
                {
                    primitives[expr.name.lexeme] = expr;
                }
                else
                {
                    throw new Errors.BackendError(ErrorType.BackendException, "Double Declaration", $"A primtive named '{expr.name.lexeme}' is already defined");
                }
                return null;
            }

            public override object? visitIsExpr(Expr.Is expr)
            {
                if (!checkFuncs)
                {
                    if (!(expr.left is Expr.Variable))
                    {
                        throw new Errors.BackendError(ErrorType.BackendException, "Invalid 'is' Operator", "the first operand of 'is' operator must be a variable");
                    }
                    undefIs.Add(expr);
                    return null;
                }
                
                expr.right.Accept(this);
                return null;
            }


            private void ResolveReferences()
            {
                checkType = 0;
                foreach (var call in undefCalls)
                {
                    call.callee.Accept(this);

                    if (resolvedContainer == null)
                        continue;

                    call.internalFunction = ((SymbolTable.Symbol.Function)resolvedContainer).self;
                    call.internalFunction.path = symbolTable.GetPath();

                    if (!call.internalFunction.modifiers["static"])
                    {
                        throw new Errors.BackendError(ErrorType.BackendException, "Static Call of Non-Static Method", $"The method '{call.callee.ToString()}' must be marked 'static' to call it from a static context");
                    }

                    symbolTable.TopContext();

                    ValidCallCheck(call.internalFunction, call);
                    resolvedContainer = null;
                }

                checkType = 1;
                // ToDo: Clean Up This Code
                foreach (var (variable, @ref) in undefVariables)
                {
                    if (@ref != null)
                    {
                        @ref._className.Accept(this);

                        if (resolvedContainer == null)
                            throw new Errors.BackendError(ErrorType.BackendException, "Undefined Reference", $"The type '{@ref._className.ToString()}' does not exist in the current context");
                        else
                            // ToDo: Clean Up This Code
                            @ref.internalClass = ((SymbolTable.Symbol.Class)resolvedContainer).self.CloneVars();

                        symbolTable.TopContext();

                        ValidClassCheck(@ref.internalClass, @ref);
                        resolvedContainer = null;
                    }
                    else
                    {
                        if (primitives.ContainsKey(variable.type.lexeme))
                        {
                            variable.size = primitives[variable.type.lexeme].size;
                        }
                    }
                }
                foreach (var @is in undefIs)
                {
                    string type = "";
                    
                    @is.Accept(this);

                    if (resolvedContainer == null && (!Primitives.PrimitiveSize.ContainsKey(@is.right.ToString())) && @is.right.ToString() != "null")
                    {
                        throw new Errors.BackendError(ErrorType.BackendException, "Undefined Reference", $"The type '{@is.right.ToString()}' does not exist in the current context");
                    }
                    symbolTable.TopContext();
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
                c.internalClass = new(_class.name, _class.block);
                c.internalClass.dName = new(c.declName);
                c.internalClass.block._classBlock = true;
            }
        }
    }
}
