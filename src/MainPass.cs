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

            public override object? visitCallExpr(Expr.Call expr)
            {
                for (int i = 0; i < expr.arguments.Count; i++)
                {
                    expr.arguments[i].Accept(this);
                }

                CurrentCalls();

                var context = symbolTable.Current;

                bool instanceCall = false;

                if (expr.callee != null)
                {
                    instanceCall = symbolTable.TryGetVariable(expr.callee.Peek(), out var topSymbol_I, out _);
                    if (instanceCall)
                    {
                        this.visitGetReferenceExpr(expr);
                    }
                    else
                    {
                        this.visitTypeReferenceExpr(expr);
                    }
                }

                symbolTable.SetContext(symbolTable.NearestEnclosingClass());
                symbolTable.SetContext(symbolTable.GetDefinition(expr.name, true));

                // 
                if (symbolTable.Current.definitionType != Expr.Definition.DefinitionType.Function)
                {
                    throw new Exception();
                }

                var callee = ((Expr.Function)symbolTable.Current);

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
                    if (instanceCall && callee.modifiers["static"])
                    {
                        throw new Errors.AnalyzerError("Static Method Called From Instance", "You cannot call a static method from an instance");
                    }
                    if (!instanceCall && !callee.modifiers["static"] && !expr.constructor)
                    {
                        throw new Errors.AnalyzerError("Instance Method Called From Static Context", "You cannot call an instance method from a static context");
                    }
                }

                if (expr.arguments.Count != callee.arity)
                {
                    throw new Errors.BackendError("Arity Mismatch", $"Arity of call for {callee} ({expr.arguments.Count}) does not match the definition's arity ({callee.arity})");
                }

                expr.internalFunction = callee;

                if (expr.get != null)
                {
                    expr.get.Accept(this);
                }

                if (!callee.constructor) 
                { 
                    symbolTable.SetContext(context); 
                }

                return null;
            }

            public override object? visitClassExpr(Expr.Class expr)
            {
                symbolTable.SetContext(symbolTable.GetDefinition(expr.name));

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
                    expr.stack.classScoped = true;
                }

                expr.value.Accept(this);
                
                symbolTable.Add(name, expr.stack, GetVariableDefinition(expr.typeName, expr.stack));

                return null;
            }

            public override object? visitPrimitiveExpr(Expr.Primitive expr)
            {
                symbolTable.SetContext(symbolTable.GetDefinition(expr.name));

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
                symbolTable.SetContext(symbolTable.GetDefinition(expr.name, true));

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

                    symbolTable.Add(paramExpr.name, paramExpr.stack, GetVariableDefinition(paramExpr.typeName, paramExpr.stack), i+Convert.ToInt16(instance), expr.arity);
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

                if (expr.typeName.Count > 1)
                {
                    isClassScoped = !HandleThisCase(expr);

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
                            

                        expr.offsets[expr.typeName.Count] = new(variable.stackOffset);

                        symbolTable.SetContext(variable.type);

                        if (expr.typeName.Count == 0)
                        {
                            expr.type = variable.type;
                        }
                    }
                }
                

                if (expr.typeName.Peek().lexeme == "this")
                {
                    expr.stack = new(symbolTable.NearestEnclosingClass(), false, 8, 8, false);
                }
                else if (symbolTable.TryGetVariable(expr.typeName.Peek(), out var symbol, out bool isClassScopedVar))
                {
                    expr.typeName.Dequeue();

                    expr.stack = new Expr.StackData(symbol.type, symbol.plus, symbol.size, symbol.stackOffset, (expr.offsets.Length == 1)? isClassScopedVar : isClassScoped);
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
                expr.member.Accept(this);
                base.visitAssignExpr(expr);
                return null;
            }

            public override object visitAssemblyExpr(Expr.Assembly expr)
            {
                foreach (var variable in expr.variables.Keys)
                {
                    variable.Accept(this);
                }
                return null;
            }

            public override object? visitNewExpr(Expr.New expr)
            {
                var context = symbolTable.Current;

                expr.call.constructor = true;

                expr.call.Accept(this);

                expr.internalClass = (Expr.Class)symbolTable.Current.enclosing;

                symbolTable.SetContext(context);

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
                HandleThisCase(expr);

                while (expr.typeName.Count > 0)
                {
                    if (symbolTable.Current.definitionType == Expr.Definition.DefinitionType.Primitive)
                    {
                        throw new Errors.AnalyzerError("Primitive Field Access", "Primitive classes cannot contain fields");
                    }

                    var variable = symbolTable.GetVariable(expr.typeName.Dequeue());

                    expr.offsets[expr.typeName.Count] = new(variable.stackOffset);

                    symbolTable.SetContext(variable.type);

                    if (expr.typeName.Count == 0)
                    {
                        expr.type = variable.type;
                    }
                }
                return null;
            }

            private Expr.Definition GetVariableDefinition(Queue<Token> typeName, Expr.StackData stack)
            {
                var context = symbolTable.Current;

                HandleTypeNameReference(typeName);

                stack.size = (symbolTable.Current.definitionType == Expr.Definition.DefinitionType.Primitive) ? ((Expr.Primitive)symbolTable.Current).size : 8;
                var definition = symbolTable.Current;

                symbolTable.SetContext(context);

                return definition;
            }
            
            private bool HandleThisCase(Expr.GetReference expr)
            {
                if (expr.typeName.Peek().lexeme == "this")
                {
                    expr.typeName.Dequeue();

                    expr.offsets[expr.offsets.Length - 1] = new(8);

                    symbolTable.SetContext(symbolTable.NearestEnclosingClass());

                    if (expr.typeName.Count == 0)
                    {
                        expr.type = ((Expr.DataType)symbolTable.Current);
                    }
                    return true;
                }
                return false;
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

            private void CurrentCalls()
            {
                if (symbolTable.Current.definitionType == Expr.Definition.DefinitionType.Function)
                {
                    ((Expr.Function)symbolTable.Current).leaf = false;
                }
            }
        }
    }
}
