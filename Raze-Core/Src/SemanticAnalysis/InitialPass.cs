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
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.TopLevelCode, expr.name.location, []));
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

            if (expr.externFileName != null)
            {
                expr.modifiers["extern"] = true;
            }
            if (expr.modifiers["extern"])
            {
                if (expr.externFileName == null)
                {
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.ExternWithoutExternFileName, expr.name.lexeme));
                }
                if (expr.block != null)
                {
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.ExternWithBlock, expr.name.lexeme));
                    expr.block = null;
                }
                expr.modifiers["static"] = true;
            }

            if (expr.modifiers["operator"])
            {
                expr.modifiers["static"] = true;
                switch (expr.name.lexeme)
                {
                    // Binary or Unary
                    case "Subtract":
                        if (expr.Arity != 1 && expr.Arity != 2)
                        {
                            Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InvalidOperatorArity, expr.name.location, expr.name.lexeme, "1 or 2"));
                        }
                        break;

                    // Binary
                    case "Add":
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
                            Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InvalidOperatorArity, expr.name.location, expr.name.lexeme, 2));
                        }
                        break;

                    // Unary
                    case "Increment":
                    case "Decrement":
                    case "Not":
                    case "Cast":
                        if (expr.Arity != 1)
                        {
                            Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InvalidOperatorArity, expr.name.location, expr.name.lexeme, 1));
                        }
                        break;
                    default:
                        Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.UnrecognizedOperator, expr.name.location, expr.name.lexeme));
                        expr.modifiers["operator"] = false;
                        break;
                }
            }

            if (expr.Abstract && (symbolTable.NearestEnclosingClass() == null || (symbolTable.NearestEnclosingClass() is Expr.Class _class  && !_class.trait)))
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.AbstractFunctionNotInTrait, expr.name.location, []));
            }

            if (expr.modifiers["inline"])
            {
                if (expr.modifiers["virtual"])
                {
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InvalidFunctionModifierPair, expr.name.location, "virtual", "inline"));
                    expr.modifiers["inline"] = false;
                }
                if (expr.modifiers["extern"])
                {
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InvalidFunctionModifierPair, expr.name.location, "extern", "inline"));
                    expr.modifiers["inline"] = false;
                }
            }

            if (expr.name.lexeme == "Main")
            {
                if (symbolTable.main == null)
                    symbolTable.main = expr;
                else 
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(
                        Diagnostic.DiagnosticName.MainDoubleDeclaration, 
                        expr.name.location
                    ));
            }

            HandleConstructor();

            foreach (var parameter in expr.parameters)
            {
                parameter.stack = new Expr.StackData();
                GetVariableDefinition(parameter.typeName, parameter.stack);
            }

            expr.block?.Accept(this);

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

            var objectType = TypeCheckUtils.objectType.Value;
            if (expr != objectType && expr.superclass.typeName == null)
            {
                expr.superclass.typeName = new();
                expr.superclass.type = objectType;
            }
            else
            {
                expr.superclass.Accept(this);
            }
            
            if (expr.superclass.type?.Matches(expr) == true)
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.CircularInheritance, expr.name.location, expr.ToString(), expr.superclass.type.ToString()));
                expr.superclass.type = TypeCheckUtils.anyType;
            }

            Expr.ListAccept(expr.declarations, this);
            Expr.ListAccept(expr.definitions, this);

            if (!symbolTable.TryGetDefinition(expr.name, out _))
            {
                expr.definitions.Add(new SpecialObjects.DefaultConstructor(expr.name));
            }

            if (expr.superclass.type is Expr.Class _class)
            {
                expr.declarations.InsertRange(0, _class.declarations);
            }
            symbolTable.CheckDuplicates(expr.declarations);

            symbolTable.UpContext();
            return null;
        }

        public override object? VisitIfExpr(Expr.If expr)
        {
            foreach (var conditional in expr.conditionals)
            {
                conditional.condition.Accept(this);
                conditional.block.Accept(this);
            }

            expr._else?.Accept(this);

            return default;
        }

        public override object? VisitNewExpr(Expr.New expr)
        {
            expr.call.callee ??= new Expr.AmbiguousGetReference(new ExprUtils.QueueList<Token>(), false);
            ((Expr.AmbiguousGetReference)expr.call.callee).typeName.Enqueue(expr.call.name);

            expr.call.Accept(this);

            return null;
        }

        public override object? VisitInlineAssemblyExpr(Expr.InlineAssembly expr)
        {
            if (symbolTable.CurrentIsTop())
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.TopLevelCode));
            }
            else if (symbolTable.Current is not Expr.Function)
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InvalidStatementLocation, "Assembly blocks", "functions"));
            }

            if ((symbolTable.Current is Expr.Function) && !((Expr.Function)symbolTable.Current).modifiers["unsafe"])
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.UnsafeCodeInSafeFunction));
            }

            return base.VisitInlineAssemblyExpr(expr);
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
                if (expr.typeName != null)
                {
                    HandleTypeNameReference(expr.typeName);
                    expr.type = (Expr.DataType)symbolTable.Current;
                }
            }
            return null;
        }

        public override object? VisitAssignExpr(Expr.Assign expr)
        {
            if (expr.member.HandleThis())
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InvalidAssignStatement, expr.member.GetLastName().location, "'this'"));
            }

            expr.member.Accept(this);
            return base.VisitAssignExpr(expr);
        }

        public override object? VisitHeapAllocExpr(Expr.HeapAlloc expr)
        {
            expr.size.Accept(this);
            return null;
        }

        public override object? VisitIsExpr(Expr.Is expr)
        {
            expr.left.Accept(this);
            expr.right.Accept(this);
            return null;
        }

        public override object? VisitAsExpr(Expr.As expr)
        {
            expr._is.Accept(this);
            return null;
        }

        public override object? VisitPrimitiveExpr(Expr.Primitive expr)
        {
            symbolTable.AddDefinition(expr);

            if (Enum.TryParse(expr.superclass.typeName, out Parser.LiteralTokenType literalTokenType))
            {
                expr.superclass.type = TypeCheckUtils.literalTypes[literalTokenType];
            }
            else if (expr.superclass.typeName != null)
            {
                Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("Invalid primitive superclass"));
            }

            foreach (var blockExpr in expr.definitions)
            {
                blockExpr.Accept(this);
            }

            foreach (var item in expr.definitions.Where(x => x is Expr.Function function && function.constructor))
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.PrimitiveWithConstructor, item.name.location, []));
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
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.ConstructorWithNonVoidReturnType, constructor.name.location, []));
            }
            if (constructor.modifiers["static"])
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InvalidConstructorModifier, constructor.name.location, "static"));
                constructor.modifiers["static"] = false;
            }
            if (constructor.modifiers["operator"])
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InvalidConstructorModifier, constructor.name.location, "operator"));
                constructor.modifiers["operator"] = false;
            }
        }
    }
}
