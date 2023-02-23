﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze
{
    internal partial class Analyzer
    {
        internal partial class InitialPass : Pass<object?>
        {
            SymbolTable symbolTable = SymbolTableSingleton.SymbolTable;

            Tuple<bool, int, Expr.If> waitingIf;

            int index;

            public InitialPass(List<Expr> expressions) : base(expressions)
            {
            }

            internal override List<Expr> Run()
            {
                foreach (Expr expr in expressions)
                {
                    expr.Accept(this);
                }

                if (SymbolTableSingleton.SymbolTable.other.main == null)
                {
                    throw new Errors.AnalyzerError("Entrypoint Not Found", "Program does not contain a Main method");
                }
                return expressions;
            }

            public override object? visitBlockExpr(Expr.Block expr)
            {
                foreach (var blockExpr in expr.block)
                {
                    blockExpr.Accept(this);
                    index++;
                }
                return null;
            }

            public override object? visitFunctionExpr(Expr.Function expr)
            {
                if (symbolTable.ContainsLocalContainerKey(expr.name.lexeme))
                {
                    if (expr.name.lexeme == "Main")
                    {
                        throw new Errors.AnalyzerError("Double Declaration", "A Program may have only one 'Main' method");
                    }
                    throw new Errors.AnalyzerError("Double Declaration", $"Function '{expr.name.lexeme}()' was declared twice");
                }

                SetPath(expr);

                symbolTable.Add(expr);

                if (expr._returnType != "void")
                {
                    if (SymbolTableSingleton.SymbolTable.other.primitives.ContainsKey(expr._returnType))
                    {
                        expr._returnSize = SymbolTableSingleton.SymbolTable.other.primitives[expr._returnType].size;
                    }
                    else
                    {
                        throw new Exception();
                        //expr._returnSize = 8;
                    }
                }

                if (expr.name.lexeme == "Main")
                {
                    if (SymbolTableSingleton.SymbolTable.other.main != null)
                    {
                        throw new Errors.AnalyzerError("Function Declared Twice", "A Program may have only one 'Main' method");
                    }
                    expr.modifiers["static"] = true;
                    SymbolTableSingleton.SymbolTable.other.main = expr;
                }
                int paramsCount = expr.parameters.Count;
                if (paramsCount > InstructionInfo.paramRegister.Length)
                {
                    throw new Errors.AnalyzerError("Too Many Parameters", $"A function cannot have more than { InstructionInfo.paramRegister.Length } parameters");
                }

                foreach (Expr.Parameter paramExpr in expr.parameters)
                {
                    paramExpr.Accept(this);
                }
                expr.block.Accept(this);

                symbolTable.UpContext();
                return null;
            }

            public override object? visitCallExpr(Expr.Call expr)
            {
                foreach (var argExpr in expr.arguments)
                {
                    argExpr.Accept(this);
                }
                return null;
            }

            public override object? visitDeclareExpr(Expr.Declare expr)
            {
                return base.visitDeclareExpr(expr);
            }

            public override object? visitClassExpr(Expr.Class expr)
            {
                if (symbolTable.ContainsContainerKey(expr.name.lexeme, out _, 1))
                {
                    throw new Errors.AnalyzerError("Double Declaration", $"A class named '{expr.name.lexeme}' is already defined in this scope");
                }

                SetPath(expr);

                symbolTable.Add(expr);

                expr.topLevelBlock.Accept(this);
                expr.block.Accept(this);

                expr.constructor = GetConstructor(expr);

                symbolTable.UpContext();
                return null;
            }
            public override object? visitConditionalExpr(Expr.Conditional expr)
            {
                if (expr.type.type == "if")
                {
                    waitingIf = new Tuple<bool, int, Expr.If>(true, index, (Expr.If)expr);
                }
                else if (expr.type.type == "else if")
                {
                    if (waitingIf != null && (waitingIf.Item1 == true && waitingIf.Item2 == (index - 1)))
                    {
                        Expr.ElseIf elif = (Expr.ElseIf)expr;
                        elif.top = waitingIf.Item3;
                        waitingIf.Item3.ElseIfs.Add(elif);
                        waitingIf = new(true, waitingIf.Item2+1, waitingIf.Item3);
                    }
                    else
                    {
                        throw new Errors.AnalyzerError("Invalid Else If", "'else if' conditional has no matching 'if'");
                    }
                }
                else if (expr.type.type == "else")
                {
                    if (waitingIf != null && (waitingIf.Item1 == true && waitingIf.Item2 == (index - 1)))
                    {
                        Expr.Else _else = (Expr.Else)expr;
                        _else.top = waitingIf.Item3;
                        waitingIf.Item3._else = _else;
                        waitingIf = new(false, waitingIf.Item2, null);
                    }
                    else
                    {
                        throw new Errors.AnalyzerError("Invalid Else", "'else' conditional has no matching 'if'");
                    }
                }
                int tmpidx = index;
                base.visitConditionalExpr(expr);
                index = tmpidx;
                return null;
            }

            public override object? visitNewExpr(Expr.New expr)
            {
                return null;
            }

            public override object? visitAssemblyExpr(Expr.Assembly expr)
            {
                if (symbolTable.CurrentIsTop())
                {
                    throw new Errors.AnalyzerError("Top Level Assembly Block", "Assembly Blocks must be placed in an unsafe function");
                }
                if (!symbolTable.Current.IsFunc())
                {
                    throw new Errors.AnalyzerError("ASM Block Not In Function", "Assembly Blocks must be placed in functions");
                }
                if (!((SymbolTable.Symbol.Function)symbolTable.Current).self.modifiers["unsafe"])
                {
                    throw new Errors.AnalyzerError("Unsafe Code in Safe Function", "Mark a function with 'unsafe' to include unsafe code");
                }
                return base.visitAssemblyExpr(expr);
            }

            public override object? visitGetExpr(Expr.Get expr)
            {
                expr.get.Accept(this);
                return null;
            }

            public override object? visitVariableExpr(Expr.Variable expr)
            {
                return null;
            }

            public override object visitAssignExpr(Expr.Assign expr)
            {
                expr.variable.Accept(this);
                return base.visitAssignExpr(expr);
            }

            public override object visitPrimitiveExpr(Expr.Primitive expr)
            {
                if (!SymbolTableSingleton.SymbolTable.other.primitives.ContainsKey(expr.name.lexeme))
                {
                    SymbolTableSingleton.SymbolTable.other.primitives[expr.name.lexeme] = expr;
                }
                else
                {
                    throw new Errors.AnalyzerError("Double Declaration", $"A primtive named '{expr.name.lexeme}' is already defined");
                }
                return null;
            }

            public override object? visitIsExpr(Expr.Is expr)
            {
                if (!(expr.left is Expr.Variable))
                {
                    throw new Errors.AnalyzerError("Invalid 'is' Operator", "the first operand of 'is' operator must be a variable");
                }
                return null;
                
                expr.right.Accept(this);
                return null;
            }

            private Expr.Function GetConstructor(Expr.Class @class)
            {
                if (!symbolTable.ContainsContainerKey(symbolTable.Current.Name.lexeme, out var symbol, 0))
                {
                    throw new Errors.AnalyzerError("Class Without Constructor", "A Class must contain a constructor method");
                }

                var constructor = ((SymbolTable.Symbol.Function)symbol).self;

                constructor.constructor = true;

                if (constructor.modifiers["static"])
                {
                    throw new Errors.AnalyzerError("Constructor Marked 'static'", "A constructor cannot have the 'static' modifier");
                }
                return constructor;
            }

            private void SetPath(Expr.Definition definition)
            {
                if (symbolTable.Current.self.QualifiedName != "")
                {
                    definition.path = symbolTable.Current.self.QualifiedName + ".";
                }
            }
        }
    }
}
