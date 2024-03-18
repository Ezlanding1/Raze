using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Analyzer
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
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.TopLevelCode));
                    expr.modifiers["operator"] = false;
                }
            }

            if (expr._returnType.typeName != null)
            {
                expr._returnType.Accept(this);
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
                    case "Indexer":
                        if (expr.Arity != 2)
                        {
                            Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InvalidOperatorArity, expr.name.lexeme, 2));
                        }
                        break;
                    // Unary

                    case "Increment":
                    case "Decrement":
                    case "Not":
                        if (expr.Arity != 1)
                        {
                            Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InvalidOperatorArity, expr.name.lexeme, 1));
                        }
                        break;
                    default:
                        Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.UnrecognizedOperator, expr.name.lexeme));
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
                parameter.stack = new Expr.StackData();
                GetVariableDefinition(parameter.typeName, parameter.stack);
            }

            expr.block.Accept(this);

            symbolTable.UpContext();

            return null;
        }

        public override object? VisitCallExpr(Expr.Call expr)
        {
            if (expr.callee != null)
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

        public override object? VisitNewExpr(Expr.New expr)
        {
            expr.call.callee ??= new Expr.AmbiguousGetReference(new ExprUtils.QueueList<Token>(), false);
            ((Expr.AmbiguousGetReference)expr.call.callee).typeName.Enqueue(expr.call.name);

            expr.call.Accept(this);

            return null;
        }

        public override object? VisitAssemblyExpr(Expr.Assembly expr)
        {
            if (symbolTable.CurrentIsTop())
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.TopLevelCode));
            }
            else if (symbolTable.Current.definitionType != Expr.Definition.DefinitionType.Function)
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InvalidStatementLocation, "Assembly blocks", "functions"));
            }

            if ((symbolTable.Current?.definitionType == Expr.Definition.DefinitionType.Function) && !((Expr.Function)symbolTable.Current).modifiers["unsafe"])
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.UnsafeCodeInSafeFunction));
            }

            return base.VisitAssemblyExpr(expr);
        }

        public override object? VisitAmbiguousGetReferenceExpr(Expr.AmbiguousGetReference expr)
        {
            if (expr.ambiguousCall)
            {
                if (!symbolTable.TryGetDefinitionFullScope(expr.typeName.Peek(), out _))
                {
                    expr.instanceCall = true;
                }
            }
            return null;
        }

        public override object? VisitInstanceGetReferenceExpr(Expr.InstanceGetReference expr)
        {
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
                expr.type = (Expr.DataType)symbolTable.Current;
            }
            return null;
        }

        public override object? VisitAssignExpr(Expr.Assign expr)
        {
            if (expr.member.HandleThis() && (symbolTable.NearestEnclosingClass()?.definitionType != Expr.Definition.DefinitionType.Primitive))
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InvalidAssignStatement, "'this'"));
            }

            expr.member.Accept(this);
            return base.VisitAssignExpr(expr);
        }

        public override object? VisitPrimitiveExpr(Expr.Primitive expr)
        {
            symbolTable.AddDefinition(expr);

            if (Enum.TryParse(expr.superclass.name, out Parser.LiteralTokenType literalTokenType))
            {
                expr.superclass.type = TypeCheckUtils.literalTypes[literalTokenType];
            }
            else if (expr.superclass.name != null)
            {
                Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("Invalid primitive superclass"));
            }

            foreach (var blockExpr in expr.definitions)
            {
                blockExpr.Accept(this);
            }

            symbolTable.UpContext();

            return null;
        }

        private void GetVariableDefinition(ExprUtils.QueueList<Token> typeName, Expr.StackData stack)
        {
            using (new SaveContext())
            {
                HandleTypeNameReference(typeName);

                stack.type = (Expr.DataType)symbolTable.Current;
            }
        }

        public static void HandleTypeNameReference(ExprUtils.QueueList<Token> typeName)
        {
            SymbolTableSingleton.SymbolTable.SetContext(SymbolTableSingleton.SymbolTable.GetClassFullScope(typeName.Dequeue()));

            while (typeName.Count > 0)
            {
                SymbolTableSingleton.SymbolTable.SetContext(SymbolTableSingleton.SymbolTable.GetClass(typeName.Dequeue()));
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
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.ConstructorWithNonVoidReturnType));
            }
            if (constructor.modifiers["static"])
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InvalidConstructorModifier, "static"));
                constructor.modifiers["static"] = false;
            }
            if (constructor.modifiers["operator"])
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InvalidConstructorModifier, "operator"));
                constructor.modifiers["operator"] = false;
            }
        }
    }
}
