using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Raze
{
    internal partial class Analyzer
    {
        internal partial class TypeCheckPass : Pass<string>
        {
            List<(string, bool, Expr.Return)> _return;
            bool callReturn;
            Dictionary<string, Expr.Primitive> primitives;

            public TypeCheckPass(List<Expr> expressions) : base(expressions)
            {
                _return = new();
                this.primitives = SymbolTableSingleton.SymbolTable.other.primitives;
            }

            internal override List<Expr> Run()
            {
                foreach (Expr expr in expressions)
                {
                    string result = expr.Accept(this);
                    if (result != "void" && !callReturn)
                    {
                        throw new Errors.AnalyzerError("Expression With Non-Null Return", $"Expression returned with type '{result}'");
                    }
                    if (_return.Count != 0)
                    {
                        throw new Errors.AnalyzerError("Top Level Code", $"Top level 'return' is Not allowed");
                    }
                    callReturn = false;
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
                        throw new Errors.AnalyzerError("Null Reference Exception", $"Reference is not set to an instance of an object.");
                    }

                    throw new Errors.AnalyzerError("Invalid Operator", $"You cannot apply operator '{expr.op.lexeme}' on types '{operand1}' and '{operand2}'");
                }
            }

            public override string visitBlockExpr(Expr.Block expr)
            {
                foreach (var blockExpr in expr.block)
                {
                    string result = blockExpr.Accept(this);
                    if (expr._classBlock && _return.Count != 0)
                    {
                        throw new Errors.AnalyzerError("Invalid Return", $"A class may not have a 'return'");
                    }

                    if (result != "void" && !callReturn)
                    {
                        throw new Errors.AnalyzerError("Expression With Non-Null Return", $"Expression returned with type '{result}'");
                    }
                    callReturn = false; 
                }
                return "void";
            }

            public override string visitCallExpr(Expr.Call expr)
            {
                for (int i = 0; i < expr.internalFunction.arity; i++)
                {
                    Expr.Parameter paramExpr = expr.internalFunction.parameters[i];

                    var assignType = expr.arguments[i].Accept(this);
                    if (!MatchesType(paramExpr.stack.type.ToString(), assignType))
                    {
                        throw new Errors.AnalyzerError("Type Mismatch", $"You cannot assign type '{assignType}' to type '{paramExpr.stack.type.ToString()}'");
                    }
                }
                callReturn = true;
                return expr.internalFunction._returnType.ToString();
            }

            public override string visitClassExpr(Expr.Class expr)
            {
                expr.topLevelBlock.Accept(this);
                expr.block.Accept(this);
                return "void";
            }

            public override string visitDeclareExpr(Expr.Declare expr)
            {
                string assignType = expr.value.Accept(this);
                if (!MatchesType(expr.stack.type.ToString(), assignType))
                {
                    throw new Errors.AnalyzerError("Type Mismatch", $"You cannot assign type '{assignType}' to type '{expr.stack.type.ToString()}'");
                }

                return "void";
            }

            public override string visitFunctionExpr(Expr.Function expr)
            {
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
                        if (!MatchesType(expr._returnType.ToString(), ret.Item1))
                        {
                            throw new Errors.AnalyzerError("Type Mismatch", $"You cannot return type '{ret.Item1}' from type '{expr._returnType}'");
                        }

                        if (primitives.ContainsKey(expr._returnType.ToString()))
                        {
                            ret.Item3.size = primitives[expr._returnType.ToString()].size;
                        }

                        if (!ret.Item2)
                        {
                            _returnCount++;
                        }
                    }
                    if (_returnCount == 0 && expr._returnType.name.type != "void")
                    {
                        if (!expr.modifiers["unsafe"])
                        {
                            if (_return.Count == 0)
                            {
                                throw new Errors.AnalyzerError("No Return", "A Function must have a 'return' expression");
                            }
                            else
                            {
                                throw new Errors.AnalyzerError("No Return", "A Function must have a 'return' expression from all code paths");
                            }
                        }
                    }
                    _return.Clear();
                }
                else
                {
                    if (_return.Count != 0)
                    {
                        throw new Errors.AnalyzerError("Invalid Return", $"A constructor cannot have a 'return' expression");
                    }
                }
                return "void";
            }

            public override string visitGetExpr(Expr.Get expr)
            {
                if (expr.get == null) { return null; }

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
                    if (expr.condition.Accept(this) != "BOOLEAN")
                    {
                        throw new Errors.AnalyzerError("Type Mismatch", $"'{expr.type.type}' expects condition to return 'BOOLEAN'. Got '{expr.condition.Accept(this)}'");
                    }
                }
                expr.block.Accept(this);
                if (expr.type.type != "else")
                {
                    for (int i = _returnCount; i < _return.Count; i++)
                    {
                        _return[i] = (_return[i].Item1, true, _return[i].Item3);
                    }
                }
                return "void";
            }

            public override string visitLiteralExpr(Expr.Literal expr)
            {
                return TypeOf(expr);
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
                        throw new Errors.AnalyzerError("Null Reference Exception", $"Reference is not set to an instance of an object.");
                    }
                    throw new Errors.AnalyzerError("Invalid Operator", $"You cannot apply operator '{expr.op.lexeme}' on type '{operand1}'");
                }
            }

            public override string visitVariableExpr(Expr.Variable expr)
            {
                return (expr.define.Item1)? expr.define.Item2.Accept(this): expr.stack.type.ToString();
            }

            public override string visitReturnExpr(Expr.Return expr)
            {
                _return.Add((expr.value.Accept(this), false, expr));
                return "void";
            }

            public override string visitAssignExpr(Expr.Assign expr)
            {
                string assignType = expr.value.Accept(this);

                if (expr.op != null)
                {
                    string operand = expr.value.Accept(this);

                    if (!(Primitives.PrimitiveOps(expr.member.variable.stack.type.ToString()))
                            ._operators.ContainsKey((expr.op.lexeme + " BIN", expr.member.variable.stack.type.ToString(), operand)))
                    {
                        throw new Errors.AnalyzerError("Invalid Operator", $"You cannot apply operator '{expr.op.lexeme}' on types '{expr.member.variable.stack.type}' and '{operand}'");
                    }
                }
                else
                {
                    if (!MatchesType(expr.member.variable.stack.type.ToString(), assignType))
                    {
                        throw new Errors.AnalyzerError("Type Mismatch", $"You cannot assign type '{assignType}' to type '{expr.member.variable.stack.type.ToString()}'");
                    }
                }
                return "void";
            }

            public override string visitKeywordExpr(Expr.Keyword expr)
            {
                return TypeOf(expr); 
            }

            public override string visitPrimitiveExpr(Expr.Primitive expr)
            {
                return "void";
            }

            public override string visitNewExpr(Expr.New expr)
            {
                foreach (Expr argExpr in expr.call.arguments)
                {
                    argExpr.Accept(this);
                }

                return expr.call.callee.ToString();
            }

            public override string visitDefineExpr(Expr.Define expr)
            {
                return "void";
            }

            public override string visitIsExpr(Expr.Is expr)
            {
                return "BOOLEAN";
            }

            public override string visitAssemblyExpr(Expr.Assembly expr)
            {
                foreach (var variable in expr.variables.Keys)
                {
                    variable.Accept(this);
                }

                return "void";
            }

            private bool MatchesType(string type1, string type2)
            {
                if (primitives.ContainsKey(type1))
                {
                    if (Parser.Literals.Contains(type2))
                    {
                        if ((!primitives[type1].literals.Contains(type2)) && type2 != "null")
                        {
                            return false;
                        }
                    }
                    else
                    {
                        if (type1 != type2 && type2 != "null")
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    if (type1 != type2 && type2 != "null")
                    {
                        return false;
                    }
                }
                return true;
            }
        }
    }
}
