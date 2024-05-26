using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Analyzer
{
    internal static class TypeCheckUtils
    {
        public readonly static Expr.Class objectType = new SpecialObjects.Object();
        public readonly static Expr.Class anyType = new SpecialObjects.Any(new(Token.TokenType.IDENTIFIER, "any"));
        public readonly static Expr.Class _voidType = new Expr.Class(new(Token.TokenType.RESERVED, "void"), new(), new(), new(null));
        private readonly static Expr.Type integralType = new(new Token((Token.TokenType)Parser.LiteralTokenType.Integer));

        public static Dictionary<Parser.LiteralTokenType, Expr.Type> literalTypes = new Dictionary<Parser.LiteralTokenType, Expr.Type>()
        {
            { Parser.VoidTokenType, _voidType },
            { Parser.LiteralTokenType.Integer, integralType },
            { Parser.LiteralTokenType.UnsignedInteger, new(new Token((Token.TokenType)Parser.LiteralTokenType.UnsignedInteger)) },
            { Parser.LiteralTokenType.Floating, new(new Token((Token.TokenType)Parser.LiteralTokenType.Floating)) },
            { Parser.LiteralTokenType.String, new(new Token((Token.TokenType)Parser.LiteralTokenType.String)) },
            { Parser.LiteralTokenType.Binary, integralType },
            { Parser.LiteralTokenType.Hex, integralType },
            { Parser.LiteralTokenType.Boolean, new(new Token((Token.TokenType)Parser.LiteralTokenType.Boolean)) },
            { Parser.LiteralTokenType.RefString, new(new Token((Token.TokenType)Parser.LiteralTokenType.RefString)) },
        };

        public static Dictionary<string, Expr.Type> keywordTypes = new Dictionary<string, Expr.Type>()
        {
            { "true", literalTypes[Parser.LiteralTokenType.Boolean] },
            { "false", literalTypes[Parser.LiteralTokenType.Boolean] },
            { "null", new SpecialObjects.Null() },
        };

        public static void MustMatchType(Expr.Type type1, Expr.Type type2, bool _return=false)
        {
            if (!type2.Matches(type1))
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(
                    _return? Diagnostic.DiagnosticName.TypeMismatch_Return : Diagnostic.DiagnosticName.TypeMismatch,
                    type2,
                    type1
                ));
            }
        }

        public static bool CannotBeRef(Expr expr) => expr is not Expr.GetReference || (expr is Expr.GetReference getRef && getRef.IsMethodCall());

        public static void RunConditionals(Expr.IVisitor<Expr.Type> visitor, string conditionalName, List<Expr.Conditional> conditionals)
        {
            var savedStates = SymbolTableSingleton.SymbolTable.GetFrameData().ToList();
            conditionals.ForEach(x => TypeCheckConditional(visitor, conditionalName, x.condition, x.block));
            SymbolTableSingleton.SymbolTable.SetFrameDataStates(savedStates);
        }

        public static void TypeCheckConditional(Expr.IVisitor<Expr.Type> visitor, string conditionalName, Expr? condition, Expr.Block block)
        {
            if (condition != null)
            {
                var conditionType = condition.Accept(visitor);
                if (!literalTypes[Parser.LiteralTokenType.Boolean].Matches(conditionType))
                {
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.TypeMismatch_Statement, conditionalName, "BOOLEAN", conditionType));
                }
            }
            block.Accept(visitor);
        }

        public static void HandleFunctionReturns(Expr.Function expr)
        {
            SymbolTable symbolTable = SymbolTableSingleton.SymbolTable;

            foreach (var returnType in symbolTable.returnFrameData.returnTypes)
            {
                if (returnType != null)
                {
                    MustMatchType(expr._returnType.type, returnType, true);
                }
            }
            if (!symbolTable.returnFrameData.initialized && !Primitives.IsVoidType(expr._returnType.type))
            {
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(
                        symbolTable.returnFrameData.initializedOnAnyBranch?
                            Diagnostic.DiagnosticName.NoReturn_FromAllPaths : 
                            Diagnostic.DiagnosticName.NoReturn
                    ));
            }
            symbolTable.returnFrameData.initialized = false;
            symbolTable.returnFrameData.returnTypes.Clear();
        }

        public static void ValidateCall(Expr.Call expr, Expr.Function callee)
        {
            if (!expr.constructor && callee.constructor)
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.ConstructorCalledAsMethod));
            }
            else if (expr.constructor && !callee.constructor)
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.MethodCalledAsConstructor));
            }

            if (expr.callee != null)
            {
                if (expr.instanceCall && callee.modifiers["static"])
                {
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.StaticMethodCalledFromInstanceContext));
                }
                if (!expr.instanceCall && !callee.modifiers["static"] && !expr.constructor)
                {
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InstanceMethodCalledFromStaticContext));
                }
            }
            ValidateFunctionParameterModifiers(expr);
        }
        public static void ValidateFunctionParameterModifiers(Expr.ICall iCall)
        {
            for (int i = 0; i < iCall.InternalFunction.Arity && iCall.InternalFunction.parameters[i].modifiers["ref"]; i++)
            {
                ValidateRefVariable(iCall.Arguments[i], true);
            }
        }
        public static void ValidateRefVariable(Expr expr, bool assign)
        {
            if (CannotBeRef(expr))
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(
                        assign? 
                        Diagnostic.DiagnosticName.InvalidParameterModifier_Ref : 
                        Diagnostic.DiagnosticName.InvalidFunctionModifier_Ref
                    )
                );
            }
        }
    }
}
