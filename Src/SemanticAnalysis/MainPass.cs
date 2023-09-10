namespace Raze;

internal partial class Analyzer
{
    internal class MainPass : Pass<object?>
    {
        SymbolTable symbolTable = SymbolTableSingleton.SymbolTable;

        public MainPass(List<Expr> expressions) : base(expressions)
        {
        }

        internal override List<Expr> Run()
        {
            foreach (var expr in expressions)
            {
                expr.Accept(this);
            }
            return expressions;
        }

        public override object? VisitBlockExpr(Expr.Block expr)
        {
            symbolTable.CreateBlock();
            
            foreach (Expr blockExpr in expr.block)
            {
                blockExpr.Accept(this);
            }
            
            symbolTable.RemoveBlock();

            return null;
        }

        public override object? VisitBinaryExpr(Expr.Binary expr)
        {
            base.VisitBinaryExpr(expr);

            expr.encSize = symbolTable.Current.size;

            return null;
        }

        public override object VisitUnaryExpr(Expr.Unary expr)
        {
            base.VisitUnaryExpr(expr);

            expr.encSize = symbolTable.Current.size;

            return null;
        }

        public override object? VisitCallExpr(Expr.Call expr)
        {
            for (int i = 0; i < expr.arguments.Count; i++)
            {
                expr.arguments[i].Accept(this);
            }

            expr.encSize = symbolTable.Current.size;

            if (expr.callee.typeName != null)
            {
                using (new SaveContext())
                {
                    expr.callee.Accept(this);
                    expr.funcEnclosing = symbolTable.Current;
                }
            }
            else
            {
                expr.funcEnclosing = symbolTable.Current;
            }

            return null;
        }

        public override object? VisitClassExpr(Expr.Class expr)
        {
            symbolTable.SetContext(expr);

            Expr.ListAccept(expr.declarations, this);
            Expr.ListAccept(expr.definitions, this);

            symbolTable.UpContext();

            return null;
        }

        public override object? VisitDeclareExpr(Expr.Declare expr)
        {
            var name = expr.name;

            if (symbolTable.TryGetVariable(name, out _, out _, true))
            {
                throw new Errors.AnalyzerError("Double Declaration", $"A variable named '{name.lexeme}' is already declared in this scope");
            }

            if (symbolTable.Current.definitionType == Expr.Definition.DefinitionType.Class)
            {
                expr.classScoped = true;
            }

            expr.value.Accept(this);

            GetVariableDefinition(expr.typeName, expr.stack);

            symbolTable.Add(name, expr.stack);

            return null;
        }

        public override object? VisitPrimitiveExpr(Expr.Primitive expr)
        {
            symbolTable.SetContext(expr);

            if (expr.superclass.typeName.Count != 0)
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
                    expr.superclass.type = TypeCheckPass.literalTypes[Parser.Literals[0]];
                    break;
                case "FLOATING":
                    expr.superclass.type = TypeCheckPass.literalTypes[Parser.Literals[1]];
                    break;
                case "STRING":
                    expr.superclass.type = TypeCheckPass.literalTypes[Parser.Literals[2]];
                    break;
                case "BINARY":
                    expr.superclass.type = TypeCheckPass.literalTypes[Parser.Literals[3]];
                    break;
                case "HEX":
                    expr.superclass.type = TypeCheckPass.literalTypes[Parser.Literals[4]];
                    break;
                case "BOOLEAN":
                    expr.superclass.type = TypeCheckPass.literalTypes[Parser.Literals[5]];
                    break;
                default: 
                    throw new Errors.ImpossibleError("Invalid primitive superclass");
            }

            Expr.ListAccept(expr.definitions, this);

            symbolTable.UpContext();

            return null;
        }

        public override object? VisitFunctionExpr(Expr.Function expr)
        {
            symbolTable.SetContext(expr);

            if (expr._returnType.typeName.Peek().type != Token.TokenType.RESERVED && expr._returnType.typeName.Peek().lexeme != "void")
            {
                using (new SaveContext())
                {
                    expr._returnType.Accept(this);
                    expr._returnType.type = (Expr.DataType)symbolTable.Current;
                    expr._returnSize = (symbolTable.Current?.definitionType == Expr.Definition.DefinitionType.Primitive) ? ((Expr.Primitive)symbolTable.Current).size : 8;
                }
            }
            else
            {
                expr._returnType.type = TypeCheckPass._voidType;
            }

            symbolTable.CreateBlock();

            bool instance = !expr.modifiers["static"];

            if (instance)
            {
                symbolTable.Current.size += 8;
            }

            for (int i = 0; i < expr.Arity; i++)
            {
                Expr.Parameter paramExpr = expr.parameters[i];

                paramExpr.stack = (expr.modifiers["inline"])? new Expr.StackRegister() : new Expr.StackData();

                GetVariableDefinition(paramExpr.typeName, paramExpr.stack);

                if (symbolTable.TryGetVariable(paramExpr.name, out _, out _, true))
                {
                    throw new Errors.AnalyzerError("Double Declaration", $"A variable named '{paramExpr.name.lexeme}' is already declared in this scope");
                }
                paramExpr.stack._ref = paramExpr.modifiers["ref"];

                symbolTable.Add(paramExpr.name, paramExpr.stack, i+Convert.ToInt16(instance), expr.Arity);
            }

            foreach (Expr blockExpr in expr.block)
            {
                blockExpr.Accept(this);
            }

            symbolTable.RemoveBlock();
            symbolTable.UpContext();

            return null;
        }

        public override object VisitIfExpr(Expr.If expr)
        {
            expr.conditionals.ForEach(x => HandleConditional(x));

            if (expr._else != null)
                expr._else.Accept(this);

            return null;
        }

        public override object? VisitAssignExpr(Expr.Assign expr)
        {
            expr.value.Accept(this);
            if (!expr.binary)
            {
                expr.member.Accept(this);
            }
            return null;
        }

        public override object VisitAssemblyExpr(Expr.Assembly expr)
        {
            foreach (var variable in expr.variables)
            {
                variable.Accept(this);
            }
            return null;
        }

        public override object? VisitNewExpr(Expr.New expr)
        {
            expr.call.constructor = true;

            expr.call.Accept(this);

            expr.internalClass = (Expr.DataType)expr.call.funcEnclosing;

            return null;
        }

        public override object VisitIsExpr(Expr.Is expr)
        {
            expr.left.Accept(this);

            using (new SaveContext())
                expr.right.Accept(this);

            return null;
        }

        public override object VisitTypeReferenceExpr(Expr.TypeReference expr)
        {
            if (expr.typeName.Count != 0)
            {
                HandleTypeNameReference(expr.typeName);
            }

            return null;
        }

        public override object? VisitGetReferenceExpr(Expr.GetReference expr)
        {
            if (expr.ambiguousCall)
            {
                var call = (Expr.Call)expr.getters[0];

                if (call.callee.typeName != null)
                {
                    call.instanceCall = !symbolTable.TryGetClassFullScope(call.callee.typeName.Peek(), out _);

                    if (call.instanceCall)
                    {
                        expr.getters.InsertRange(0, call.callee.ToGetReference().getters);
                        call.callee.typeName = null;
                    }
                    expr.ambiguousCall = false;
                }
            }

            var context = new SaveContext();

            var get = expr.getters[0] as Expr.Get;
            if (get != null)
            {
                get.data = symbolTable.GetVariable(get.name, out expr.classScoped);
                symbolTable.SetContext(get.data.type);
            }

            for (int i = Convert.ToInt16(get != null); i < expr.getters.Count; i++)
            {
                expr.getters[i].Accept(this);
            }
            
            context.Dispose();
            return null;
        }

        public override object? VisitGetExpr(Expr.Get expr)
        {
            var variable = symbolTable.GetVariable(expr.name);

            expr.data = variable;

            symbolTable.SetContext(variable.type);

            return null;
        }

        private void GetVariableDefinition(ExprUtils.QueueList<Token> typeName, Expr.StackData stack)
        {
            using (new SaveContext())
            {
                HandleTypeNameReference(typeName);

                stack.size = (symbolTable.Current.definitionType == Expr.Definition.DefinitionType.Primitive) ? ((Expr.Primitive)symbolTable.Current).size : 8;
                stack.type = symbolTable.Current;
            }
        }

        private void HandleTypeNameReference(ExprUtils.QueueList<Token> typeName)
        {
            symbolTable.SetContext(symbolTable.GetClassFullScope(typeName.Dequeue()));

            while (typeName.Count > 0)
            {
                symbolTable.SetContext(symbolTable.GetDefinition(typeName.Dequeue()));
            }
        }

        private void HandleConditional(Expr.Conditional expr)
        {
            expr.condition.Accept(this);
            expr.block.Accept(this);
        }
    }
}
