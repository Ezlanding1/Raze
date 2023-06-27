namespace Raze
{
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

            public override object? visitBlockExpr(Expr.Block expr)
            {
                symbolTable.CreateBlock();
                
                foreach (Expr blockExpr in expr.block)
                {
                    blockExpr.Accept(this);
                }
                
                symbolTable.RemoveBlock();

                return null;
            }

            public override object? visitBinaryExpr(Expr.Binary expr)
            {
                base.visitBinaryExpr(expr);

                expr.encSize = symbolTable.Current.size;

                return null;
            }

            public override object visitUnaryExpr(Expr.Unary expr)
            {
                base.visitUnaryExpr(expr);

                expr.encSize = symbolTable.Current.size;

                return null;
            }

            public override object? visitCallExpr(Expr.Call expr)
            {
                for (int i = 0; i < expr.arguments.Count; i++)
                {
                    expr.arguments[i].Accept(this);
                }

                var context = symbolTable.Current;

                expr.encSize = symbolTable.Current.size;

                if (expr.callee != null)
                {
                    expr.instanceCall = symbolTable.TryGetVariable(expr.callee.Peek(), out var topSymbol_I, out _);
                    if (expr.instanceCall)
                    {
                        this.visitGetReferenceExpr(expr);
                    }
                    else
                    {
                        this.visitTypeReferenceExpr(expr);
                    }
                    expr.funcEnclosing = symbolTable.Current;
                }
                else
                {
                    expr.funcEnclosing = symbolTable.Current;
                }

                symbolTable.SetContext(context);
                return null;
            }

            public override object? visitClassExpr(Expr.Class expr)
            {
                symbolTable.SetContext(expr);

                Expr.ListAccept(expr.declarations, this);
                Expr.ListAccept(expr.definitions, this);

                symbolTable.UpContext();

                return null;
            }

            public override object? visitDeclareExpr(Expr.Declare expr)
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

            public override object? visitPrimitiveExpr(Expr.Primitive expr)
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

            public override object? visitFunctionExpr(Expr.Function expr)
            {
                symbolTable.SetContext(expr);

                if (expr._returnType.typeName.Peek().type != Token.TokenType.RESERVED && expr._returnType.typeName.Peek().lexeme != "void")
                {
                    var context = symbolTable.Current;
                    expr._returnType.Accept(this);
                    expr._returnType.type = (Expr.DataType)symbolTable.Current;
                    expr._returnSize = (symbolTable.Current?.definitionType == Expr.Definition.DefinitionType.Primitive) ? ((Expr.Primitive)symbolTable.Current).size : 8;
                    symbolTable.SetContext(context);
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

                for (int i = 0; i < expr.arity; i++)
                {
                    Expr.Parameter paramExpr = expr.parameters[i];

                    paramExpr.stack = (expr.modifiers["inline"])? new Expr.StackRegister() : new Expr.StackData();

                    GetVariableDefinition(paramExpr.typeName, paramExpr.stack);

                    if (symbolTable.TryGetVariable(paramExpr.name, out _, out _, true))
                    {
                        throw new Errors.AnalyzerError("Double Declaration", $"A variable named '{paramExpr.name.lexeme}' is already declared in this scope");
                    }
                    paramExpr.stack._ref = paramExpr.modifiers["ref"];

                    symbolTable.Add(paramExpr.name, paramExpr.stack, i+Convert.ToInt16(instance), expr.arity);
                }

                foreach (Expr blockExpr in expr.block)
                {
                    blockExpr.Accept(this);
                }

                symbolTable.RemoveBlock();
                symbolTable.UpContext();

                return null;
            }

            public override object? visitVariableExpr(Expr.Variable expr)
            {
                var context = symbolTable.Current;

                bool isClassScoped = false;

                while (expr.typeName.Count > 1)
                {
                    if (symbolTable.Current.definitionType == Expr.Definition.DefinitionType.Primitive)
                    {
                        throw new Errors.AnalyzerError("Primitive Field Access", "Primitive classes cannot contain fields");
                    }

                    Expr.StackData variable;

                    if (expr.offsets.Length == expr.typeName.Count)
                    {
                        variable = symbolTable.GetVariable(expr.typeName.Dequeue(), out isClassScoped);
                    }
                    else 
                    {
                        variable = symbolTable.GetVariable(expr.typeName.Dequeue());
                    }
                            

                    expr.offsets[expr.typeName.Count] = variable;

                    symbolTable.SetContext(variable.type);

                    if (expr.typeName.Count == 0)
                    {
                        expr.type = variable.type;
                    }
                }
                
                if (symbolTable.TryGetVariable(expr.typeName.Peek(), out var symbol, out bool isClassScopedVar))
                {
                    expr.typeName.Dequeue();

                    expr.stack = symbol;
                    expr.classScoped = (expr.offsets.Length == 1) ? isClassScopedVar : isClassScoped;
                }
                else
                {
                    throw new Errors.AnalyzerError("Undefined Reference", $"The variable '{expr.typeName.Dequeue().lexeme}' does not exist in the current context");
                }

                symbolTable.SetContext(context);

                return null;
            }

            public override object visitIfExpr(Expr.If expr)
            {
                HandleConditional(expr.conditional);

                expr.ElseIfs.ForEach(x => HandleConditional(x.conditional));

                if (expr._else != null)
                    HandleConditional(expr._else.conditional);

                return null;
            }


            //public override object? visitDefineExpr(Expr.Define expr)
            //{
            //    //symbolTable.Add(expr);
            //    //return null;
            //}

            public override object? visitAssignExpr(Expr.Assign expr)
            {
                expr.value.Accept(this);
                if (!expr.binary)
                {
                    expr.member.Accept(this);
                }
                return null;
            }

            public override object visitAssemblyExpr(Expr.Assembly expr)
            {
                foreach (var variable in expr.variables)
                {
                    variable.Accept(this);
                }
                return null;
            }

            public override object? visitNewExpr(Expr.New expr)
            {
                expr.call.constructor = true;

                expr.call.Accept(this);

                expr.internalClass = (Expr.DataType)expr.call.funcEnclosing;

                return null;
            }

            public override object visitIsExpr(Expr.Is expr)
            {
                expr.left.Accept(this);

                var context = symbolTable.Current;

                expr.right.Accept(this);

                symbolTable.SetContext(context);

                return null;
            }

            public override object visitTypeReferenceExpr(Expr.TypeReference expr)
            {
                HandleTypeNameReference(expr.typeName);

                return null;
            }

            public override object? visitGetReferenceExpr(Expr.GetReference expr)
            {
                while (expr.typeName.Count > 0)
                {
                    if (symbolTable.Current.definitionType == Expr.Definition.DefinitionType.Primitive)
                    {
                        throw new Errors.AnalyzerError("Primitive Field Access", "Primitive classes cannot contain fields");
                    }

                    var variable = symbolTable.GetVariable(expr.typeName.Dequeue());

                    expr.offsets[expr.typeName.Count] = variable;

                    symbolTable.SetContext(variable.type);

                    if (expr.typeName.Count == 0)
                    {
                        expr.type = variable.type;
                    }
                }
                return null;
            }

            private void GetVariableDefinition(Queue<Token> typeName, Expr.StackData stack)
            {
                var context = symbolTable.Current;

                HandleTypeNameReference(typeName);

                stack.size = (symbolTable.Current.definitionType == Expr.Definition.DefinitionType.Primitive) ? ((Expr.Primitive)symbolTable.Current).size : 8;
                stack.type = symbolTable.Current;

                symbolTable.SetContext(context);
            }

            private void HandleTypeNameReference(Queue<Token> typeName)
            {
                symbolTable.SetContext(symbolTable.GetClassFullScope(typeName.Dequeue()));

                while (typeName.Count > 0)
                {
                    symbolTable.SetContext(symbolTable.GetDefinition(typeName.Dequeue()));
                }
            }

            private void HandleConditional(Expr.Conditional expr)
            {
                if (expr.condition != null)
                    expr.condition.Accept(this);

                expr.block.Accept(this);
            }
        }
    }
}
