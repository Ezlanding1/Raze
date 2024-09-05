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
                (!arg0.Item1 && ResolveCallReferenceUsingCallee(name, (Expr.DataType)argumentTypes[0], argumentTypes, true)) ||
                (!arg1.Item1 && ResolveCallReferenceUsingCallee(name, (Expr.DataType)argumentTypes[1], argumentTypes.Reverse().ToArray(), true));

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
            TypeCheckUtils.ValidateFunctionParameterModifiers(expr);
            symbolTable.SetContext(context);
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

            if (!ResolveCallReferenceUsingCallee(name, (Expr.DataType)argumentTypes[0], argumentTypes, true))
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
            TypeCheckUtils.ValidateFunctionParameterModifiers(expr);
            callReturn = new List<string>() { "Increment", "Decrement" }.Contains(name);
            symbolTable.SetContext(context);
            return expr.internalFunction._returnType.type;
        }

        public override Expr.Type VisitCallExpr(Expr.Call expr)
        {
            Expr.Type[] argumentTypes = expr.arguments.Select(arg => arg.Accept(this)).ToArray();

            if (expr.callee != null)
            {
                var callee = (Expr.DataType)expr.callee.Accept(this);
                
                if (!ResolveCallReferenceUsingCallee(expr.name.lexeme, callee, argumentTypes, false))
                {
                    symbolTable.SetContext(callee);
                    symbolTable.SetContext(symbolTable.FunctionSearchFail(expr.name.lexeme, argumentTypes));
                }
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

            expr.internalFunction = GetResolvedFunction();

            if (expr.internalFunction == symbolTable.FunctionNotFoundDefinition)
            {
                callReturn = true;
                return expr.internalFunction._returnType.type;
            }

            TypeCheckUtils.ValidateCall(expr, expr.internalFunction);

            callReturn = true;
            return expr.internalFunction._returnType.type;
        }

        private bool ResolveCallReferenceUsingCallee(string name, Expr.DataType callee, Expr.Type[] argumentTypes, bool invokableIsOp)
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
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.DoubleDeclaration, "variable", name.lexeme));
            }

            if (symbolTable.Current == null) return TypeCheckUtils._voidType;

            if (symbolTable.Current is Expr.Class)
            {
                expr.classScoped = true;
            }

            Expr.Type assignType = expr.value?.Accept(this);

            if (assignType != null)
            {
                TypeCheckUtils.MustMatchType(expr.stack.type, assignType, expr.stack._ref, expr.value, true, false);
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

                if (symbolTable.TryGetVariable(paramExpr.name, out _, out _, true))
                {
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.DoubleDeclaration, "variable", paramExpr.name.lexeme));
                }
                paramExpr.stack._ref = paramExpr.modifiers["ref"];

                symbolTable.AddParameter(paramExpr.name, paramExpr.stack);
            }

            if (!expr.Abstract)
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
                var states = Enumerable.Repeat(true, symbolTable.frameData.Count + 1).ToList();

                expr.conditionals.ForEach(x =>
                {
                    TypeCheckUtils.TypeCheckConditional(this, "if", x.condition, x.block);
                    symbolTable.ResolveStates(states);
                });
                TypeCheckUtils.TypeCheckConditional(this, "else", null, expr._else);
                symbolTable.ResolveStates(states);

                symbolTable.SetFrameDataStates(Enumerable.Range(0, symbolTable.frameData.Count + 1).Select(i => sStates[i] | states[i]));
            }
            else
            {
                TypeCheckUtils.RunConditionals(this, "if", expr.conditionals);
            }

            return TypeCheckUtils._voidType;
        }

        public override Expr.Type VisitAssignExpr(Expr.Assign expr)
        {
            string memberName = expr.member.GetLastName().lexeme;
            string? valueName = (expr.value as Expr.GetReference)?.GetLastName().lexeme;

            Expr.Type assignType = expr.value.Accept(this);

            if (!expr.binary)
            {
                assigns = true;
                expr.member.Accept(this);
                assigns = false;
            }

            bool initialized = true;
            if (expr.member.GetLastData() != null && symbolTable.frameData.TryGetValue(expr.member.GetLastData(), out var fd))
            {
                initialized = fd.initialized;
                fd.initialized = true;
            }

            TypeCheckUtils.MustMatchType(expr.member.GetLastType(), assignType, TypeCheckUtils.IsRefVariable(expr.member), expr.value, !initialized, false);

            if (symbolTable.Current is Expr.Function function && 
                    TypeCheckUtils.IsVariableWithRefModifier(expr.value) && 
                    (!symbolTable.IsLocallyScoped(memberName) || symbolTable.VariableIsParameter(function, expr.member.GetLastData(), out _)))
            {
                if (valueName != null && symbolTable.IsLocallyScoped(valueName) && !symbolTable.VariableIsParameter(function, ((Expr.GetReference)expr.value).GetLastData(), out _))
                {
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.DanglingPointerCreated_Assigned, memberName));
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
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InstanceOfTraitCreated));
            }
            return expr.internalClass;
        }

        public override Expr.Type VisitIsExpr(Expr.Is expr)
        {
            var leftType = expr.left.Accept(this);
            var rightType = expr.right.Accept(this);

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

            return TypeCheckUtils.literalTypes[Parser.LiteralTokenType.Boolean];
        }

        public override Expr.Type VisitAsExpr(Expr.As expr)
        {
            expr._is.Accept(this);
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
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InvalidRefModifier, "method"));
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

            TypeCheckUtils.MustMatchType(argumentTypes[0], TypeCheckUtils.literalTypes[Parser.LiteralTokenType.Boolean], false, expr.left, false, false);
            TypeCheckUtils.MustMatchType(argumentTypes[1], TypeCheckUtils.literalTypes[Parser.LiteralTokenType.Boolean], false, expr.right, false, false);

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
            string? name = (expr.value as Expr.GetReference)?.GetLastName().lexeme;
            Expr.Type type = expr.value.Accept(this);

            if (symbolTable.Current is Expr.Function function && function.refReturn)
            {
                if (name != null && symbolTable.IsLocallyScoped(name))
                {
                    Expr.GetReference getRef = (Expr.GetReference)expr.value;
                    if (!getRef.GetLastData()._ref || !symbolTable.VariableIsParameter(function, getRef.GetLastData(), out _))
                    {
                        Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.DanglingPointerCreated_Returned, name));
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
            TypeCheckUtils.MustMatchType(expr.size.Accept(this), TypeCheckUtils.literalTypes[Parser.LiteralTokenType.UnsignedInteger], false, false, false, false);
            return TypeCheckUtils.heapallocType.Value;
        }

        public override Expr.Type VisitNoOpExpr(Expr.NoOp expr)
        {
            return TypeCheckUtils.anyType;
        }

        private void FindVirtualFunctionForOverride(Expr.Function function)
        {
            if (function.modifiers["static"])
            {
                if (function.modifiers["override"])
                {
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InvalidFunctionModifierPair, "static", "override"));
                    function.modifiers["override"] = false;
                }
                if (function.modifiers["virtual"])
                {
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InvalidFunctionModifierPair, "static", "virtual"));
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
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InvalidOverrideModifier, function.ToString()));
            }
        }

        public bool CanBeReturned(Expr.Type type) => !Primitives.IsVoidType(type) && type != TypeCheckUtils.anyType && !callReturn;
    }
}
