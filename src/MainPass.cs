using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.Remoting;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Espionage
{
    internal partial class Analyzer
    {
        internal class MainPass : Pass<object?>
        {
            Stack stack;
            Expr.Function mainFunc;
            Expr.Function current;
            public MainPass(List<Expr> expressions, Expr.Function main) : base(expressions)
            {
                this.stack = new();
                this.mainFunc = main;
                this.current = main;
            }

            internal override List<Expr> Run()
            {
                mainFunc.Accept(this);
                return expressions;
            }

            public override object? visitBlockExpr(Expr.Block expr)
            {
                var tmpCurrent = current;
                int countStart = stack.stackOffset;
                int frameStart = stack.count;
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
                current = tmpCurrent;
                current.size += (stack.stackOffset - countStart);
                // De-alloc variables
                if (!expr._classBlock)
                {
                    stack.RemoveUnderCurrent(frameStart);
                    //for (int i = frameStart, frameEnd = stack.count; i < frameEnd; i++)
                    //{
                    //    stack.RemoveLast();
                    //}
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
                    var temp = stack.stackOffset;
                    stack.stackOffset = 0;
                    expr.internalFunction.Accept(this);
                    stack.stackOffset = temp;
                }
                return null;
            }

            public override object? visitClassExpr(Expr.Class expr)
            {
                stack.AddClass(expr.name.lexeme, expr.dName);
                base.visitClassExpr(expr);
                stack.CurrentUp();
                //stack.RemoveLast();
                return null;
            }

            public override object? visitDeclareExpr(Expr.Declare expr)
            {
                // Function Todo Notice:
                // Note: since classes aren't implemented yet, functions are in a very early stage.
                // The flaws with storing functions on the stack, function defitions, function calls, sizeof, and typeof will be resolved in later commits.
                string type = expr.type.lexeme;
                string name = expr.name.lexeme;

                base.visitDeclareExpr(expr);

                int? size = SizeOf(type, expr.value);
                if (stack.ContainsKey(name))
                {
                    throw new Errors.BackendError(ErrorType.BackendException, "Double Declaration", $"A variable named '{name}' is already defined in this scope", stack.callStack);
                }

                if (expr.value is Expr.New)
                {
                    stack.AddClass(type, name);
                }
                else
                {
                stack.Add(type, name, size);
                }
                base.visitDeclareExpr(expr);
                expr.offset = stack.stackOffset;
                return null;
            }

            public override object? visitPrimitiveExpr(Expr.Primitive expr)
            {
                // Function Todo Notice:
                // Note: since classes aren't implemented yet, functions are in a very early stage.
                // The flaws with storing functions on the stack, function defitions, function calls, sizeof, and typeof will be resolved in later commits.
                string type = expr.literal.type.lexeme;
                string name = expr.literal.name.lexeme;

                base.visitPrimitiveExpr(expr);

                int size = expr.literal.size;
                if (stack.ContainsKey(name))
                {
                    throw new Errors.BackendError(ErrorType.BackendException, "Double Declaration", $"A variable named '{name}' is already defined in this scope", stack.callStack);
                }
                stack.Add(type, name, size);
                expr.stackOffset = stack.stackOffset;
                return null;
            }

            public override object? visitFunctionExpr(Expr.Function expr)
            {
                expr.dead = false;
                current = expr;
                stack.AddFunc(expr.name.lexeme, expr.modifiers);
                int arity = expr.arity;
                for (int i = 0; i < arity; i++)
                {
                    Expr.Parameter paramExpr = expr.parameters[i];
                    stack.Add(paramExpr.type.lexeme, paramExpr.variable.lexeme, SizeOf(paramExpr.type.lexeme));
                    paramExpr.offset = stack.stackOffset;
                    paramExpr.size = SizeOf(paramExpr.type.lexeme);
                }

                expr.block.Accept(this);


                for (int i = 0; i < arity; i++)
                {
                    stack.RemoveLastParam();
                }
                stack.callStack.RemoveLast();
                stack.RemoveLast();
                return null;
            }

            public override object? visitVariableExpr(Expr.Variable expr)
            {
                if (stack.ContainsKey(expr.variable.lexeme, out int? varVal, out string type, current.constructor))
                {
                    if (value == null)
                    {
                        throw new Errors.BackendError(ErrorType.BackendException, "Null Reference Exception", $"reference to object {expr.variable.lexeme} not set to an instance of an object.", stack.callStack);
                    }
                    expr.size = SizeOf(type);
                    expr.offset = varVal;
                    expr.type = type;
                }
                else
                {
                    throw new Errors.BackendError(ErrorType.BackendException, "Undefined Reference", $"The variable '{expr.variable.lexeme}' does not exist in the current context", stack.callStack);
                }
                return null;
            }

            public override object? visitAssignExpr(Expr.Assign expr)
            {
                string name = "";
                expr.variable.Accept(this);
                string type = expr.variable.type;

                base.visitAssignExpr(expr);

                int? size = SizeOf(type);
                stack.Modify(type, name, size);
                return null;
            }

            public override object? visitNewExpr(Expr.New expr)
            {
                expr.internalClass.Accept(this);
                return null;
            }

            public override object? visitGetExpr(Expr.Get expr)
            {
                if (!stack.SwitchContext("DOWN", expr.variable.lexeme))
                {
                    throw new Errors.BackendError(ErrorType.BackendException, "Undefined Reference", $"The variable '{expr.variable.lexeme}' does not exist in the current context", stack.callStack);
                }
                expr.get.Accept(this);
                expr.type = expr.get.type;
                expr.offset = expr.get.offset;
                stack.SwitchContext("BACK");
                return null;
            }
        }
    }
}
