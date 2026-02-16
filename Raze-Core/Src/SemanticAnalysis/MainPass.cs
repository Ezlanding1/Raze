namespace Raze;

public partial class Analyzer
{
    internal class MainPass : Pass<Expr.Type>
    {
        SymbolTable symbolTable = SymbolTableSingleton.SymbolTable;
        bool callReturn;
        bool assigns;

        public MainPass(List<Expr> expressions) : base(expressions)
        {
        }

        internal override void Run()
        {
            foreach (var expr in expressions)
            {
                Expr.Type result = expr.Accept(this);

                if (CanBeReturned(result))
                {
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.ExpressionWithNonNullReturn, result));
                }
                symbolTable.returnFrameData.initialized = false;
                symbolTable.returnFrameData.initializedOnAnyBranch = false;
                callReturn = false;
            }
        }

        public override Expr.Type VisitBlockExpr(Expr.Block expr)
        {
            symbolTable.CreateBlock();

            foreach (var blockExpr in expr.block)
            {
                Expr.Type result = blockExpr.Accept(this);

                if (CanBeReturned(result))
                {
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.ExpressionWithNonNullReturn, result));
                }
                callReturn = false;
            }

            symbolTable.RemoveBlock();

            return TypeCheckUtils._voidType;
        }

        public override Expr.Type VisitBinaryExpr(Expr.Binary expr)
        {
            var context = symbolTable.Current;
            string name = Primitives.SymbolToPrimitiveName(expr.op);
            Expr.Type[] argumentTypes = [expr.left.Accept(this), expr.right.Accept(this)];
            
            var (arg0, arg1) = (Primitives.IsLiteralTypeOrVoid(argumentTypes[0]), Primitives.IsLiteralTypeOrVoid(argumentTypes[1]));
            if (arg0.Item1 && arg1.Item1)
            {
                var opType = Primitives.OperationType(expr.op, arg0.Item2, arg1.Item2);
                return (opType != Parser.VoidTokenType) ? 
                    TypeCheckUtils.literalTypes[opType] : 
                    TypeCheckUtils.anyType;
            }

            bool found =
                (!arg0.Item1 &&
                ResolveCallReferenceUsingCallee(name, expr.op.location, (Expr.DataType)argumentTypes[0], argumentTypes, true)) ||
                (!arg1.Item1 &&
                ResolveCallReferenceUsingCallee(name, expr.op.location, (Expr.DataType)argumentTypes[1], argumentTypes, true));

            if (!found)
            {
                var reversedArgTypes = argumentTypes.Reverse().ToArray();

                found =
                    (!arg1.Item1 &&
                    ResolveCallReferenceUsingCallee(name, expr.op.location, (Expr.DataType)argumentTypes[1], reversedArgTypes, true)) ||
                    (!arg0.Item1 &&
                    ResolveCallReferenceUsingCallee(name, expr.op.location, (Expr.DataType)argumentTypes[0], reversedArgTypes, true));

                if (found)
                    (expr.left, expr.right) = (expr.right, expr.left);
            }

            if (!found)
            {
                if (!argumentTypes.Any(x => x == TypeCheckUtils.anyType))
                {
                    Diagnostics.Report(Primitives.InvalidOperation(expr.op, argumentTypes[0].ToString(), argumentTypes[1].ToString()));
                }
                expr.internalFunction = symbolTable.FunctionNotFoundDefinition;
                callReturn = true;
                symbolTable.SetContext(context);
                return expr.internalFunction._returnType.type;
            }

            expr.internalFunction = GetResolvedFunction();
            symbolTable.SetContext(context);
            TypeCheckUtils.ValidateFunctionParameterModifiers(expr);
            return expr.internalFunction._returnType.type;
        }

        public override Expr.Type VisitUnaryExpr(Expr.Unary expr)
        {
            var context = symbolTable.Current;
            string name = Primitives.SymbolToPrimitiveName(expr.op);
            Expr.Type[] argumentTypes = [expr.operand.Accept(this)];

            var arg = Primitives.IsLiteralTypeOrVoid(argumentTypes[0]);
            if (arg.Item1)
            {
                var opType = Primitives.OperationType(expr.op, arg.Item2);
                return (opType != Parser.VoidTokenType)? 
                    TypeCheckUtils.literalTypes[opType] : 
                    TypeCheckUtils.anyType;
            }

            if (!ResolveCallReferenceUsingCallee(name, expr.op.location, (Expr.DataType)argumentTypes[0], argumentTypes, true))
            {
                if (argumentTypes[0] != TypeCheckUtils.anyType)
                {
                    Diagnostics.Report(Primitives.InvalidOperation(expr.op, argumentTypes[0].ToString()));
                }
                expr.internalFunction = symbolTable.FunctionNotFoundDefinition;
                callReturn = true;
                symbolTable.SetContext(context);
                return expr.internalFunction._returnType.type;
            }

            expr.internalFunction = GetResolvedFunction();
            callReturn = new List<string>() { "Increment", "Decrement" }.Contains(name);
            symbolTable.SetContext(context);
            TypeCheckUtils.ValidateFunctionParameterModifiers(expr);
            return expr.internalFunction._returnType.type;
        }

        public override Expr.Type VisitCallExpr(Expr.Call expr)
        {
            var context = symbolTable.Current;
            Expr.Type[] argumentTypes = expr.arguments.Select(arg => arg.Accept(this)).ToArray();

            if (expr.callee != null)
            {
                var callee = (Expr.DataType)expr.callee.Accept(this);
                
                if (!ResolveCallReferenceUsingCallee(expr.name.lexeme, expr.name.location, callee, argumentTypes, false))
                {
                    symbolTable.SetContext(callee);
                    symbolTable.SetContext(symbolTable.FunctionSearchFail(expr.name, argumentTypes));
                }
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
                    symbolTable.SetContext(context);
                    symbolTable.SetContext(symbolTable.GetFunction(expr.name, argumentTypes));
                }
            }

            expr.internalFunction = GetResolvedFunction();

            callReturn = true;

            if (expr.internalFunction == symbolTable.FunctionNotFoundDefinition)
            {
                symbolTable.SetContext(context);
                return expr.internalFunction._returnType.type;
            }

            symbolTable.SetContext(context);
            TypeCheckUtils.ValidateCall(expr, expr.internalFunction);
            return expr.internalFunction._returnType.type;
        }

        private bool ResolveCallReferenceUsingCallee(string name, Location location, Expr.DataType callee, Expr.Type[] argumentTypes, bool invokableIsOp)
        {
            var currentCallee = callee;
            do
            {
                symbolTable.SetContext(currentCallee);

                if (symbolTable.TryGetFunction(name, argumentTypes, out var symbol))
                {
                    if (callee != symbolTable.Current && (!invokableIsOp && symbol.modifiers["static"]))
                    {
                        symbolTable.SetContext(callee);
                        Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(
                            Diagnostic.DiagnosticName.UndefinedReference_Suggestion,
                            location,
                            "function",
                            callee + "." + Expr.Call.CallNameToString(name, argumentTypes),
                            symbol
                        ));
                    }
                    symbolTable.SetContext(symbol);
                    return true;
                }
            }
            while ((currentCallee = currentCallee.SuperclassType as Expr.DataType) != null);
            return false;
        }

        private Expr.Function GetResolvedFunction()
        {
            if (symbolTable.Current is not Expr.Function function)
            {
                throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("Call references non-function"));
            }
            return function;
        }

        public override Expr.Type VisitClassExpr(Expr.Class expr)
        {
            symbolTable.SetContext(expr);

            HandleTraitSuperclass(expr);

            Expr.ListAccept(expr.declarations, this);
            Expr.ListAccept(expr.definitions, this);

            symbolTable.UpContext();

            return TypeCheckUtils._voidType;
        }

        public override Expr.Type VisitDeclareExpr(Expr.Declare expr)
        {
            var name = expr.name;

            if (symbolTable.Current == null) return TypeCheckUtils._voidType;

            if (symbolTable.Current is Expr.Class)
            {
                expr.classScoped = true;
            }

            Expr.Type assignType = expr.value?.Accept(this);

            if (assignType != null)
            {
                TypeCheckUtils.MustMatchType(expr.stack.type, assignType, expr.stack._ref, expr.stack._readonly, expr.value, true, false);
            }
            symbolTable.AddVariable(name, expr.stack, assignType != null);

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

            FindVirtualFunctionForOverride(expr);

            for (int i = 0; i < expr.Arity; i++)
            {
                Expr.Parameter paramExpr = expr.parameters[i];

                paramExpr.stack._ref = paramExpr.modifiers["ref"];
                paramExpr.stack._readonly = paramExpr.modifiers["readonly"];

                symbolTable.AddVariable(paramExpr.name, paramExpr.stack, true);
            }

            if (!expr.Abstract && !expr.modifiers["extern"])
            {
                expr.block.Accept(this);
                TypeCheckUtils.HandleFunctionReturns(expr);
            }

            symbolTable.RemoveBlock();
            symbolTable.UpContext();

            return TypeCheckUtils._voidType;
        }

        public override Expr.Type VisitIfExpr(Expr.If expr)
        {
            if (expr._else != null)
            {
                var sStates = symbolTable.GetFrameData().ToList();
                var states = Enumerable.Repeat(true, sStates.Count).ToList();

                expr.conditionals.ForEach(x =>
                {
                    TypeCheckUtils.TypeCheckConditional(this, "if", x.condition, x.block);
                    symbolTable.ResolveStates(states);
                });
                TypeCheckUtils.TypeCheckConditional(this, "else", null, expr._else);
                symbolTable.ResolveStates(states);

                symbolTable.SetFrameDataStates(Enumerable.Range(0, sStates.Count).Select(i => sStates[i] | states[i]));
            }
            else
            {
                TypeCheckUtils.RunConditionals(this, "if", expr.conditionals);
            }

            return TypeCheckUtils._voidType;
        }

        public override Expr.Type VisitAssignExpr(Expr.Assign expr)
        {
            Token memberName = expr.member.GetLastName();
            string? valueName = (expr.value as Expr.GetReference)?.GetLastName().lexeme;

            Expr.Type assignType = expr.value.Accept(this);

            if (!expr.binary)
            {
                assigns = true;
                expr.member.Accept(this);
                assigns = false;
            }

            if (expr.member.GetLastData()?._readonly == true && !(symbolTable.Current is Expr.Function { constructor: true }))
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(
                    Diagnostic.DiagnosticName.ReadonlyFieldModified, 
                    memberName.location, 
                    memberName.lexeme
                ));
            }

            bool initialized = true;
            if (expr.member.GetLastData() != null && symbolTable.frameData.TryGetValue(expr.member.GetLastData(), out var fd))
            {
                initialized = fd.initialized;
                fd.initialized = true;
            }

            var (_ref, _readonly) = TypeCheckUtils.GetVariableModifiers(expr.member);
            TypeCheckUtils.MustMatchType(expr.member.GetLastType(), assignType, _ref, _readonly, expr.value, !initialized, false);

            if (symbolTable.Current is Expr.Function function && 
                    TypeCheckUtils.IsVariableWithRefModifier(expr.value) && 
                    (!symbolTable.IsLocallyScoped(memberName.lexeme) || symbolTable.VariableIsParameter(function, expr.member.GetLastData(), out _)))
            {
                if (valueName != null && symbolTable.IsLocallyScoped(valueName) && !symbolTable.VariableIsParameter(function, ((Expr.GetReference)expr.value).GetLastData(), out _))
                {
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.DanglingPointerCreated_Assigned, memberName.location, memberName.lexeme));
                }
            }

            return TypeCheckUtils._voidType;
        }

        public override Expr.Type VisitInlineAssemblyExpr(Expr.InlineAssembly expr)
        {
            foreach (var instruction in expr.instructions)
            {
                if (instruction is Expr.InlineAssembly.Return || instruction is Expr.InlineAssembly.Instruction { _return: true }) 
                {
                    symbolTable.returnFrameData.Initialized(false, null);
                }

                instruction.GetOperands()
                    .Select(x => x.GetVariable())
                    .ToList()
                    .ForEach(x => x?.variable.Accept(this));
            }
            return TypeCheckUtils._voidType;
        }

        public override Expr.Type VisitNewExpr(Expr.New expr)
        {
            using (new SaveContext())
            {
                expr.call.constructor = true;

                expr.call.Accept(this);

                expr.internalClass = expr.call.internalFunction.enclosing as Expr.Class ?? TypeCheckUtils.anyType;
            }
            if (expr.internalClass.trait)
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InstanceOfTraitCreated, expr.call.name.location, []));
            }
            return expr.internalClass;
        }

        private void CheckIsExprMatches(Expr.Is expr, Expr.Type leftType, Expr.Type rightType)
        {
            if ((expr.value = leftType.Matches(rightType)) != true)
            {
                if (rightType.Matches(leftType))
                {
                    expr.value = null;
                    ((Expr.Class)rightType).emitVTable = true;
                    ((Expr.Class)leftType).emitVTable = true;
                }
                else
                {
                    expr.value = false;
                }
            }
        }

        public override Expr.Type VisitIsExpr(Expr.Is expr)
        {
            var leftType = expr.left.Accept(this);
            var rightType = expr.right.Accept(this);

            CheckIsExprMatches(expr, leftType, rightType);

            return TypeCheckUtils.literalTypes[Parser.LiteralTokenType.Boolean];
        }

        public override Expr.Type VisitAsExpr(Expr.As expr)
        {
            var leftType = expr._is.left.Accept(this);
            var rightType = expr._is.right.Accept(this);

            using (new SaveContext())
            {
                symbolTable.SetContext(rightType as Expr.Definition);

                Token name = new(Token.TokenType.IDENTIFIER, "Cast", Location.NoLocation);

                expr.overloadedCast = new Expr.Call(name, [expr._is.left], null);

                if (symbolTable.TryGetFunction(
                    name.lexeme,
                    [TypeCheckUtils.ToDataTypeOrDefault(leftType)],
                    out expr.overloadedCast.internalFunction
                ))
                {
                    return expr.overloadedCast.internalFunction._returnType.type;
                }
                else if (rightType is Expr.Primitive)
                {
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(
                        Diagnostic.DiagnosticName.NoConversionFound, 
                        leftType,
                        rightType
                    ));
                }
            }
                
            CheckIsExprMatches(expr._is, leftType, rightType);
            return expr._is.right.type;
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
                    expr.datas[0] = symbolTable.GetVariable(expr.typeName.Dequeue(), out expr.classScoped, assigns: expr.datas.Length > 1? false : assigns);
                    symbolTable.SetContext(expr.datas[0].type);

                    for (int i = 1; i < expr.datas.Length; i++)
                    {
                        expr.datas[i] = symbolTable.GetVariable(expr.typeName.Dequeue(), assigns);
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
                    symbolTable.SetContext(TypeCheckUtils.ToDataTypeOrDefault(getter.Accept(this)));
                }

                if (expr._ref && expr.IsMethodCall() && !((Expr.Invokable)expr.getters[^1]).internalFunction.refReturn)
                {
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InvalidRefModifier, expr.GetLastName().location, "method"));
                }

                return symbolTable.Current;
            }
        }

        public override Expr.Type VisitGetExpr(Expr.Get expr) => (expr.data = symbolTable.GetVariable(expr.name, assigns)).type;

        public override Expr.Type VisitLogicalExpr(Expr.Logical expr)
        {
            Expr.Type[] argumentTypes =
            {
                expr.left.Accept(this),
                expr.right.Accept(this)
            };

            TypeCheckUtils.MustMatchType(argumentTypes[0], TypeCheckUtils.literalTypes[Parser.LiteralTokenType.Boolean], false, false, expr.left, false, false);
            TypeCheckUtils.MustMatchType(argumentTypes[1], TypeCheckUtils.literalTypes[Parser.LiteralTokenType.Boolean], false, false, expr.right, false, false);

            return TypeCheckUtils.literalTypes[Parser.LiteralTokenType.Boolean];
        }

        public override Expr.Type VisitGroupingExpr(Expr.Grouping expr)
        {
            return expr.type = expr.expression.Accept(this);
        }

        public override Expr.Type VisitWhileExpr(Expr.While expr)
        {
            TypeCheckUtils.RunConditionals(this, "while", [expr.conditional]);
            return TypeCheckUtils._voidType;
        }

        public override Expr.Type VisitForExpr(Expr.For expr)
        {
            symbolTable.CreateBlock();

            var result = expr.initExpr.Accept(this);
            if (CanBeReturned(result))
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.ExpressionWithNonNullReturn, result));
            }
            callReturn = false;

            result = expr.updateExpr.Accept(this);
            if (CanBeReturned(result))
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.ExpressionWithNonNullReturn, result));
            }
            callReturn = false;

            TypeCheckUtils.RunConditionals(this, "for", [expr.conditional]);


            symbolTable.RemoveBlock();
            return TypeCheckUtils._voidType;
        }

        public override Expr.Type VisitLiteralExpr(Expr.Literal expr)
        {
            return TypeCheckUtils.literalTypes[expr.literal.type];
        }

        public override Expr.Type VisitReturnExpr(Expr.Return expr)
        {
            Token? name = (expr.value as Expr.GetReference)?.GetLastName();
            Expr.Type type = expr.value.Accept(this);

            if (symbolTable.Current is Expr.Function function && function.refReturn)
            {
                if (name != null && symbolTable.IsLocallyScoped(name.lexeme))
                {
                    Expr.GetReference getRef = (Expr.GetReference)expr.value;
                    if (!getRef.GetLastData()._ref || !symbolTable.VariableIsParameter(function, getRef.GetLastData(), out _))
                    {
                        Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.DanglingPointerCreated_Returned, name.location, name.lexeme));
                    }
                }
            }
            

            symbolTable.returnFrameData.Initialized(TypeCheckUtils.IsVariableWithRefModifier(expr.value), type);
            return TypeCheckUtils._voidType;
        }

        public override Expr.Type VisitKeywordExpr(Expr.Keyword expr)
        {
            return TypeCheckUtils.keywordTypes[expr.keyword];
        }

        public override Expr.Type VisitImportExpr(Expr.Import expr) =>
            TypeCheckUtils._voidType;


        public override Expr.Type VisitHeapAllocExpr(Expr.HeapAlloc expr)
        {
            TypeCheckUtils.MustMatchType((expr.size.Accept(this), TypeCheckUtils.literalTypes[Parser.LiteralTokenType.UnsignedInteger]), (false, false), (false, false), false, false);
            return TypeCheckUtils.heapallocType.Value;
        }

        public override Expr.Type VisitNoOpExpr(Expr.NoOp expr)
        {
            return TypeCheckUtils.anyType;
        }

        private void HandleTraitSuperclass(Expr.Class _class)
        {
            var superclass = _class.SuperclassType as Expr.Class;
            if (superclass?.trait == true)
            {
                foreach (var abstractFunction in superclass.definitions.Where(x => x is Expr.Function func && func.Abstract).Cast<Expr.Function>())
                {
                    if (!symbolTable.TryGetFunction(abstractFunction.name.lexeme, abstractFunction.parameters.Select(x => x.stack.type).ToArray(), out var virtualFunc))
                    {
                        Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.ClassDoesNotOverrideAbstractFunction, _class.name.lexeme, superclass.name.lexeme, abstractFunction.ToString()));
                    }
                }
            }
        }

        private void FindVirtualFunctionForOverride(Expr.Function function)
        {
            if (function.modifiers["static"])
            {
                if (function.modifiers["override"])
                {
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InvalidFunctionModifierPair, function.name.location, "static", "override"));
                    function.modifiers["override"] = false;
                }
                if (function.modifiers["virtual"])
                {
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InvalidFunctionModifierPair, function.name.location, "static", "virtual"));
                    function.modifiers["virtual"] = false;
                }
                return;
            }

            function.modifiers["virtual"] = false;
            using (new SaveContext())
            {
                var superclass = symbolTable.NearestEnclosingClass()?.SuperclassType as Expr.DataType;
                while (superclass != null)
                {
                    symbolTable.SetContext(superclass);
                    if (symbolTable.TryGetFunction(function.name.lexeme, function.parameters.Select(x => x.stack.type).ToArray(), out var virtualFunc))
                    {
                        function.modifiers["override"] = true;
                        virtualFunc.modifiers["virtual"] = true;
                        if (!function._returnType.type.Matches(virtualFunc._returnType.type))
                        {
                            Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(
                                Diagnostic.DiagnosticName.TypeMismatch_OverridenMethod,
                                function.name.location,
                                function,
                                virtualFunc._returnType.type
                            ));
                        }
                        return;
                    }
                    superclass = superclass.SuperclassType as Expr.DataType;
                }
            }
            if (function.modifiers["override"])
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InvalidOverrideModifier, function.name.location, function.ToString()));
            }
        }

        public bool CanBeReturned(Expr.Type type) => !Primitives.IsVoidType(type) && type != TypeCheckUtils.anyType && !callReturn;
    }
}
