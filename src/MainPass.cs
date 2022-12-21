using System;
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

            Expr.Function mainFunc;
            Expr.Function current;
            List<Expr.Define> globalDefines;

            public MainPass(List<Expr> expressions, Expr.Function main, List<Expr.Define> globalDefines) : base(expressions)
            {
                this.symbolTable = new();

                this.mainFunc = main;
                this.current = main;
                this.globalDefines = globalDefines;
            }

            internal override List<Expr> Run()
            {
                foreach (var define in globalDefines)
                {
                    define.Accept(this);
                }
                mainFunc.Accept(this);
                return expressions;
            }

            public override object? visitBlockExpr(Expr.Block expr)
            {
                foreach (Expr blockExpr in expr.block)
                {
                    if (blockExpr is Expr.Function)
                    {
                        var b = (Expr.Function)blockExpr;
                        if (b.constructor)
                        {
                            blockExpr.Accept(this);
                        }
                    }
                    else
                    {
                        blockExpr.Accept(this);
                    } 
                }
                // De-alloc variables
                if (!expr._classBlock)
                {
                    symbolTable.RemoveUnderCurrent();
                }
                return null;
            }

            public override object? visitCallExpr(Expr.Call expr)
            {
                for (int i = 0; i < expr.internalFunction.arity; i++)
                {
                    expr.arguments[i].Accept(this);
                }
                if (!expr.found)
                {
                    expr.found = true;
                    expr.internalFunction.Accept(this);
                }
                return null;
            }

            public override object? visitClassExpr(Expr.Class expr)
            {
                base.visitClassExpr(expr);
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


                expr.size = SizeOf(type, expr.value);

                if (symbolTable.ContainsKey(name))
                {
                    throw new Errors.BackendError(ErrorType.BackendException, "Double Declaration", $"A variable named '{name}' is already defined in this scope", symbolTable.callStack);
                }

                if (expr.value is Expr.New)
                {
                    symbolTable.Add(((Expr.New)expr.value).internalClass);
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
                // Function Todo Notice:
                // Note: since classes aren't implemented yet, functions are in a very early stage.
                // The flaws with storing functions on the stack, function defitions, function calls, sizeof, and typeof will be resolved in later commits.
                Token type = expr.literal.type;
                Token name = expr.literal.name;

                base.visitPrimitiveExpr(expr);

                int size = expr.literal.size;
                if (symbolTable.ContainsKey(name.lexeme))
                {
                    throw new Errors.BackendError(ErrorType.BackendException, "Double Declaration", $"A variable named '{name}' is already defined in this scope", symbolTable.callStack);
                }
                var v = new Expr.Variable(type, name, size);
                symbolTable.Add(v);
                expr.stackOffset = v.stackOffset;
                return null;
            }

            public override object? visitFunctionExpr(Expr.Function expr)
            {
                expr.dead = false;
                current = expr;
                symbolTable.Add(expr);
                int arity = expr.arity;
                for (int i = 0; i < arity; i++)
                {
                    Expr.Parameter paramExpr = expr.parameters[i];
                    paramExpr.size = SizeOf(paramExpr.type.lexeme);
                    symbolTable.Add(paramExpr);
                }

                expr.block.Accept(this);


                symbolTable.RemoveUnderCurrent();
                symbolTable.UpContext();
                return null;
            }

            public override object? visitVariableExpr(Expr.Variable expr)
            {
                if (symbolTable.ContainsKey(expr.name.lexeme, out int varVal, out Token type, current.constructor))
                {
                    expr.size = SizeOf(type.lexeme);
                    expr.stackOffset = varVal;
                    expr.type = type;
                }
                else if (symbolTable.ContainsDefine(expr.name.lexeme, out Expr.Literal defVal))
                {
                    expr.define.Item1 = true;
                    expr.define.Item2 = defVal;
                }
                else
                {
                    throw new Errors.BackendError(ErrorType.BackendException, "Undefined Reference", $"The variable '{expr.name.lexeme}' does not exist in the current context", symbolTable.callStack);
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

                expr.variable.size = SizeOf(expr.variable.type.lexeme);
                return null;
            }

            public override object? visitNewExpr(Expr.New expr)
            {
                expr.internalClass.Accept(this);
                return null;
            }

            public override object? visitGetExpr(Expr.Get expr)
            {
                if (!symbolTable.DownContext(expr.name.lexeme))
                {
                    throw new Errors.BackendError(ErrorType.BackendException, "Undefined Reference", $"The variable '{expr.name.lexeme}' does not exist in the current context", symbolTable.callStack);
                }
                expr.get.Accept(this);
                expr.type = expr.get.type;
                expr.stackOffset = expr.get.stackOffset;
                symbolTable.UpContext();
                return null;
            }
        }
    }
}
