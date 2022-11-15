using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Espionage
{
    internal partial class Analyzer
    {
        internal partial class TypeCheckPass : Pass<string>
        {
            List<string> _return;
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
                string operator1 = expr.left.Accept(this);
                string operator2 = expr.right.Accept(this);

                if ((Primitives.PrimitiveOps(operator1))
                        ._operators.TryGetValue((expr.op.lexeme + " BIN", operator1, operator2), out string value))
                {
                    return value;
                }
                else
                {
                    throw new Errors.BackendError(ErrorType.BackendException, "Invalid Operator", $"You cannot apply operator '{expr.op.lexeme}' on types '{operator1}' and '{operator2}'");
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
                if (expr.type.lexeme != assignType && assignType != "keyword_null")
                {
                    throw new Errors.BackendError(ErrorType.BackendException, "Type Mismatch", $"You cannot assign type '{assignType}' to type '{expr.type.lexeme}'");
                }
                return "void";
            }

            public override string visitFunctionExpr(Expr.Function expr)
            {
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
                    if (_return.Count == 0 && expr._returnType != "null")
                    {
                        throw new Errors.BackendError(ErrorType.BackendException, "No Return", $"A function must have a 'return' expression");
                    }
                    _return.ForEach(x =>
                    {
                        if (x != expr._returnType)
                        {
                            throw new Errors.BackendError(ErrorType.BackendException, "Type Mismatch", $"You cannot return type '{x}' from type '{expr._returnType}'");
                        }
                    });
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
                if (expr.condition != null)
                    expr.condition.Accept(this);

                expr.block.Accept(this);
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
                string operator1 = expr.operand.Accept(this);

                if ((Primitives.PrimitiveOps(operator1))
                        ._operators.TryGetValue((expr.op.lexeme + " UN", operator1, ""), out string value))
                {
                    return value;
                }
                else
                {
                    throw new Errors.BackendError(ErrorType.BackendException, "Invalid Operator", $"You cannot apply operator '{expr.op.lexeme}' on types '{operator1}'");
                }
            }

            public override string visitVariableExpr(Expr.Variable expr)
            {
                return expr.type;
            }

            public override string visitReturnExpr(Expr.Return expr)
            {
                _return.Add(expr.value.Accept(this));
                return "void";
            }

            public override string visitAssignExpr(Expr.Assign expr)
            {
                expr.value.Accept(this);
                return "void";
            }

            public override string visitKeywordExpr(Expr.Keyword expr)
            {
                return expr.keyword; 
            }

            public override string visitPrimitiveExpr(Expr.Primitive expr)
            {
                string assignType = expr.literal.value.Accept(this);
                if (expr.literal.type.lexeme != assignType && assignType == "keyword_null")
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
        }
    }
}
