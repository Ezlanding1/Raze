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
        public static Expr.Class anyType = new Analyzer.SpecialObjects.Any(new(Token.TokenType.RESERVED, "any"));

        public static Expr.Class _voidType = new Expr.Class(new(Token.TokenType.RESERVED, "void"), new(), new(), new(null));

        private static Expr.Type integralType = new(new Token((Token.TokenType)Parser.LiteralTokenType.Integer));

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
            { "null", null },
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

        public static bool CannotBeRef(Expr expr) => expr is not Expr.GetReference || (expr is Expr.GetReference getRef && getRef.IsMethod());

        public static void TypeCheckConditional(Expr.IVisitor<Expr.Type> visitor, string conditionalName, List<(Expr.Type?, bool, Expr.Return?)> _return, Expr? condition, Expr.Block block)
        {
            int _returnCount = _return.Count;
            if (condition != null)
            {
                var conditionType = condition.Accept(visitor);
                if (!literalTypes[Parser.LiteralTokenType.Boolean].Matches(conditionType))
                {
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.TypeMismatch_Statement, conditionalName, "BOOLEAN", conditionType));
                }
            }
            block.Accept(visitor);

            if (condition != null)
            {
                for (int i = _returnCount; i < _return.Count; i++)
                {
                    _return[i] = (_return[i].Item1, true, _return[i].Item3);
                }
            }
        }

        public static void HandleFunctionReturns(Expr.Function expr, List<(Expr.Type?, bool, Expr.Return?)> _return)
        {
            int _returnCount = 0;
            foreach (var ret in _return)
            {
                if (!ret.Item2)
                {
                    _returnCount++;
                }

                if (ret.Item3 == null)
                    continue;

                MustMatchType(expr._returnType.type, ret.Item1, true);

                ret.Item3.type = expr._returnType.type;
            }
            if (_returnCount == 0 && !Primitives.IsVoidType(expr._returnType.type))
            {
                if (_return.Count == 0)
                {
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.NoReturn));
                }
                else
                {
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.NoReturn_FromAllPaths));
                }
            }
            _return.Clear();
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
        }
    }
}
