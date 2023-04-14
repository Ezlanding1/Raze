using System;
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
                }
                return null;
            }

            public override object? visitFunctionExpr(Expr.Function expr)
            {
                if (symbolTable.TryGetContainer(expr.name.lexeme, out _))
                {
                    if (expr.name.lexeme == "Main")
                    {
                        throw new Errors.AnalyzerError("Double Declaration", "A Program may have only one 'Main' method");
                    }
                    throw new Errors.AnalyzerError("Double Declaration", $"Function '{expr.name.lexeme}()' was declared twice");
                }

                SetPath(expr);

                symbolTable.Add(expr);

                if (expr.name.lexeme == "Main")
                {
                    if (SymbolTableSingleton.SymbolTable.other.main != null)
                    {
                        throw new Errors.AnalyzerError("Function Declared Twice", "A Program may have only one 'Main' method");
                    }
                    expr.modifiers["static"] = true;
                    SymbolTableSingleton.SymbolTable.other.main = expr;
                }

                expr.block.Accept(this);

                symbolTable.UpContext();

                return null;
            }

            public override object? visitCallExpr(Expr.Call expr)
            {
                if (symbolTable.CurrentIsTop()) { throw new Errors.AnalyzerError("Top Level Code", "Top level code is not allowed"); }

                foreach (var argExpr in expr.arguments)
                {
                    argExpr.Accept(this);
                }
                return null;
            }

            public override object? visitDeclareExpr(Expr.Declare expr)
            {
                if (symbolTable.CurrentIsTop()) { throw new Errors.AnalyzerError("Top Level Code", "Top level code is not allowed"); }

                if (symbolTable.Current.IsPrimitive())
                {
                    throw new Errors.AnalyzerError("Invalid Variable Declaration", "A variable may not be declared within a primitive definition");
                }

                return base.visitDeclareExpr(expr);
            }

            public override object? visitClassExpr(Expr.Class expr)
            {
                if (symbolTable.Current.IsFunc())
                {
                    throw new Errors.AnalyzerError("Invalid Class Definition", "A class definition may be only within another class");
                }

                if (symbolTable.TryGetContainer(expr.name.lexeme, out _))
                {
                    throw new Errors.AnalyzerError("Double Declaration", $"A class named '{expr.name.lexeme}' is already defined in this scope");
                }

                SetPath(expr);

                expr.block._classBlock = true;
                expr.topLevelBlock._classBlock = true;

                symbolTable.Add(expr);

                expr.topLevelBlock.Accept(this);
                expr.block.Accept(this);

                expr.constructor = GetConstructor();

                symbolTable.UpContext();
                return null;
            }

            public override object? visitIfExpr(Expr.If expr)
            {
                if (symbolTable.CurrentIsTop()) { throw new Errors.AnalyzerError("Top Level Code", "Top level code is not allowed"); }

                return base.visitIfExpr(expr);
            }

            public override object visitWhileExpr(Expr.While expr)
            {
                if (symbolTable.CurrentIsTop()) { throw new Errors.AnalyzerError("Top Level Code", "Top level code is not allowed"); }

                return base.visitWhileExpr(expr);
            }

            public override object visitForExpr(Expr.For expr)
            {
                if (symbolTable.CurrentIsTop()) { throw new Errors.AnalyzerError("Top Level Code", "Top level code is not allowed"); }

                return base.visitForExpr(expr);
            }

            public override object? visitNewExpr(Expr.New expr)
            {
                if (symbolTable.CurrentIsTop()) { throw new Errors.AnalyzerError("Top Level Code", "Top level code is not allowed"); }

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
                if (symbolTable.Current.IsFunc() && !((SymbolTable.Symbol.Function)symbolTable.Current).self.modifiers["unsafe"])
                {
                    throw new Errors.AnalyzerError("Unsafe Code in Safe Function", "Mark a function with 'unsafe' to include unsafe code");
                }

                foreach (var variable in expr.variables.Keys)
                {
                    variable.Accept(this);
                }

                return base.visitAssemblyExpr(expr);
            }

            public override object? visitGetExpr(Expr.Get expr)
            {
                expr.get.Accept(this);
                return null;
            }

            public override object visitThisExpr(Expr.This expr)
            {
                if (expr.get == null) { return null; }

                return this.visitGetExpr(expr);
            }

            public override object? visitVariableExpr(Expr.Variable expr)
            {
                if (symbolTable.CurrentIsTop()) { throw new Errors.AnalyzerError("Top Level Code", "Top level code is not allowed"); }

                return null;
            }

            public override object visitAssignExpr(Expr.Assign expr)
            {
                if (expr.member.get.name.type == "this" && expr.member.variable == null && !symbolTable.NearestEnclosingClass().IsPrimitive()) { throw new Errors.AnalyzerError("Invalid 'This' Keyword", "The 'this' keyword cannot be assigned to");  }

                expr.member.Accept(this);
                return base.visitAssignExpr(expr);
            }

            public override object visitPrimitiveExpr(Expr.Primitive expr)
            {
                if (symbolTable.Current.IsFunc())
                {
                    throw new Errors.AnalyzerError("Invalid Class Definition", "A primitive class definition may be only within another class");
                }

                if (symbolTable.TryGetContainer(expr.name.lexeme, out _))
                {
                    throw new Errors.AnalyzerError("Double Declaration", $"A primitive class named '{expr.name.lexeme}' is already defined in this scope");
                }

                SetPath(expr);

                symbolTable.Add(expr);

                expr.block.Accept(this);

                symbolTable.UpContext();

                return null;
            }

            public override object? visitIsExpr(Expr.Is expr)
            {
                if (symbolTable.CurrentIsTop()) { throw new Errors.AnalyzerError("Top Level Code", "Top level code is not allowed"); }

                if (!(expr.left is Expr.Variable))
                {
                    throw new Errors.AnalyzerError("Invalid 'is' Operator", "the first operand of 'is' operator must be a variable");
                }
                return null;
            }

            private Expr.Function GetConstructor()
            {
                if (!symbolTable.TryGetContainer(symbolTable.Current.Name.lexeme, out var symbol))
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
                if (symbolTable.Current.self != null && symbolTable.Current.self.QualifiedName != "")
                {
                    definition.path = symbolTable.Current.self.QualifiedName + ".";
                }
            }
        }
    }
}
