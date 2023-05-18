namespace Raze
{
    internal partial class Analyzer
    {
        internal class MainPass : Pass<object?>
        {
            SymbolTable symbolTable = SymbolTableSingleton.SymbolTable;
            HashSet<Expr.Definition> handledClasses;

            public MainPass(List<Expr> expressions) : base(expressions)
            {
                this.handledClasses = new();
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

                if (!expr._classBlock)
                {
                    symbolTable.RemoveUnderCurrent();
                }
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
                    instanceCall = symbolTable.TryGetVariable(expr.callee.Peek().lexeme, out SymbolTable.Symbol.Variable topSymbol_I, out _);

                    if (instanceCall)
                    {
                        expr.stackOffset = topSymbol_I.self.stackOffset;
                        this.visitGetReferenceExpr(expr);
                    }
                    else
                    {
                        this.visitTypeReferenceExpr(expr);
                    }
                }
                
                symbolTable.SetContext(symbolTable.GetContainer(expr.name.lexeme, true));

                // 
                if (!symbolTable.Current.IsFunc())
                {
                    throw new Exception();
                }

                var callee = ((SymbolTable.Symbol.Function)symbolTable.Current).self;

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
                    throw new Errors.BackendError("Arity Mismatch", $"Arity of call for {callee.type.ToString()} ({expr.arguments.Count}) does not match the definition's arity ({callee.arity})");
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
                if (handledClasses.Contains(expr))
                {
                    return null;
                }

                handledClasses.Add(expr);

                symbolTable.SetContext(symbolTable.GetContainer(expr.name.lexeme));

                expr.topLevelBlock.Accept(this);
                expr.block.Accept(this);


                symbolTable.UpContext();

                return null;
            }

            public override object? visitDeclareExpr(Expr.Declare expr)
            {
                var name = expr.name;

                if (symbolTable.TryGetVariable(name.lexeme, out _, out _, true))
                {
                    throw new Errors.AnalyzerError("Double Declaration", $"A variable named '{name.lexeme}' is already defined in this scope");
                }

                if (symbolTable.Current.IsClass())
                {
                    expr.stack.classScoped = true;
                }

                expr.value.Accept(this);

                var context = symbolTable.Current;

                this.visitTypeReferenceExpr(expr.type);
                (expr.stack.size, var definition) = symbolTable.Current.IsPrimitive() ? (((SymbolTable.Symbol.Primitive)symbolTable.Current).self.size, symbolTable.Current) : (8, symbolTable.Current);

                symbolTable.SetContext(context);

                expr.stack.type = definition.self.type;


                symbolTable.Add(expr.stack, name, definition);

                return null;
            }

            public override object? visitPrimitiveExpr(Expr.Primitive expr)
            {
                if (handledClasses.Contains(expr))
                {
                    return null;
                }

                handledClasses.Add(expr);

                symbolTable.SetContext(symbolTable.GetContainer(expr.name.lexeme));

                expr.block.Accept(this);


                symbolTable.UpContext();

                return null;
            }

            public override object? visitFunctionExpr(Expr.Function expr)
            {
                symbolTable.SetContext(symbolTable.GetContainer(expr.name.lexeme, true));

                if (expr._returnType.typeName.Peek().type != "void")
                {
                    var context = symbolTable.Current;
                    expr._returnType.Accept(this);
                    expr._returnType.type = symbolTable.Current.self.type;
                    expr._returnSize = symbolTable.Current.IsPrimitive() ? ((SymbolTable.Symbol.Primitive)symbolTable.Current).self.size : 8;
                    symbolTable.SetContext(context);
                }
                else
                {
                    expr._returnType.type = TypeCheckPass._voidType;
                }


                symbolTable.CreateBlock();

                int count = 0;

                if (!expr.modifiers["static"])
                {
                    symbolTable.Current.self.size += 8;
                    count++;
                }

                for (int i = 0; i < expr.arity; i++)
                {
                    Expr.Parameter paramExpr = expr.parameters[i];
                    var context = symbolTable.Current;

                    paramExpr.Accept(this);

                    (paramExpr.stack.size, var definition) = symbolTable.Current.IsPrimitive() ? (((SymbolTable.Symbol.Primitive)symbolTable.Current).self.size, symbolTable.Current) : (8, symbolTable.Current);

                    symbolTable.SetContext(context);

                    paramExpr.stack.type = definition.self.type;

                    symbolTable.Add(paramExpr, definition, i+count, expr.arity);
                }

                expr.block.Accept(this);


                symbolTable.RemoveUnderCurrent();
                symbolTable.UpContext();

                if (expr.constructor)
                {
                    // Assumes a function is enclosed by a class (no nested functions)
                    expr.block.Extend(((SymbolTable.Symbol.Class)symbolTable.Current).self.topLevelBlock);
                }

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
                        if (symbolTable.Current.IsPrimitive())
                        {
                            throw new Errors.AnalyzerError("Primitive Field Access", "Primitive classes cannot contain fields");
                        }

                        SymbolTable.Symbol.Variable variable;

                        if (expr.offsets.Length == expr.typeName.Count)
                        {
                            variable = symbolTable.GetVariable(expr.typeName.Dequeue().lexeme, out isClassScoped);
                        }
                        else 
                        {
                            variable = symbolTable.GetVariable(expr.typeName.Dequeue().lexeme);
                        }
                            

                        expr.offsets[expr.typeName.Count] = new(variable.self.stackOffset);

                        symbolTable.SetContext(variable.definition);

                        if (expr.typeName.Count == 0)
                        {
                            expr.type = variable.self.type;
                        }
                    }
                }
                

                if (expr.typeName.Peek().type == "this")
                {
                    expr.stack = new(symbolTable.NearestEnclosingClass().self.type, false, 8, 8, false);
                }
                else if (symbolTable.TryGetVariable(expr.typeName.Peek().lexeme, out SymbolTable.Symbol.Variable symbol, out bool isClassScopedVar))
                {
                    expr.typeName.Dequeue();

                    expr.stack = new Expr.StackData(symbol.self.type, symbol.self.plus, symbol.self.size, symbol.self.stackOffset, (expr.offsets.Length == 1)? isClassScopedVar : isClassScoped);
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

                CurrentCalls();

                expr.call.constructor = true;

                expr.call.Accept(this);

                expr.internalClass = ((SymbolTable.Symbol.Class)symbolTable.Current.enclosing).self;

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
                symbolTable.SetContext(symbolTable.GetClassFullScope(expr.typeName.Dequeue().lexeme));

                while (expr.typeName.Count > 0)
                {
                    symbolTable.SetContext(symbolTable.GetContainer(expr.typeName.Dequeue().lexeme));
                }

                return null;
            }

            public override object? visitGetReferenceExpr(Expr.GetReference expr)
            {
                HandleThisCase(expr);

                while (expr.typeName.Count > 0)
                {
                    if (symbolTable.Current.IsPrimitive())
                    {
                        throw new Errors.AnalyzerError("Primitive Field Access", "Primitive classes cannot contain fields");
                    }

                    var variable = symbolTable.GetVariable(expr.typeName.Dequeue().lexeme);

                    expr.offsets[expr.typeName.Count] = new(variable.self.stackOffset);

                    symbolTable.SetContext(variable.definition);

                    if (expr.typeName.Count == 0)
                    {
                        expr.type = variable.self.type;
                    }
                }
                return null;
            }
            
            private bool HandleThisCase(Expr.GetReference expr)
            {
                if (expr.typeName.Peek().type == "this")
                {
                    expr.typeName.Dequeue();

                    expr.offsets[expr.offsets.Length - 1] = new(8);

                    symbolTable.SetContext(symbolTable.NearestEnclosingClass());

                    if (expr.typeName.Count == 0)
                    {
                        expr.type = symbolTable.Current.self.type;
                    }
                    return true;
                }
                return false;
            }

            private void HandleConditional(Expr.Conditional expr)
            {
                if (expr.condition != null)
                    expr.condition.Accept(this);

                expr.block.Accept(this);
            }

            private void CurrentCalls()
            {
                if (symbolTable.Current.IsFunc())
                {
                    ((SymbolTable.Symbol.Function)symbolTable.Current).self.leaf = false;
                }
            }
        }
    }
}
