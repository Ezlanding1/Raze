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
                    Diagnostics.errors.Push(new Error.AnalyzerError("Invalid Operator Definition", $"Top level operator function definitions are not allowed"));
                    expr.modifiers["operator"] = false;
                }
            }

            if (expr._returnType.typeName != null)
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
                            Diagnostics.errors.Push(new Error.AnalyzerError("Invalid Operator Definition", $"The '{expr.name.lexeme}' operator must have an arity of 2"));
                        }
                        break;
                    // Unary

                    case "Increment":
                    case "Decrement":
                    case "Not":
                        if (expr.Arity != 1)
                        {
                            Diagnostics.errors.Push(new Error.AnalyzerError("Invalid Operator Definition", $"The '{expr.name.lexeme}' operator must have an arity of 1"));
                        }
                        break;
                    default:
                        Diagnostics.errors.Push(new Error.AnalyzerError("Invalid Operator Definition", $"'{expr.name.lexeme}' is not a recognized operator"));
                        expr.modifiers["operator"] = false;
                        break;
                }
            }
            
            if (expr.name.lexeme == "Main")
            {
                symbolTable.main = expr;
            }

            HandleConstructor();

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
            HandleTopLevelCode();

            GetVariableDefinition(expr.typeName, expr.stack);

            return base.VisitDeclareExpr(expr);
        }

        public override object? VisitClassExpr(Expr.Class expr)
        {
            symbolTable.AddDefinition(expr);

            Expr.ListAccept(expr.declarations, this);
            Expr.ListAccept(expr.definitions, this);

            if (!symbolTable.TryGetDefinition(expr.name, out _))
            {
                expr.definitions.Add(new SpecialObjects.DefaultConstructor(((Expr.Class)symbolTable.Current).name));

            }

            symbolTable.UpContext();
            return null;
        }

        public override object? VisitIfExpr(Expr.If expr)
        {
            HandleTopLevelCode();
            return base.VisitIfExpr(expr);
        }

        public override object VisitWhileExpr(Expr.While expr)
        {
            HandleTopLevelCode();
            return base.VisitWhileExpr(expr);
        }

        public override object VisitForExpr(Expr.For expr)
        {
            HandleTopLevelCode();
            return base.VisitForExpr(expr);
        }

        public override object? VisitNewExpr(Expr.New expr)
        {
            HandleTopLevelCode();
            
            expr.call.callee.typeName ??= new();
            expr.call.callee.typeName.Enqueue(expr.call.name);

            expr.call.Accept(this);

            return null;
        }

        public override object VisitReturnExpr(Expr.Return expr)
        {
            HandleTopLevelCode();
            return base.VisitReturnExpr(expr);
        }

        public override object? VisitAssemblyExpr(Expr.Assembly expr)
        {
            if (symbolTable.CurrentIsTop())
            {
                Diagnostics.errors.Push(new Error.AnalyzerError("Top Level Assembly Block", "Assembly Blocks must be placed in an unsafe function"));
            }
            else if (symbolTable.Current.definitionType != Expr.Definition.DefinitionType.Function)
            {
                Diagnostics.errors.Push(new Error.AnalyzerError("ASM Block Not In Function", "Assembly Blocks must be placed in functions"));
            }
            
            if ((symbolTable.Current?.definitionType == Expr.Definition.DefinitionType.Function) && !((Expr.Function)symbolTable.Current).modifiers["unsafe"])
            {
                Diagnostics.errors.Push(new Error.AnalyzerError("Unsafe Code in Safe Function", "Mark a function with 'unsafe' to include unsafe code"));
            }

            return base.VisitAssemblyExpr(expr);
        }

        public override object? VisitGetReferenceExpr(Expr.GetReference expr)
        {
            HandleTopLevelCode();

            if (expr.ambiguousCall)
            {
                var call = (Expr.Call)expr.getters[0];

                if (call.callee.typeName != null)
                {
                    call.instanceCall = !symbolTable.TryGetDefinitionFullScope(call.callee.typeName.Peek(), out _);

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
            if (expr.member.getters.Count == 1 && expr.member.getters[0].name.lexeme == "this" && (symbolTable.NearestEnclosingClass()?.definitionType != Expr.Definition.DefinitionType.Primitive)) 
            { 
                Diagnostics.errors.Push(new Error.AnalyzerError("Invalid 'This' Keyword", "The 'this' keyword cannot be assigned to"));
                expr.member.getters[0].name.type = Token.TokenType.IDENTIFIER;
            }

            expr.member.Accept(this);
            return base.VisitAssignExpr(expr);
        }

        public override object VisitPrimitiveExpr(Expr.Primitive expr)
        {
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
            HandleTopLevelCode();
            return null;
        }

        private void HandleTopLevelCode()
        {
            if (symbolTable.CurrentIsTop()) Diagnostics.errors.Push(new Error.AnalyzerError("Top Level Code", "Top level code is not allowed"));
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
                symbolTable.SetContext(symbolTable.GetClass(typeName.Dequeue()));
            }
        }

        private void HandleConstructor()
        {
            if (!(symbolTable.Current.name.lexeme == symbolTable.NearestEnclosingClass()?.name.lexeme))
            {
                return;
            }

            var constructor = (Expr.Function)symbolTable.Current;

            constructor.constructor = true;

            if (constructor._returnType.typeName != null)
            {
                Diagnostics.errors.Push(new Error.AnalyzerError("Constructor With Non-Void Return Type", "The return type of a constructor must be 'void'"));
            }
            if (constructor.modifiers["static"])
            {
                Diagnostics.errors.Push(new Error.AnalyzerError("Constructor Marked 'static'", "A constructor cannot have the 'static' modifier"));
                constructor.modifiers["static"] = false;
            }
            if (constructor.modifiers["operator"])
            {
                Diagnostics.errors.Push(new Error.AnalyzerError("Constructor Marked 'operator'", "A constructor cannot have the 'operator' modifier"));
                constructor.modifiers["static"] = false;
            }
        }
    }
}
