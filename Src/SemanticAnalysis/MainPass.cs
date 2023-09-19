namespace Raze;

internal partial class Analyzer
{
    internal class MainPass : Pass<Expr.Type>
    {
        SymbolTable symbolTable = SymbolTableSingleton.SymbolTable;
        List<(Expr.Type?, bool, Expr.Return?)> _return = new();
        bool callReturn;

        public MainPass(List<Expr> expressions) : base(expressions)
        {
        }

        internal override void Run()
        {
            foreach (var expr in expressions)
            {
                Expr.Type result = expr.Accept(this);

                if (!Primitives.IsVoidType(result) && !callReturn)
                {
                    throw new Error.AnalyzerError("Expression With Non-Null Return", $"Expression returned with type '{result}'");
                }
                if (_return.Count != 0)
                {
                    throw new Error.AnalyzerError("Top Level Code", $"Top level 'return' is Not allowed");
                }
                callReturn = false;
            }
        }

        public override Expr.Type VisitBlockExpr(Expr.Block expr)
        {
            symbolTable.CreateBlock();

            foreach (var blockExpr in expr.block)
            {
                Expr.Type result = blockExpr.Accept(this);

                if (!Primitives.IsVoidType(result) && !callReturn)
                {
                    throw new Error.AnalyzerError("Expression With Non-Null Return", $"Expression returned with type '{result}'");
                }
                callReturn = false;
            }

            symbolTable.RemoveBlock();

            return TypeCheckUtils._voidType;
        }

        public override Expr.Type VisitBinaryExpr(Expr.Binary expr)
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
                return TypeCheckUtils.literalTypes[Primitives.OperationType(expr.op, arg0.Item2, arg1.Item2)];
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
                throw Primitives.InvalidOperation(expr.op, argumentTypes[0].ToString(), argumentTypes[1].ToString());
            }

            symbolTable.SetContext(context);

            if (expr.internalFunction.parameters[0].modifiers["ref"] && TypeCheckUtils.CannotBeRef(expr.left))
            {
                throw new Error.AnalyzerError("Invalid Operator Argument", "Cannot assign when non-variable is passed to 'ref' parameter");
            }
            if (expr.internalFunction.parameters[1].modifiers["ref"] && TypeCheckUtils.CannotBeRef(expr.right))
            {
                throw new Error.AnalyzerError("Invalid Operator Argument", "Cannot assign when non-variable is passed to 'ref' parameter");
            }

            return expr.internalFunction._returnType.type;
        }

        public override Expr.Type VisitUnaryExpr(Expr.Unary expr)
        {
            Expr.Type[] argumentTypes =
            {
                expr.operand.Accept(this)
            };

            var context = symbolTable.Current;

            var arg = Primitives.IsLiteralTypeOrVoid(argumentTypes[0]);

            if (arg.Item1)
            {
                return TypeCheckUtils.literalTypes[Primitives.OperationType(expr.op, arg.Item2)];
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
                throw Primitives.InvalidOperation(expr.op, argumentTypes[0].ToString());
            }
            symbolTable.SetContext(context);

            if (Primitives.SymbolToPrimitiveName(expr.op) == "Increment")
            {
                callReturn = true;
            }

            if (expr.internalFunction.parameters[0].modifiers["ref"] && TypeCheckUtils.CannotBeRef(expr.operand))
            {
                throw new Error.AnalyzerError("Invalid Operator Argument", "Cannot assign when non-variable is passed to 'ref' parameter");
            }

            return expr.internalFunction._returnType.type;
        }

        public override Expr.Type VisitCallExpr(Expr.Call expr)
        {
            Expr.Type[] argumentTypes = new Expr.Type[expr.arguments.Count];

            for (int i = 0; i < expr.arguments.Count; i++)
            {
                argumentTypes[i] = expr.arguments[i].Accept(this);
            }

            if (expr.callee.typeName != null)
            {
                symbolTable.SetContext((Expr.Definition)expr.callee.Accept(this));
                symbolTable.SetContext(symbolTable.GetFunction(expr.name.lexeme, argumentTypes));
            }
            else
            {
                symbolTable.SetContext(symbolTable.NearestEnclosingClass(symbolTable.Current));
                if (symbolTable.TryGetFunction(expr.name.lexeme, argumentTypes, out var symbol))
                {
                    symbolTable.SetContext(symbol);
                }
                else
                {
                    symbolTable.SetContext(null);
                    symbolTable.SetContext(symbolTable.GetFunction(expr.name.lexeme, argumentTypes));
                }
            }

            // 
            if (symbolTable.Current.definitionType != Expr.Definition.DefinitionType.Function)
            {
                throw new Exception();
            }

            TypeCheckUtils.ValidateCall(expr, ((Expr.Function)symbolTable.Current));

            expr.internalFunction = ((Expr.Function)symbolTable.Current);

            for (int i = 0; i < expr.internalFunction.Arity; i++)
            {
                if (expr.internalFunction.parameters[i].modifiers["ref"] && TypeCheckUtils.CannotBeRef(expr.arguments[i]))
                {
                    throw new Error.AnalyzerError("Invalid Function Argument", "Cannot assign when non-variable is passed to 'ref' parameter");
                }
            }

            callReturn = true;
            return expr.internalFunction._returnType.type;
        }

        public override Expr.Type VisitClassExpr(Expr.Class expr)
        {
            symbolTable.SetContext(expr);

            Expr.ListAccept(expr.declarations, this);
            Expr.ListAccept(expr.definitions, this);

            symbolTable.UpContext();

            return TypeCheckUtils._voidType;
        }

        public override Expr.Type VisitDeclareExpr(Expr.Declare expr)
        {
            var name = expr.name;

            if (symbolTable.TryGetVariable(name, out _, out _, true))
            {
                throw new Error.AnalyzerError("Double Declaration", $"A variable named '{name.lexeme}' is already declared in this scope");
            }

            if (symbolTable.Current.definitionType == Expr.Definition.DefinitionType.Class)
            {
                expr.classScoped = true;
            }

            Expr.Type assignType = expr.value.Accept(this);

            TypeCheckUtils.MustMatchType(expr.stack.type, assignType);

            symbolTable.Add(name, expr.stack);

            return TypeCheckUtils._voidType;
        }

        public override Expr.Type VisitPrimitiveExpr(Expr.Primitive expr)
        {
            symbolTable.SetContext(expr);

            if (expr.superclass != null)
            {
                expr._Matches =
                    (x) =>
                    {
                        return (x == expr || x == expr.superclass.type);
                    };
            }

            switch (expr.superclass.typeName.Dequeue().lexeme)
            {
                case "INTEGER":
                    expr.superclass.type = TypeCheckUtils.literalTypes[Parser.Literals[0]];
                    break;
                case "FLOATING":
                    expr.superclass.type = TypeCheckUtils.literalTypes[Parser.Literals[1]];
                    break;
                case "STRING":
                    expr.superclass.type = TypeCheckUtils.literalTypes[Parser.Literals[2]];
                    break;
                case "BINARY":
                    expr.superclass.type = TypeCheckUtils.literalTypes[Parser.Literals[3]];
                    break;
                case "HEX":
                    expr.superclass.type = TypeCheckUtils.literalTypes[Parser.Literals[4]];
                    break;
                case "BOOLEAN":
                    expr.superclass.type = TypeCheckUtils.literalTypes[Parser.Literals[5]];
                    break;
                default: 
                    Diagnostics.errors.Push(new Error.ImpossibleError("Invalid primitive superclass"));
                    break;
            }

            Expr.ListAccept(expr.definitions, this);

            symbolTable.UpContext();

            return TypeCheckUtils._voidType;
        }

        public override Expr.Type VisitFunctionExpr(Expr.Function expr)
        {
            symbolTable.SetContext(expr);

            symbolTable.CreateBlock();

            bool instance = !expr.modifiers["static"];

            if (instance)
            {
                symbolTable.Current.size += 8;
            }

            for (int i = 0; i < expr.Arity; i++)
            {
                Expr.Parameter paramExpr = expr.parameters[i];

                if (symbolTable.TryGetVariable(paramExpr.name, out _, out _, true))
                {
                    throw new Error.AnalyzerError("Double Declaration", $"A variable named '{paramExpr.name.lexeme}' is already declared in this scope");
                }
                paramExpr.stack._ref = paramExpr.modifiers["ref"];

                symbolTable.Add(paramExpr.name, paramExpr.stack, i+Convert.ToInt16(instance), expr.Arity);
            }

            foreach (Expr blockExpr in expr.block)
            {
                Expr.Type result = blockExpr.Accept(this);

                if (!Primitives.IsVoidType(result) && !callReturn)
                {
                    throw new Error.AnalyzerError("Expression With Non-Null Return", $"Expression returned with type '{result}'");
                }
                callReturn = false;
            }

            TypeCheckUtils.HandleFunctionReturns(expr, _return);

            symbolTable.RemoveBlock();
            symbolTable.UpContext();

            return TypeCheckUtils._voidType;
        }

        public override Expr.Type VisitIfExpr(Expr.If expr)
        {
            expr.conditionals.ForEach(x => { HandleConditional(x); TypeCheckUtils.TypeCheckConditional(this, _return, x.condition, x.block); });

            if (expr._else != null)
            {
                expr._else.Accept(this);
                TypeCheckUtils.TypeCheckConditional(this, _return, null, expr._else);
            }

            return TypeCheckUtils._voidType;
        }

        public override Expr.Type VisitAssignExpr(Expr.Assign expr)
        {
            Expr.Type assignType = expr.value.Accept(this);

            if (!expr.binary)
            {
                expr.member.Accept(this);
            }
            TypeCheckUtils.MustMatchType(((Expr.Get)expr.member.getters[^1]).data.type, assignType);

            return TypeCheckUtils._voidType;
        }

        public override Expr.Type VisitAssemblyExpr(Expr.Assembly expr)
        {
            foreach (var variable in expr.variables)
            {
                variable.Accept(this);
            }
            if (expr.block.Any(x => x.HasReturn()))
            {
                _return.Add((null, false, null));
            }

            return TypeCheckUtils._voidType;
        }

        public override Expr.Type VisitNewExpr(Expr.New expr)
        {
            using (new SaveContext())
            {
                expr.call.constructor = true;

                expr.call.Accept(this);

                expr.internalClass = (Expr.DataType)expr.call.internalFunction.enclosing;
            }
            return expr.internalClass;
        }

        public override Expr.Type VisitIsExpr(Expr.Is expr)
        {
            expr.left.Accept(this);

            using (new SaveContext())
                expr.value = expr.right.Accept(this) == expr.right.type ? "1" : "0";

            return TypeCheckUtils.literalTypes[Token.TokenType.BOOLEAN];
        }

        public override Expr.Type VisitTypeReferenceExpr(Expr.TypeReference expr)
        {
            return expr.type;
        }

        public override Expr.Type VisitGetReferenceExpr(Expr.GetReference expr)
        {
            using (new SaveContext())
            {
                if (expr.ambiguousCall)
                {
                    foreach (Expr.Getter getter in expr.getters)
                    {
                        symbolTable.SetContext((Expr.Definition)getter.Accept(this));
                    }
                }
                else
                {
                    var get = expr.getters[0] as Expr.Get;
                    if (get != null)
                    {
                        get.data = symbolTable.GetVariable(get.name, out expr.classScoped);
                        symbolTable.SetContext(get.data.type);
                    }

                    for (int i = Convert.ToInt16(get != null); i < expr.getters.Count; i++)
                    {
                        symbolTable.SetContext((Expr.Definition)expr.getters[i].Accept(this));
                    }
                }
                return symbolTable.Current;
            }
        }

        public override Expr.Type VisitGetExpr(Expr.Get expr) => (expr.data = symbolTable.GetVariable(expr.name)).type;

        public override Expr.Type VisitLogicalExpr(Expr.Logical expr)
        {
            Expr.Type[] argumentTypes =
            {
                expr.left.Accept(this),
                expr.right.Accept(this)
            };

            TypeCheckUtils.MustMatchType(argumentTypes[0], TypeCheckUtils.literalTypes[Token.TokenType.BOOLEAN]);
            TypeCheckUtils.MustMatchType(argumentTypes[1], TypeCheckUtils.literalTypes[Token.TokenType.BOOLEAN]);

            return TypeCheckUtils.literalTypes[Token.TokenType.BOOLEAN];
        }
        
        public override Expr.Type VisitGroupingExpr(Expr.Grouping expr)
        {
            return expr.expression.Accept(this);
        }

        public override Expr.Type VisitWhileExpr(Expr.While expr)
        {
            TypeCheckUtils.TypeCheckConditional(this, _return, expr.conditional.condition, expr.conditional.block);

            return TypeCheckUtils._voidType;
        }

        public override Expr.Type VisitForExpr(Expr.For expr)
        {
            var result = expr.initExpr.Accept(this);
            if (!Primitives.IsVoidType(result) && !callReturn)
            {
                throw new Error.AnalyzerError("Expression With Non-Null Return", $"Expression returned with type '{result}'");
            }
            callReturn = false;

            result = expr.updateExpr.Accept(this);
            if (!Primitives.IsVoidType(result) && !callReturn)
            {
                throw new Error.AnalyzerError("Expression With Non-Null Return", $"Expression returned with type '{result}'");
            }
            callReturn = false;

            TypeCheckUtils.TypeCheckConditional(this, _return, expr.conditional.condition, expr.conditional.block);

            return TypeCheckUtils._voidType;
        }

        public override Expr.Type VisitLiteralExpr(Expr.Literal expr)
        {
            return TypeCheckUtils.literalTypes[expr.literal.type];
        }

        public override Expr.Type VisitReturnExpr(Expr.Return expr)
        {
            _return.Add((expr._void ? TypeCheckUtils._voidType : expr.value.Accept(this), false, expr));

            return TypeCheckUtils._voidType;
        }

        public override Expr.Type VisitKeywordExpr(Expr.Keyword expr)
        {
            return TypeCheckUtils.keywordTypes[expr.keyword];
        }

        private void HandleConditional(Expr.Conditional expr)
        {
            expr.condition.Accept(this);
            expr.block.Accept(this);
        }
    }
}
