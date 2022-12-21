using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze
{
    internal partial class Analyzer
    {
        internal partial class TypeCheckPass : Pass<string>
        {
            List<(string, bool)> _return;
            bool callReturn;
            public TypeCheckPass(List<Expr> expressions) : base(expressions)
            {
                _return = new();
            }

            internal override List<Expr> Run()
            {
                foreach (Expr expr in expressions)
                {
                    string result = expr.Accept(this);
                    if (result != "void" && !callReturn)
                    {
                        callReturn = false; 
                        throw new Errors.BackendError(ErrorType.BackendException, "Expression With Non-Null Return", $"Expression returned with type '{result}'");
                    }
                    if (_return.Count != 0)
                    {
                        throw new Errors.BackendError(ErrorType.BackendException, "Top Level Code", $"Top level 'return' is Not allowed");
                    }
                }
                return expressions;
            }

            public override string visitBinaryExpr(Expr.Binary expr)
            {
                string operand1 = expr.left.Accept(this);
                string operand2 = expr.right.Accept(this);

                if ((Primitives.PrimitiveOps(operand1))
                        ._operators.TryGetValue((expr.op.lexeme + " BIN", operand1, operand2), out string value))
                {
                    return value;
                }
                else
                {
                    if (operand1 == "null" || operand2 == "null")
                    {
                        throw new Errors.BackendError(ErrorType.BackendException, "Null Reference Exception", $"Reference is not set to an instance of an object.");
                    }

                    throw new Errors.BackendError(ErrorType.BackendException, "Invalid Operator", $"You cannot apply operator '{expr.op.lexeme}' on types '{operand1}' and '{operand2}'");
                }
            }

            public override string visitBlockExpr(Expr.Block expr)
            {
                foreach (var blockExpr in expr.block)
                {
                    string result = blockExpr.Accept(this);
                    if (expr._classBlock && _return.Count != 0)
                    {
                        throw new Errors.BackendError(ErrorType.BackendException, "Invalid Return", $"A class may not have a 'return'");
                    }

                    if (result != "void" && !callReturn)
                    {
                        callReturn = false; 
                        throw new Errors.BackendError(ErrorType.BackendException, "Expression With Non-Null Return", $"Expression returned with type '{result}'");
                    }
                }
                return "void";
            }

            public override string visitCallExpr(Expr.Call expr)
            {
                for (int i = 0; i < expr.internalFunction.arity; i++)
                {
                    Expr.Parameter paramExpr = expr.internalFunction.parameters[i];
                    expr.arguments[i].Accept(this);
                    if (paramExpr.type.lexeme != expr.arguments[i].Accept(this))
                    {
                        throw new Errors.BackendError(ErrorType.BackendException, "Type Mismatch", $"In call for {expr.internalFunction.name.lexeme}, type of '{paramExpr.type.lexeme}' does not match the definition's type '{expr.arguments[i].Accept(this)}'");
                    }
                }
                callReturn = true;
                return expr.internalFunction._returnType;
            }

            public override string visitClassExpr(Expr.Class expr)
            {
                expr.block.Accept(this);
                return "void";
            }

            public override string visitDeclareExpr(Expr.Declare expr)
            {
                string assignType = expr.value.Accept(this);
                if (expr.type.lexeme != assignType && assignType != "null")
                {
                    throw new Errors.BackendError(ErrorType.BackendException, "Type Mismatch", $"You cannot assign type '{assignType}' to type '{expr.type.lexeme}'");
                }
                return "void";
            }

            public override string visitFunctionExpr(Expr.Function expr)
            {
                if (expr.dead)
                {
                    return "void";
                }
                if (expr.modifiers["unsafe"])
                {
                    return "void";
                }
                foreach (Expr.Parameter paramExpr in expr.parameters)
                {
                    paramExpr.Accept(this);
                }
                expr.block.Accept(this);
                if (!expr.constructor)
                {
                    int _returnCount = 0;
                    foreach (var ret in _return)
                    {
                        if (ret.Item1 != expr._returnType && ret.Item1 != "null")
                        {
                            throw new Errors.BackendError(ErrorType.BackendException, "Type Mismatch", $"You cannot return type '{ret.Item1}' from type '{expr._returnType}'");
                        }
                        if (!ret.Item2)
                        {
                            _returnCount++;
                        }
                    }
                    if (_returnCount == 0 && expr._returnType != "void")
                    {
                        if (_return.Count == 0)
                        {
                            throw new Errors.BackendError(ErrorType.BackendException, "No Return", "A Function must have a 'return' expression");
                        }
                        else
                        {
                            throw new Errors.BackendError(ErrorType.BackendException, "No Return", "A Function must have a 'return' expression from all code paths");
                        }
                    }
                    _return.Clear();
                }
                else
                {
                    if (_return.Count != 0)
                    {
                        throw new Errors.BackendError(ErrorType.BackendException, "Invalid Return", $"A constructor cannot have a 'return' expression");
                    }
                }
                return "void";
            }

            public override string visitGetExpr(Expr.Get expr)
            {
                return expr.get.Accept(this);
            }

            public override string visitGroupingExpr(Expr.Grouping expr)
            {
                return expr.expression.Accept(this);
            }

            public override string visitConditionalExpr(Expr.Conditional expr)
            {
                int _returnCount = _return.Count;
                if (expr.condition != null)
                {
                    if (expr.condition.Accept(this) != "bool")
                    {
                        throw new Errors.BackendError(ErrorType.BackendException, "Type Mismatch", $"'{expr.type.type}' expects condition to return 'bool'. Got '{expr.condition.Accept(this)}'");
                    }
                }
                expr.block.Accept(this);
                if (expr.type.type != "else")
                {
                    for (int i = _returnCount; i < _return.Count; i++)
                    {
                        _return[i] = (_return[i].Item1, true);
                    }
                }
                return "void";
            }

            public override string visitLiteralExpr(Expr.Literal expr)
            {
                return TypeOf(expr);
            }

            public override string visitSuperExpr(Expr.Super expr)
            {
                return null;
            }

            public override string visitThisExpr(Expr.This expr)
            {
                return null;
            }

            public override string visitUnaryExpr(Expr.Unary expr)
            {
                string operand1 = expr.operand.Accept(this);
                if ((Primitives.PrimitiveOps(operand1))
                        ._operators.TryGetValue((expr.op.lexeme + " UN", operand1, ""), out string value))
                {
                    return value;
                }
                else
                {
                    if (operand1 == "null")
                    {
                        throw new Errors.BackendError(ErrorType.BackendException, "Null Reference Exception", $"Reference is not set to an instance of an object.");
                    }
                    throw new Errors.BackendError(ErrorType.BackendException, "Invalid Operator", $"You cannot apply operator '{expr.op.lexeme}' on type '{operand1}'");
                }
            }

            public override string visitVariableExpr(Expr.Variable expr)
            {
                return (expr.define.Item1)? expr.define.Item2.Accept(this)
                       : (expr.stackOffset != null)? expr.type.lexeme : "null";
            }

            public override string visitReturnExpr(Expr.Return expr)
            {
                _return.Add((expr.value.Accept(this), false));
                return "void";
            }

            public override string visitAssignExpr(Expr.Assign expr)
            {
                if (expr.op != null)
                {
                    string operand = expr.value.Accept(this);

                    if (!(Primitives.PrimitiveOps(expr.variable.type.lexeme))
                            ._operators.ContainsKey((expr.op.lexeme + " BIN", expr.variable.type.lexeme, operand)))
                    {
                        throw new Errors.BackendError(ErrorType.BackendException, "Invalid Operator", $"You cannot apply operator '{expr.op.lexeme}' on types '{expr.variable.type}' and '{operand}'");
                    }
                }
                else
                {
                    expr.value.Accept(this);
                }
                return "void";
            }

            public override string visitKeywordExpr(Expr.Keyword expr)
            {
                return TypeOf(expr); 
            }

            public override string visitPrimitiveExpr(Expr.Primitive expr)
            {
                string assignType = expr.literal.value.Accept(this);
                if (expr.literal.type.lexeme != assignType && assignType != "null")
                {
                    throw new Errors.BackendError(ErrorType.BackendException, "Type Mismatch", $"You cannot assign type '{assignType}' to type '{expr.literal.type.lexeme}'");
                }
                return "void";
            }

            public override string visitNewExpr(Expr.New expr)
            {
                foreach (Expr argExpr in expr.arguments)
                {
                    argExpr.Accept(this);
                }

                return expr._className.lexeme;
            }

            public override string visitDefineExpr(Expr.Define expr)
            {
                return "void";
            }
        }
    }
}
