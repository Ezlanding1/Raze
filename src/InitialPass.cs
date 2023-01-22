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
        internal partial class InitialPass : Pass<object?>
        {
            SymbolTable symbolTable = SymbolTableSingleton.SymbolTable;

            List<Expr.Call> undefCalls;
            List<Expr.Is> undefIs;
            List<(Expr.Variable?, Expr.New?)> undefVariables;

            Tuple<bool, int, Expr.If> waitingIf;

            int index;
            Expr.Function main;

            bool checkFuncs;
            int checkType;

            SymbolTable.Symbol.Container? resolvedContainer;

            public InitialPass(List<Expr> expressions) : base(expressions)
            {
                this.undefCalls = new();
                this.undefIs = new();
                this.undefVariables = new();

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
                    throw new Errors.BackendError("Entrypoint Not Found", "Program does not contain a Main method");
                }
                return expressions;
            }

            internal (Expr.Function, Dictionary<string, Expr.Primitive>) GetOutput()
            {
                return (main, SymbolTable.other.primitives);
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
                if (symbolTable.ContainsContainerKey(expr.name.lexeme))
                {
                    if (expr.name.lexeme == "Main")
                    {
                        throw new Errors.BackendError("Double Declaration", "A Program may have only one 'Main' method");
                    }
                    throw new Errors.BackendError("Double Declaration", $"Function '{expr.name.lexeme}()' was declared twice");
                }

                SetPath(expr);

                symbolTable.Add(expr);

                if (expr._returnType != "void")
                {
                    if (SymbolTable.other.primitives.ContainsKey(expr._returnType))
                    {
                        expr._returnSize = SymbolTable.other.primitives[expr._returnType].size;
                    }
                    else
                    {
                        throw new Exception();
                    }
                }

                if (expr.name.lexeme == "Main")
                {
                    if (main != null)
                    {
                        throw new Errors.BackendError("Function Declared Twice", "A Program may have only one 'Main' method");
                    }
                    expr.modifiers["static"] = true;
                    main = expr;
                }
                int paramsCount = expr.parameters.Count;
                if (paramsCount > InstructionInfo.paramRegister.Length)
                {
                    throw new Errors.BackendError("Too Many Parameters", $"A function cannot have more than { InstructionInfo.paramRegister.Length } parameters");
                }

                foreach (Expr.Parameter paramExpr in expr.parameters)
                {
                    undefVariables.Add((paramExpr, null));
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

                return base.visitDeclareExpr(expr);
            }

            public override object? visitClassExpr(Expr.Class expr)
            {
                SetPath(expr);

                symbolTable.Add(expr);

                expr.topLevelBlock.Accept(this);
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
                        throw new Errors.BackendError("Invalid Else If", "'else if' conditional has no matching 'if'");
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
                        throw new Errors.BackendError("Invalid Else", "'else' conditional has no matching 'if'");
                    }
                }
                int tmpidx = index;
                base.visitConditionalExpr(expr);
                index = tmpidx;
                return null;
            }

            public override object? visitNewExpr(Expr.New expr)
            {
                undefVariables.RemoveAt(undefVariables.Count - 1);
                undefVariables.Add((null, expr));

                return null;
            }

            public override object? visitAssemblyExpr(Expr.Assembly expr)
            {
                if (symbolTable.CurrentIsTop())
                {
                    throw new Errors.BackendError("Top Level Assembly Block", "Assembly Blocks must be placed in an unsafe function");
                }
                if (!symbolTable.Current.IsFunc())
                {
                    throw new Errors.BackendError("ASM Block Not In Function", "Assembly Blocks must be placed in functions");
                }
                if (!((SymbolTable.Symbol.Function)symbolTable.Current).self.modifiers["unsafe"])
                {
                    throw new Errors.BackendError("Unsafe Code in Safe Function", "Mark a function with 'unsafe' to include unsafe code");
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
                if (!SymbolTable.other.primitives.ContainsKey(expr.name.lexeme))
                {
                    SymbolTable.other.primitives[expr.name.lexeme] = expr;
                }
                else
                {
                    throw new Errors.BackendError("Double Declaration", $"A primtive named '{expr.name.lexeme}' is already defined");
                }
                return null;
            }

            public override object? visitIsExpr(Expr.Is expr)
            {
                if (!checkFuncs)
                {
                    if (!(expr.left is Expr.Variable))
                    {
                        throw new Errors.BackendError("Invalid 'is' Operator", "the first operand of 'is' operator must be a variable");
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

                    if (!call.internalFunction.modifiers["static"])
                    {
                        throw new Errors.BackendError("Static Call of Non-Static Method", $"The method '{call.callee.ToString()}' must be marked 'static' to call it from a static context");
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
                        @ref.call.callee.Accept(this);

                        if (resolvedContainer == null)
                            throw new Errors.BackendError("Undefined Reference", $"The type '{@ref.call.callee.ToString()}' does not exist in the current context");
                        else
                        {

                            // ToDo: Clean Up This Code
                            var resolvedClass = ((SymbolTable.Symbol.Class)resolvedContainer).self;
                            @ref.internalClass = resolvedClass;
                            @ref.internalClass.block._classBlock = true;
                            @ref.call.internalFunction = GetConstructor(resolvedContainer);
                        }

                        symbolTable.TopContext();
                        resolvedContainer = null;
                    }
                    else
                    {
                        if (SymbolTable.other.primitives.ContainsKey(variable.type.lexeme))
                        {
                            variable.size = SymbolTable.other.primitives[variable.type.lexeme].size;
                        }
                        else
                        {
                            throw new Errors.BackendError("Undefined Reference", $"The primitive type '{variable.type.lexeme}' does not exist in the current context");
                        }
                    }
                }
                foreach (var @is in undefIs)
                {
                    string type = "";
                    
                    @is.Accept(this);

                    if (resolvedContainer == null && (!Primitives.PrimitiveSize.ContainsKey(@is.right.ToString())) && @is.right.ToString() != "null")
                    {
                        throw new Errors.BackendError("Undefined Reference", $"The type '{@is.right.ToString()}' does not exist in the current context");
                    }
                    symbolTable.TopContext();
                }
            }

            private void ValidCallCheck(Expr.Function function, Expr.Call call)
            {
                string name = function.name.lexeme;
                if (function.arity != call.arguments.Count)
                {
                    throw new Errors.BackendError("Arity Mismatch", $"Arity of call for {name} ({call.arguments.Count}) does not match the definition's arity ({function.arity})");
                }
            }

            private Expr.Function GetConstructor(SymbolTable.Symbol.Container @class)
            {
                symbolTable.Current = @class;
                if (!symbolTable.ContainsContainerKey(symbolTable.Current.Name.lexeme, out var symbol, 0))
                {
                    throw new Errors.BackendError("Class Without Constructor", "A Class must contain a constructor method");
                }

                var constructor = ((SymbolTable.Symbol.Function)symbol).self;

                constructor.constructor = true;

                if (constructor.modifiers["static"])
                {
                    throw new Errors.BackendError("Constructor Marked 'static'", "A constructor cannot have the 'static' modifier");
                }
                return constructor;
            }

            private void SetPath(Expr.Definition definition)
            {
                if (symbolTable.Current.self.QualifiedName != "")
                {
                    definition.path = symbolTable.Current.self.QualifiedName + ".";
                }
            }
        }
    }
}
