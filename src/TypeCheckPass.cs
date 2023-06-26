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
                    if (!Primitives.IsVoidType(result) && !callReturn)
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
                Expr.Type[] argumentTypes =
                {
                    expr.left.Accept(this),
                    expr.right.Accept(this)
                };
                
                var context = symbolTable.Current;

                var (arg0, arg1) = (Primitives.IsLiteralTypeOrVoid(argumentTypes[0]), Primitives.IsLiteralTypeOrVoid(argumentTypes[1]));

                if (arg0.Item1 && arg1.Item1)
                {
                    return Primitives.Operation(expr.op, arg0.Item2, expr.left, arg1.Item2, expr.right);
                }

                if (!arg0.Item1)
                {
                    symbolTable.SetContext((Expr.Definition)argumentTypes[0]);

                    if (symbolTable.TryGetFunction(Primitives.SymbolToPrimitiveName(expr.op), argumentTypes, out var symbol))
                    {
                        expr.internalFunction = symbol;
                    }
                }
                
                if (expr.internalFunction == null && !arg1.Item1) 
                {
                    symbolTable.SetContext((Expr.Definition)argumentTypes[1]);

                    if (symbolTable.TryGetFunction(Primitives.SymbolToPrimitiveName(expr.op), new Expr.Type[] { argumentTypes[1], argumentTypes[0] }, out var symbol))
                    {
                        expr.internalFunction = symbol;
                    }
                }

                if (expr.internalFunction == null)
                {
                    Primitives.InvalidOperation(expr.op, argumentTypes[0].ToString(), argumentTypes[1].ToString());
                }

                symbolTable.SetContext(context);

                if (expr.internalFunction.modifiers["inline"])
                {
                    context.size = Math.Max(context.size, expr.encSize + expr.internalFunction.size);
                }

                return expr.internalFunction._returnType.type;
            }

            public override Expr.Type visitBlockExpr(Expr.Block expr)
            {
                foreach (var blockExpr in expr.block)
                {
                    Expr.Type result = blockExpr.Accept(this);

                    if (!Primitives.IsVoidType(result) && !callReturn)
                    {
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
                    symbolTable.SetContext(symbolTable.GetFunction(expr.name.lexeme, argumentTypes));
                }
                else
                {
                    symbolTable.SetContext(null);
                    if (symbolTable.TryGetFunction(expr.name.lexeme, argumentTypes, out var symbol))
                    {
                        symbolTable.SetContext(symbol);
                    }
                    else
                    {
                        symbolTable.SetContext(symbolTable.NearestEnclosingClass(context));
                        symbolTable.SetContext(symbolTable.GetFunction(expr.name.lexeme, argumentTypes));
                    }
                }

                // 
                if (symbolTable.Current.definitionType != Expr.Definition.DefinitionType.Function)
                {
                    throw new Exception();
                }

                ValidateCall(expr, ((Expr.Function)symbolTable.Current));

                expr.internalFunction = ((Expr.Function)symbolTable.Current);

                symbolTable.SetContext(context);

                if (expr.internalFunction.modifiers["inline"])
                {
                    context.size = Math.Max(context.size, expr.encSize + expr.internalFunction.size);
                }

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

                    if (!Primitives.IsVoidType(result) && !callReturn)
                    {
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
                    if (_returnCount == 0 && !Primitives.IsVoidType(expr._returnType.type))
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
                if (!Primitives.IsVoidType(result) && !callReturn)
                {
                    throw new Errors.AnalyzerError("Expression With Non-Null Return", $"Expression returned with type '{result}'");
                }
                callReturn = false;

                result = expr.updateExpr.Accept(this);
                if (!Primitives.IsVoidType(result) && !callReturn)
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
                Expr.Type[] argumentTypes =
                {
                    expr.operand.Accept(this)
                };

                var context = symbolTable.Current;

                var arg = Primitives.IsLiteralTypeOrVoid(argumentTypes[0]);

                if (arg.Item1)
                {
                    return Primitives.Operation(expr.op, arg.Item2, expr.operand);
                }

                if (!arg.Item1)
                {
                    symbolTable.SetContext((Expr.Definition)argumentTypes[0]);

                    if (symbolTable.TryGetFunction(Primitives.SymbolToPrimitiveName(expr.op), argumentTypes, out var symbol))
                    {
                        expr.internalFunction = symbol;
                    }
                }

                if (expr.internalFunction == null)
                {
                    Primitives.InvalidOperation(expr.op, argumentTypes[0].ToString());
                }
                symbolTable.SetContext(context);

                if (expr.internalFunction.modifiers["inline"])
                {
                    context.size = Math.Max(context.size, expr.encSize + expr.internalFunction.size);
                }

                if (Primitives.SymbolToPrimitiveName(expr.op) == "Increment")
                {
                    callReturn = true;
                }

                return expr.internalFunction._returnType.type;
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
                MustMatchType(expr.member.stack.type, assignType);

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
                foreach (var variable in expr.variables)
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
            }
        }
    }
}
