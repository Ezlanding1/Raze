using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

internal partial class Analyzer
{
    internal partial class InitialPass : Pass<object?>
    {
        SymbolTable symbolTable = SymbolTableSingleton.SymbolTable;

        public InitialPass(List<Expr> expressions) : base(expressions)
        {
        }

        internal override void Run()
        {
            symbolTable.CheckGlobals();

            foreach (Expr expr in expressions)
            {
                expr.Accept(this);
            }
        }

        public override object? VisitBlockExpr(Expr.Block expr)
        {
            foreach (var blockExpr in expr.block)
            {
                blockExpr.Accept(this);
            }
            return null;
        }

        public override object? VisitFunctionExpr(Expr.Function expr)
        {
            symbolTable.AddDefinition(expr);

            if (expr.enclosing == null)
            {
                expr.modifiers["static"] = true;

                if (expr.modifiers["operator"]) 
                {
                    throw new Error.AnalyzerError("Invalid Operator Definition", $"Top level operator function definitions are not allowed");
                }
            }

            if (expr._returnType.typeName.Peek().type != Token.TokenType.RESERVED && expr._returnType.typeName.Peek().lexeme != "void")
            {
                expr._returnType.Accept(this);
                expr._returnSize = (expr._returnType.type?.definitionType == Expr.Definition.DefinitionType.Primitive) ? ((Expr.Primitive)expr._returnType.type).size : 8;
            }
            else
            {
                expr._returnType.type = TypeCheckUtils._voidType;
            }

            if (expr.modifiers["operator"])
            {
                expr.modifiers["static"] = true;
                switch (expr.name.lexeme)
                {
                    // Binary

                    case "Add":
                    case "Subtract":
                    case "Multiply":
                    case "Divide":
                    case "Modulo":
                    case "BitwiseAnd":
                    case "BitwiseOr":
                    case "BitwiseXor":
                    case "BitwiseShiftLeft":
                    case "BitwiseShiftRight":
                    case "EqualTo":
                    case "NotEqualTo":
                    case "GreaterThan":
                    case "LessThan":
                    case "GreaterThanOrEqualTo":
                    case "LessThanOrEqualTo":
                        if (expr.Arity != 2)
                        {
                            throw new Error.AnalyzerError("Invalid Operator Definition", $"The '{expr.name.lexeme}' operator must have an arity of 2");
                        }
                        break;
                    // Unary

                    case "Increment":
                    case "Decrement":
                    case "Not":
                        if (expr.Arity != 1)
                        {
                            throw new Error.AnalyzerError("Invalid Operator Definition", $"The '{expr.name.lexeme}' operator must have an arity of 1");
                        }
                        break;
                    default:
                        throw new Error.AnalyzerError("Invalid Operator Definition", $"'{expr.name.lexeme}' is not a recognized operator");
                }
            }
            
            if (expr.name.lexeme == "Main")
            {
                symbolTable.main = expr;
            }

            foreach (var parameter in expr.parameters) 
            {
                parameter.stack = (expr.modifiers["inline"]) ? new Expr.StackRegister() : new Expr.StackData();
                GetVariableDefinition(parameter.typeName, parameter.stack);
            }

            foreach (var blockExpr in expr.block)
            {
                blockExpr.Accept(this);
            }

            symbolTable.UpContext();

            return null;
        }

        public override object? VisitCallExpr(Expr.Call expr)
        {
            if (expr.callee.typeName != null)
            {
                expr.callee.Accept(this);
            }

            foreach (var argExpr in expr.arguments)
            {
                argExpr.Accept(this);
            }
            
            return null;
        }

        public override object? VisitDeclareExpr(Expr.Declare expr)
        {
            if (symbolTable.CurrentIsTop()) { throw new Error.AnalyzerError("Top Level Code", "Top level code is not allowed"); }

            if (symbolTable.Current?.definitionType == Expr.Definition.DefinitionType.Primitive)
            {
                throw new Error.AnalyzerError("Invalid Variable Declaration", "A variable may not be declared within a primitive definition");
            }

            GetVariableDefinition(expr.typeName, expr.stack);

            return base.VisitDeclareExpr(expr);
        }

        public override object? VisitClassExpr(Expr.Class expr)
        {
            if (symbolTable.Current?.definitionType == Expr.Definition.DefinitionType.Function)
            {
                throw new Error.AnalyzerError("Invalid Class Definition", "A class definition may be only within another class");
            }

            symbolTable.AddDefinition(expr);

            Expr.ListAccept(expr.declarations, this);
            Expr.ListAccept(expr.definitions, this);

            GetConstructor();

            symbolTable.UpContext();
            return null;
        }

        public override object? VisitIfExpr(Expr.If expr)
        {
            if (symbolTable.CurrentIsTop()) { throw new Error.AnalyzerError("Top Level Code", "Top level code is not allowed"); }

            return base.VisitIfExpr(expr);
        }

        public override object VisitWhileExpr(Expr.While expr)
        {
            if (symbolTable.CurrentIsTop()) { throw new Error.AnalyzerError("Top Level Code", "Top level code is not allowed"); }

            return base.VisitWhileExpr(expr);
        }

        public override object VisitForExpr(Expr.For expr)
        {
            if (symbolTable.CurrentIsTop()) { throw new Error.AnalyzerError("Top Level Code", "Top level code is not allowed"); }

            return base.VisitForExpr(expr);
        }

        public override object? VisitNewExpr(Expr.New expr)
        {
            if (symbolTable.CurrentIsTop()) { throw new Error.AnalyzerError("Top Level Code", "Top level code is not allowed"); }
            
            expr.call.callee.typeName ??= new();
            expr.call.callee.typeName.Enqueue(expr.call.name);

            expr.call.Accept(this);

            return null;
        }

        public override object? VisitAssemblyExpr(Expr.Assembly expr)
        {
            if (symbolTable.CurrentIsTop())
            {
                throw new Error.AnalyzerError("Top Level Assembly Block", "Assembly Blocks must be placed in an unsafe function");
            }

            if (symbolTable.Current?.definitionType != Expr.Definition.DefinitionType.Function)
            {
                throw new Error.AnalyzerError("ASM Block Not In Function", "Assembly Blocks must be placed in functions");
            }
            if ((symbolTable.Current?.definitionType == Expr.Definition.DefinitionType.Function) && !((Expr.Function)symbolTable.Current).modifiers["unsafe"])
            {
                throw new Error.AnalyzerError("Unsafe Code in Safe Function", "Mark a function with 'unsafe' to include unsafe code");
            }

            foreach (var variable in expr.variables)
            {
                variable.Accept(this);
            }

            return base.VisitAssemblyExpr(expr);
        }

        public override object? VisitGetReferenceExpr(Expr.GetReference expr)
        {
            if (symbolTable.CurrentIsTop()) { throw new Error.AnalyzerError("Top Level Code", "Top level code is not allowed"); }

            if (expr.ambiguousCall)
            {
                var call = (Expr.Call)expr.getters[0];

                if (call.callee.typeName != null)
                {
                    call.instanceCall = !symbolTable.TryGetClassFullScope(call.callee.typeName.Peek(), out _);

                    if ((bool)call.instanceCall)
                    {
                        expr.getters.InsertRange(0, call.callee.ToGetReference().getters);
                        call.callee.typeName = null;
                    }
                }
                else
                {
                    call.instanceCall = null;
                }
            }
            
            foreach (Expr.Getter get in expr.getters)
            {
                get.Accept(this);
            }
            return null;
        }

        public override object? VisitTypeReferenceExpr(Expr.TypeReference expr)
        {
            using (new SaveContext())
            {
                if (expr.typeName.Count != 0)
                {
                    HandleTypeNameReference(expr.typeName);
                }
                expr.type = symbolTable.Current;
            }
            return null;
        }

        public override object VisitAssignExpr(Expr.Assign expr)
        {
            if (expr.member.getters.Count == 1 && expr.member.getters[0].name.lexeme == "this" && (symbolTable.NearestEnclosingClass()?.definitionType != Expr.Definition.DefinitionType.Primitive)) { throw new Error.AnalyzerError("Invalid 'This' Keyword", "The 'this' keyword cannot be assigned to");  }

            expr.member.Accept(this);
            return base.VisitAssignExpr(expr);
        }

        public override object VisitPrimitiveExpr(Expr.Primitive expr)
        {
            if (symbolTable.Current?.definitionType == Expr.Definition.DefinitionType.Function)
            {
                throw new Error.AnalyzerError("Invalid Class Definition", "A primitive class definition may be only within another class");
            }

            symbolTable.AddDefinition(expr);

            foreach (var blockExpr in expr.definitions)
            {
                blockExpr.Accept(this);
            }
            
            symbolTable.UpContext();

            return null;
        }

        public override object? VisitIsExpr(Expr.Is expr)
        {
            if (symbolTable.CurrentIsTop()) { throw new Error.AnalyzerError("Top Level Code", "Top level code is not allowed"); }

            if (!(expr.left is Expr.GetReference))
            {
                throw new Error.AnalyzerError("Invalid 'is' Operator", "the first operand of 'is' operator must be a variable");
            }
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

        private void GetConstructor()
        {
            if (!symbolTable.TryGetDefinition(symbolTable.Current.name, out var symbol))
            {
                throw new Error.AnalyzerError("Class Without Constructor", "A Class must contain a constructor method");
            }

            var constructor = (Expr.Function)symbol;

            constructor.constructor = true;

            if (constructor.modifiers["static"])
            {
                throw new Error.AnalyzerError("Constructor Marked 'static'", "A constructor cannot have the 'static' modifier");
            }
            if (constructor.modifiers["operator"])
            {
                throw new Error.AnalyzerError("Constructor Marked 'operator'", "A constructor cannot have the 'operator' modifier");
            }
        }
    }
}
