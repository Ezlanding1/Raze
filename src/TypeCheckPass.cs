using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze
{
    internal partial class Analyzer
    {
        internal partial class TypeCheckPass : Pass<Expr.Type>
        {
            SymbolTable symbolTable = SymbolTableSingleton.SymbolTable;
            List<(Expr.Type, bool, Expr.Return)> _return;
            bool callReturn;

            public static Expr.Type _voidType = new(new(Token.TokenType.RESERVED, "void"));

            public static Dictionary<Token.TokenType, Expr.Type> literalTypes = new Dictionary<Token.TokenType, Expr.Type>()
            {
                { Parser.Literals[0], new(new Token(Parser.Literals[0])) },
                { Parser.Literals[1], new(new Token(Parser.Literals[1])) },
                { Parser.Literals[2], new(new Token(Parser.Literals[2])) },
                { Parser.Literals[3], new(new Token(Parser.Literals[3])) },
                { Parser.Literals[4], new(new Token(Parser.Literals[4])) },
                { Parser.Literals[5], new(new Token(Parser.Literals[5])) },
            };

            Dictionary<string, Expr.Type> keywordTypes = new Dictionary<string, Expr.Type>()
            {
                { "true", literalTypes[Token.TokenType.BOOLEAN] },
                { "false", literalTypes[Token.TokenType.BOOLEAN] },
                { "null", null },
            };

            public TypeCheckPass(List<Expr> expressions) : base(expressions)
            {
                _return = new();
            }

            internal override List<Expr> Run()
            {
                foreach (Expr expr in expressions)
                {
                    Expr.Type result = expr.Accept(this);
                    if (!IsVoidType(result) && !callReturn)
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

            public override Expr.Type visitBinaryExpr(Expr.Binary expr)
            {
                Expr.Type operand1 = expr.left.Accept(this);
                Expr.Type operand2 = expr.right.Accept(this);

                //if ((Primitives.PrimitiveOps(operand1))
                //        ._operators.TryGetValue((expr.op.lexeme + " BIN", operand1, operand2), out string value))
                //{
                //    return value;
                //}
                //else
                //{
                //    throw new Errors.AnalyzerError("Invalid Operator", $"You cannot apply operator '{expr.op.lexeme}' on types '{operand1}' and '{operand2}'");
                //}
                return null;
            }

            public override Expr.Type visitBlockExpr(Expr.Block expr)
            {
                foreach (var blockExpr in expr.block)
                {
                    Expr.Type result = blockExpr.Accept(this);

                    if (!IsVoidType(result) && !callReturn)
                    {
                        if (false)
                            throw new Errors.AnalyzerError("Expression With Non-Null Return", $"Expression returned with type '{result}'");
                    }
                    callReturn = false; 
                }
                return _voidType;
            }

            public override Expr.Type visitCallExpr(Expr.Call expr)
            {
                Expr.Type[] argumentTypes = new Expr.Type[expr.arguments.Count];

                for (int i = 0; i < expr.arguments.Count; i++)
                {
                    argumentTypes[i] = expr.arguments[i].Accept(this);
                }

                var context = symbolTable.Current;

                symbolTable.SetContext(expr.funcEnclosing);
                if (expr.callee != null)
                {
                    symbolTable.SetContext(symbolTable.GetFunction(expr.name, argumentTypes));
                }
                else
                {
                    symbolTable.SetContext(null);
                    if (symbolTable.TryGetFunction(expr.name, argumentTypes, out var symbol))
                    {
                        symbolTable.SetContext(symbol);
                    }
                    else
                    {
                        symbolTable.SetContext(symbolTable.NearestEnclosingClass(context));
                        symbolTable.SetContext(symbolTable.GetFunction(expr.name, argumentTypes));
                    }
                }

                // 
                if (symbolTable.Current.definitionType != Expr.Definition.DefinitionType.Function)
                {
                    throw new Exception();
                }

                var callee = ((Expr.Function)symbolTable.Current);

                ValidateCall(expr, callee);

                expr.internalFunction = callee;

                symbolTable.SetContext(context);

                callReturn = true;
                return expr.internalFunction._returnType.type;
            }

            public override Expr.Type visitClassExpr(Expr.Class expr)
            {
                symbolTable.SetContext(expr);

                Expr.ListAccept(expr.declarations, this);
                Expr.ListAccept(expr.definitions, this);

                symbolTable.UpContext();

                return _voidType;
            }

            public override Expr.Type visitDeclareExpr(Expr.Declare expr)
            {
                Expr.Type assignType = expr.value.Accept(this);
                MustMatchType(expr.stack.type, assignType);

                return _voidType;
            }

            public override Expr.Type visitFunctionExpr(Expr.Function expr)
            {
                symbolTable.SetContext(expr);

                foreach (var blockExpr in expr.block)
                {
                    Expr.Type result = blockExpr.Accept(this);

                    if (!IsVoidType(result) && !callReturn)
                    {
                        if (false)
                            throw new Errors.AnalyzerError("Expression With Non-Null Return", $"Expression returned with type '{result}'");
                    }
                    callReturn = false;
                }

                if (!expr.constructor)
                {
                    int _returnCount = 0;
                    foreach (var ret in _return)
                    {
                        MustMatchType(expr._returnType.type, ret.Item1, "You cannot return type '{0}' from type '{1}'");

                        ret.Item3.size = expr._returnSize;

                        if (!ret.Item2)
                        {
                            _returnCount++;
                        }
                    }
                    if (_returnCount == 0 && !IsVoidType(expr._returnType.type))
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

                symbolTable.UpContext();

                return _voidType;
            }

            public override Expr.Type visitTypeReferenceExpr(Expr.TypeReference expr)
            {
                return expr.type;
            }

            public override Expr.Type visitGetReferenceExpr(Expr.GetReference expr)
            {
                return expr.type;
            }

            public override Expr.Type visitGroupingExpr(Expr.Grouping expr)
            {
                return expr.expression.Accept(this);
            }

            public override Expr.Type visitIfExpr(Expr.If expr)
            {
                TypeCheckConditional(expr.conditional);

                expr.ElseIfs.ForEach(x => TypeCheckConditional(x.conditional));

                if (expr._else != null)
                TypeCheckConditional(expr._else.conditional);

                return _voidType;
            }

            public override Expr.Type visitWhileExpr(Expr.While expr)
            {
                TypeCheckConditional(expr.conditional);

                return _voidType;
            }

            public override Expr.Type visitForExpr(Expr.For expr)
            {
                var result = expr.initExpr.Accept(this);
                if (!IsVoidType(result) && !callReturn)
                {
                    throw new Errors.AnalyzerError("Expression With Non-Null Return", $"Expression returned with type '{result}'");
                }
                callReturn = false;

                result = expr.updateExpr.Accept(this);
                if (!IsVoidType(result) && !callReturn)
                {
                    throw new Errors.AnalyzerError("Expression With Non-Null Return", $"Expression returned with type '{result}'");
                }
                callReturn = false;

                TypeCheckConditional(expr.conditional);

                return _voidType;
            }

            public override Expr.Type visitLiteralExpr(Expr.Literal expr)
            {
                return literalTypes[expr.literal.type];
            }

            public override Expr.Type visitUnaryExpr(Expr.Unary expr)
            {
                Expr.Type operand1 = expr.operand.Accept(this);
                //if ((Primitives.PrimitiveOps(operand1))
                //        ._operators.TryGetValue((expr.op.lexeme + " UN", operand1, ""), out string value))
                //{
                //    return value;
                //}
                //else
                //{
                //    throw new Errors.AnalyzerError("Invalid Operator", $"You cannot apply operator '{expr.op.lexeme}' on type '{operand1}'");
                //}
                return null;
            }

            public override Expr.Type visitVariableExpr(Expr.Variable expr)
            {
                return expr.stack.type;
            }

            public override Expr.Type visitReturnExpr(Expr.Return expr)
            { 
                _return.Add((expr._void? _voidType : expr.value.Accept(this), false, expr));

                return _voidType;
            }

            public override Expr.Type visitAssignExpr(Expr.Assign expr)
            {
                Expr.Type assignType = expr.value.Accept(this);

                //if (expr.op != null)
                //{
                //    Expr.Type operand = expr.value.Accept(this);

                //    if (!(Primitives.PrimitiveOps(expr.member.stack.type.ToString()))
                //            ._operators.ContainsKey((expr.op.lexeme + " BIN", expr.member.stack.type.ToString(), operand)))
                //    {
                //        throw new Errors.AnalyzerError("Invalid Operator", $"You cannot apply operator '{expr.op.lexeme}' on types '{expr.member.stack.type}' and '{operand}'");
                //    }
                //}
                //else
                //{
                //    if (!MatchesType(expr.member.stack.type, assignType))
                //    {
                //        throw new Errors.AnalyzerError("Type Mismatch", $"You cannot assign type '{assignType}' to type '{expr.member.stack.type.ToString()}'");
                //    }
                //}
                return _voidType;
            }

            public override Expr.Type visitKeywordExpr(Expr.Keyword expr)
            {
                return keywordTypes[expr.keyword]; 
            }

            public override Expr.Type visitPrimitiveExpr(Expr.Primitive expr)
            {
                symbolTable.SetContext(expr);

                Expr.ListAccept(expr.definitions, this);

                symbolTable.UpContext();

                return _voidType;
            }

            public override Expr.Type visitNewExpr(Expr.New expr)
            {
                expr.call.Accept(this);

                return expr.internalClass;
            }

            public override Expr.Type visitDefineExpr(Expr.Define expr)
            {
                return _voidType;
            }

            public override Expr.Type visitIsExpr(Expr.Is expr)
            {
                expr.value = expr.right.Accept(this) == expr.right.type ? "1" : "0";

                return literalTypes[Token.TokenType.BOOLEAN];
            }

            public override Expr.Type visitAssemblyExpr(Expr.Assembly expr)
            {
                foreach (var variable in expr.variables.Keys)
                {
                    variable.Accept(this);
                }

                return _voidType;
            }

            private bool MatchesType(Expr.Type type1, Expr.Type type2)
            {
                return type2.Matches(type1);
            }

            private void MustMatchType(Expr.Type type1, Expr.Type type2, string error= "You cannot assign type '{0}' to type '{1}'")
            {
                if (!type2.Matches(type1))
                {
                    throw new Errors.AnalyzerError("Type Mismatch", string.Format(error, type2, type1));
                }
            }

            private void TypeCheckConditional(Expr.Conditional expr, bool _else=false)
            {
                int _returnCount = _return.Count;
                if (expr.condition != null)
                {
                    if (!expr.condition.Accept(this).Matches(literalTypes[Token.TokenType.BOOLEAN]))
                    {
                        throw new Errors.AnalyzerError("Type Mismatch", $"'if' expects condition to return 'BOOLEAN'. Got '{expr.condition.Accept(this)}'");
                    }
                }
                expr.block.Accept(this);

                if (!_else)
                {
                    for (int i = _returnCount; i < _return.Count; i++)
                    {
                        _return[i] = (_return[i].Item1, true, _return[i].Item3);
                    }
                }
            }

            private bool IsVoidType(Expr.Type type)
            { 
                return type.name.lexeme == "void"; 
            }

            private bool IsLiteralType(Expr.Type type, byte literal)
            {
                return type.name.type == Parser.Literals[literal];
            }

            private void ValidateCall(Expr.Call expr, Expr.Function callee)
            {
                if (!expr.constructor && callee.constructor)
                {
                    throw new Errors.AnalyzerError("Constructor Called As Method", "A Constructor may not be called as a method of its class");
                }
                else if (expr.constructor && !callee.constructor)
                {
                    throw new Errors.AnalyzerError("Method Called As Constructor", "A Method may not be called as a constructor of its class");
                }

                if (expr.callee != null)
                {
                    if (expr.instanceCall && callee.modifiers["static"])
                    {
                        throw new Errors.AnalyzerError("Static Method Called From Instance", "You cannot call a static method from an instance");
                    }
                    if (!expr.instanceCall && !callee.modifiers["static"] && !expr.constructor)
                    {
                        throw new Errors.AnalyzerError("Instance Method Called From Static Context", "You cannot call an instance method from a static context");
                    }
                }

                if (expr.arguments.Count != callee.arity)
                {
                    throw new Errors.BackendError("Arity Mismatch", $"Arity of call for {callee} ({expr.arguments.Count}) does not match the definition's arity ({callee.arity})");
                }
            }
        }
    }
}
