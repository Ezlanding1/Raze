namespace Raze;

public partial class Analyzer
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
                    Diagnostics.errors.Push(new Error.AnalyzerError("Expression With Non-Null Return", $"Expression returned with type '{result}'"));
                }
                if (_return.Count != 0)
                {
                    _return.Clear();
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
                    Diagnostics.errors.Push(new Error.AnalyzerError("Expression With Non-Null Return", $"Expression returned with type '{result}'"));
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
                var opType = Primitives.OperationType(expr.op, arg0.Item2, arg1.Item2);
                if (opType != Parser.VoidTokenType)
                {
                    return TypeCheckUtils.literalTypes[opType];
                }
                return TypeCheckUtils.anyType;
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
                if (argumentTypes[0] != TypeCheckUtils.anyType && argumentTypes[1] != TypeCheckUtils.anyType)
                {
                    Diagnostics.errors.Push(Primitives.InvalidOperation(expr.op, argumentTypes[0].ToString(), argumentTypes[1].ToString()));
                }
                expr.internalFunction = symbolTable.FunctionNotFoundDefinition;
            }

            symbolTable.SetContext(context);

            if (expr.internalFunction == symbolTable.FunctionNotFoundDefinition)
            {
                callReturn = true;
                return expr.internalFunction._returnType.type;
            }

            if (expr.internalFunction.parameters[0].modifiers["ref"] && TypeCheckUtils.CannotBeRef(expr.left))
            {
                Diagnostics.errors.Push(new Error.AnalyzerError("Invalid Operator Argument", "Cannot assign when non-variable is passed to 'ref' parameter"));
                expr.internalFunction.parameters[0].modifiers["ref"] = false;
            }
            if (expr.internalFunction.parameters[1].modifiers["ref"] && TypeCheckUtils.CannotBeRef(expr.right))
            {
                Diagnostics.errors.Push(new Error.AnalyzerError("Invalid Operator Argument", "Cannot assign when non-variable is passed to 'ref' parameter"));
                expr.internalFunction.parameters[1].modifiers["ref"] = false;
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
                var opType = Primitives.OperationType(expr.op, arg.Item2);
                if (opType != Parser.VoidTokenType)
                {
                    return TypeCheckUtils.literalTypes[opType];
                }
                return TypeCheckUtils.anyType;
            }

            symbolTable.SetContext((Expr.Definition)argumentTypes[0]);

            expr.internalFunction = symbolTable.GetFunction(Primitives.SymbolToPrimitiveName(expr.op), argumentTypes);

            symbolTable.SetContext(context);

            if (expr.internalFunction == symbolTable.FunctionNotFoundDefinition)
            {
                callReturn = true;
                return expr.internalFunction._returnType.type;
            }

            if (Primitives.SymbolToPrimitiveName(expr.op) == "Increment")
            {
                callReturn = true;
            }

            if (expr.internalFunction.parameters[0].modifiers["ref"] && TypeCheckUtils.CannotBeRef(expr.operand))
            {
                Diagnostics.errors.Push(new Error.AnalyzerError("Invalid Operator Argument", "Cannot assign when non-variable is passed to 'ref' parameter"));
                expr.internalFunction.parameters[0].modifiers["ref"] = false;
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

            if (expr.callee != null)
            {
                symbolTable.SetContext((Expr.Definition)expr.callee.Accept(this));
                symbolTable.SetContext(symbolTable.GetFunction(expr.name.lexeme, argumentTypes));
            }
            else
            {
                var context = symbolTable.Current;

                symbolTable.SetContext(null);
                if (symbolTable.TryGetFunction(expr.name.lexeme, argumentTypes, out var symbol))
                {
                    symbolTable.SetContext(symbol);
                }
                else
                {
                    symbolTable.SetContext(context);
                    symbolTable.SetContext(symbolTable.GetFunction(expr.name.lexeme, argumentTypes));
                }
            }

            if (symbolTable.Current.definitionType != Expr.Definition.DefinitionType.Function)
            {
                Diagnostics.errors.Push(new Error.ImpossibleError("Call references non-function"));
            }

            expr.internalFunction = (Expr.Function)symbolTable.Current;

            if (expr.internalFunction == symbolTable.FunctionNotFoundDefinition)
            {
                callReturn = true;
                return expr.internalFunction._returnType.type;
            }

            TypeCheckUtils.ValidateCall(expr, expr.internalFunction);

            for (int i = 0; i < expr.internalFunction.Arity; i++)
            {
                if (expr.internalFunction.parameters[i].modifiers["ref"] && TypeCheckUtils.CannotBeRef(expr.arguments[i]))
                {
                    Diagnostics.errors.Push(new Error.AnalyzerError("Invalid Function Argument", "Cannot assign when non-variable is passed to 'ref' parameter"));
                    expr.internalFunction.parameters[i].modifiers["ref"] = false;
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
                Diagnostics.errors.Push(new Error.AnalyzerError("Double Declaration", $"A variable named '{name.lexeme}' is already declared in this scope"));
            }

            if (symbolTable.Current == null) return TypeCheckUtils._voidType;

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
                symbolTable.Current.size += (int)InstructionUtils.SYS_SIZE;
            }

            for (int i = 0; i < expr.Arity; i++)
            {
                Expr.Parameter paramExpr = expr.parameters[i];

                if (symbolTable.TryGetVariable(paramExpr.name, out _, out _, true))
                {
                    Diagnostics.errors.Push(new Error.AnalyzerError("Double Declaration", $"A variable named '{paramExpr.name.lexeme}' is already declared in this scope"));
                }
                paramExpr.stack._ref = paramExpr.modifiers["ref"];

                symbolTable.Add(paramExpr.name, paramExpr.stack, i+Convert.ToInt16(instance), expr.Arity);
            }

            expr.block.Accept(this);

            TypeCheckUtils.HandleFunctionReturns(expr, _return);

            symbolTable.RemoveBlock();
            symbolTable.UpContext();

            return TypeCheckUtils._voidType;
        }

        public override Expr.Type VisitIfExpr(Expr.If expr)
        {
            expr.conditionals.ForEach(x => TypeCheckUtils.TypeCheckConditional(this, "if", _return, x.condition, x.block));

            if (expr._else != null)
            {
                TypeCheckUtils.TypeCheckConditional(this, "else", _return, null, expr._else);
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
            TypeCheckUtils.MustMatchType(expr.member.GetLastData().type, assignType);

            return TypeCheckUtils._voidType;
        }

        public override Expr.Type VisitAssemblyExpr(Expr.Assembly expr)
        {
            foreach (var variable in expr.variables)
            {
                variable.Item2.Accept(this);
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

            return TypeCheckUtils.literalTypes[Parser.LiteralTokenType.Boolean];
        }

        public override Expr.Type VisitTypeReferenceExpr(Expr.TypeReference expr)
        {
            return expr.type;
        }

        public override Expr.Type VisitAmbiguousGetReferenceExpr(Expr.AmbiguousGetReference expr)
        {
            if (expr.instanceCall)
            {
                using (new SaveContext())
                {
                    expr.datas[0] = symbolTable.GetVariable(expr.typeName.Dequeue(), out expr.classScoped);
                    symbolTable.SetContext(expr.datas[0].type);

                    for (int i = 1; i < expr.datas.Length; i++)
                    {
                        expr.datas[i] = symbolTable.GetVariable(expr.typeName.Dequeue());
                        symbolTable.SetContext(expr.datas[i].type);
                    }
                    return expr.datas[^1].type;
                }
            }
            else
            {
                using (new SaveContext())
                {
                    InitialPass.HandleTypeNameReference(expr.typeName);
                    return symbolTable.Current;
                }
            }
        }

        public override Expr.Type VisitInstanceGetReferenceExpr(Expr.InstanceGetReference expr)
        {
            using (new SaveContext())
            {
                foreach (Expr.Getter getter in expr.getters)
                {
                    symbolTable.SetContext((Expr.Definition)getter.Accept(this));
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

            TypeCheckUtils.MustMatchType(argumentTypes[0], TypeCheckUtils.literalTypes[Parser.LiteralTokenType.Boolean]);
            TypeCheckUtils.MustMatchType(argumentTypes[1], TypeCheckUtils.literalTypes[Parser.LiteralTokenType.Boolean]);

            return TypeCheckUtils.literalTypes[Parser.LiteralTokenType.Boolean];
        }

        public override Expr.Type VisitGroupingExpr(Expr.Grouping expr)
        {
            return expr.expression.Accept(this);
        }

        public override Expr.Type VisitWhileExpr(Expr.While expr)
        {
            TypeCheckUtils.TypeCheckConditional(this, "while", _return, expr.conditional.condition, expr.conditional.block);

            return TypeCheckUtils._voidType;
        }

        public override Expr.Type VisitForExpr(Expr.For expr)
        {
            var result = expr.initExpr.Accept(this);
            if (!Primitives.IsVoidType(result) && !callReturn)
            {
                Diagnostics.errors.Push(new Error.AnalyzerError("Expression With Non-Null Return", $"Expression returned with type '{result}'"));
            }
            callReturn = false;

            result = expr.updateExpr.Accept(this);
            if (!Primitives.IsVoidType(result) && !callReturn)
            {
                Diagnostics.errors.Push(new Error.AnalyzerError("Expression With Non-Null Return", $"Expression returned with type '{result}'"));
            }
            callReturn = false;

            TypeCheckUtils.TypeCheckConditional(this, "for", _return, expr.conditional.condition, expr.conditional.block);

            return TypeCheckUtils._voidType;
        }

        public override Expr.Type VisitLiteralExpr(Expr.Literal expr)
        {
            return TypeCheckUtils.literalTypes[expr.literal.type];
        }

        public override Expr.Type VisitReturnExpr(Expr.Return expr)
        {
            _return.Add((expr.value.Accept(this), false, expr));

            return TypeCheckUtils._voidType;
        }

        public override Expr.Type VisitKeywordExpr(Expr.Keyword expr)
        {
            return TypeCheckUtils.keywordTypes[expr.keyword];
        }

        public override Expr.Type VisitNoOpExpr(Expr.NoOp expr)
        {
            return TypeCheckUtils.anyType;
        }
    }
}
