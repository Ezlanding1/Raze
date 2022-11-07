using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.Remoting;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using static Espionage.Expr;
using System.Xml.Linq;

namespace Espionage
{
    internal partial class Analyzer
    {
        internal class MainPass : Pass
        {
            Stack stack;
            CallStack callStack;
            Expr.Function mainFunc;
            public MainPass(List<Expr> expressions, Expr.Function main) : base(expressions)
            {
                this.stack = new();
                this.callStack = new();
                this.mainFunc = main;
            }

            internal override List<Expr> Run()
            {
                mainFunc.Accept(this);
                return expressions;
            }

            public override object? visitBlockExpr(Expr.Block expr)
            {
                int frameStart = stack.count;
                foreach (Expr blockExpr in expr.block)
                {
                    blockExpr.Accept(this);
                }
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

            public override object? visitCallExpr(Call expr)
            {
                for (int i = 0; i < expr.internalFunction.arity; i++)
                {
                    Expr.Parameter paramExpr = expr.internalFunction.parameters[i];
                    expr.arguments[i].Accept(this);
                    if (paramExpr.type.lexeme != TypeOf(expr.arguments[i]))
                    {
                        throw new Errors.BackendError(ErrorType.BackendException, "Type Mismatch", $"In call for {expr.internalFunction.name.lexeme}, type of '{paramExpr.type.lexeme}' does not match the definition's type '{TypeOf(expr.arguments[i])}'");
                    }
                }
                return base.visitCallExpr(expr);    
            }

            public override object? visitClassExpr(Expr.Class expr)
            {
                stack.AddClass(expr.name.lexeme, expr.dName);
                callStack.Add(expr);
                base.visitClassExpr(expr);
                stack.CurrentUp();
                //stack.RemoveLast();
                callStack.RemoveLast();
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

                int size = SizeOf(type);
                if (stack.ContainsKey(name))
                {
                    throw new Errors.BackendError(ErrorType.BackendException, "Double Declaration", $"A variable named '{name}' is already defined in this scope", callStack);
                }

                stack.Add(type, name, size);
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
                    throw new Errors.BackendError(ErrorType.BackendException, "Double Declaration", $"A variable named '{name}' is already defined in this scope", callStack);
                }
                stack.AddPrim(type, name, expr.literal.Location(size), size);
                expr.stackOffset = stack.stackOffset;
                return null;
            }

            public override object? visitFunctionExpr(Expr.Function expr)
            {
                stack.AddFunc(expr.name.lexeme, expr._static);
                int arity = expr.arity;
                for (int i = 0; i < arity; i++)
                {
                    Expr.Parameter paramExpr = expr.parameters[i];
                    stack.Add(paramExpr.type.lexeme, paramExpr.variable.lexeme, InstructionTypes.paramRegister[i]);
                }

                callStack.Add(expr);

                expr.block.Accept(this);

                callStack.RemoveLast();

                for (int i = 0; i < arity; i++)
                {
                    stack.RemoveLastParam();
                }
                stack.RemoveLast();
                return null;
            }

            public override object? visitVariableExpr(Expr.Variable expr)
            {
                if (stack.ContainsKey(expr.variable.lexeme, out string value, out string type))
                {
                    if (value == null)
                    {
                        throw new Errors.BackendError(ErrorType.BackendException, "Null Reference Exception", $"reference to object {expr.variable.lexeme} not set to an instance of an object.", callStack);
                    }
                    expr.stackPos = value;
                    expr.type = type;
                    if (char.IsLetter(value[0]))
                    {
                        expr.register = true;
                    }
                }
                else
                {
                    throw new Errors.BackendError(ErrorType.BackendException, "Undefined Reference", $"The variable '{expr.variable.lexeme}' does not exist in the current context", callStack);
                }
                return null;
            }

            public override object? visitAssignExpr(Expr.Assign expr)
            {
                string name = "";
                expr.variable.Accept(this);
                string type = expr.variable.type;

                base.visitAssignExpr(expr);

                int size = SizeOf(type);
                stack.Modify(type, name, size);
                expr.offset = stack.stackOffset;
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
                    throw new Errors.BackendError(ErrorType.BackendException, "Undefined Reference", $"The variable '{expr.variable.lexeme}' does not exist in the current context", callStack);
                }
                expr.get.Accept(this);
                expr.type = expr.get.type;
                stack.SwitchContext("BACK");
                return null;
            }
        }
    }
}
