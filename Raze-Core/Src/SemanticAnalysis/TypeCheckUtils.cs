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
        // Primitive Types:

        public readonly static Expr.Class objectType = new SpecialObjects.Object();
        public readonly static Expr.Class anyType = new SpecialObjects.Any(new(Token.TokenType.IDENTIFIER, "any", Location.NoLocation));
        public readonly static Expr.Class _voidType = new Expr.Class(new(Token.TokenType.RESERVED, "void", Location.NoLocation), new(), new(), new(null));

        public static Dictionary<Parser.LiteralTokenType, Expr.Type> literalTypes = new Dictionary<Parser.LiteralTokenType, Expr.Type>()
        {
            { Parser.VoidTokenType, _voidType },
            { Parser.LiteralTokenType.Integer, new(new Token((Token.TokenType)Parser.LiteralTokenType.Integer, Location.NoLocation)) },
            { Parser.LiteralTokenType.UnsignedInteger, new(new Token((Token.TokenType)Parser.LiteralTokenType.UnsignedInteger, Location.NoLocation)) },
            { Parser.LiteralTokenType.Floating, new(new Token((Token.TokenType)Parser.LiteralTokenType.Floating, Location.NoLocation)) },
            { Parser.LiteralTokenType.String, new(new Token((Token.TokenType)Parser.LiteralTokenType.String, Location.NoLocation)) },
            { Parser.LiteralTokenType.Boolean, new(new Token((Token.TokenType)Parser.LiteralTokenType.Boolean, Location.NoLocation)) },
            { Parser.LiteralTokenType.RefString, new(new Token((Token.TokenType)Parser.LiteralTokenType.RefString, Location.NoLocation)) },
        };

        public static Dictionary<string, Expr.Type> keywordTypes = new Dictionary<string, Expr.Type>()
        {
            { "true", literalTypes[Parser.LiteralTokenType.Boolean] },
            { "false", literalTypes[Parser.LiteralTokenType.Boolean] },
            { "null", new SpecialObjects.Null() },
            { "void", literalTypes[Parser.VoidTokenType] }
        };


        // Runtime Library Types:

        public static Dictionary<Parser.LiteralTokenType, RuntimeLibrarySingletonDataType> defualtLiteralTypes = new()
        {
            { Parser.LiteralTokenType.Integer,  new("Raze.Std", ["Std", "int64"]) },
            { Parser.LiteralTokenType.UnsignedInteger, new("Raze.Std", ["Std", "uint64"]) },
            { Parser.LiteralTokenType.Floating, new("Raze.Std", ["Std", "float64"]) },
            { Parser.LiteralTokenType.String, new("Raze.Std", ["Std", "char"]) },
            { Parser.LiteralTokenType.Boolean, new("Raze.Std", ["Std", "bool"]) },
            { Parser.LiteralTokenType.RefString, new("Raze.Std", ["Std", "string"]) },
        };

        public static RuntimeLibrarySingletonDataType heapallocType = new("Raze.Std", "HeapData");
        public static RuntimeLibrarySingletonFunction newFunction = new("System", [Diagnostics.runtimeName], "New", [defualtLiteralTypes[Parser.LiteralTokenType.UnsignedInteger]]);
        public static RuntimeLibrarySingletonFunction exitFunction = new("System", [Diagnostics.runtimeName], "Exit", [new("Raze.Std", ["Std", "int"])]);


        public static void MustMatchType(Expr.Type type1, Expr.Type type2, bool _ref1, bool _readonly1, Expr expr2, bool declare, bool _return) =>
            MustMatchType((type1, type2), (_ref1, IsVariableWithRefModifier(expr2)), (_readonly1, GetVariableModifiers(expr2)._readonly), declare, _return);
        
        public static void MustMatchType(
            (Expr.Type type1, Expr.Type type2) types,
            (bool _ref1, bool _ref2) _refs, 
            (bool _readonly1, bool _readonly2) _readonlys, 
            bool declare, 
            bool _return)
        {
            if (_refs._ref1 && _readonlys._readonly2 && !_readonlys._readonly1)
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(
                    Diagnostic.DiagnosticName.ReadonlyFieldModified_Ref,
                    "readonly " + types.type2.ToString(),
                    "ref " + types.type1.ToString()
                ));
            }

            if (!types.type2.Matches(types.type1) || IsInvalidRef(declare, _refs._ref1, _refs._ref2))
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(
                    _return? Diagnostic.DiagnosticName.TypeMismatch_Return : Diagnostic.DiagnosticName.TypeMismatch,
                    (_refs._ref2 ? "ref " : "") + types.type2.ToString(),
                    (_refs._ref1 ? "ref " : "") + types.type1.ToString()
                ));
            }
        }

        private static bool IsInvalidRef(bool declare, bool _ref1, bool _ref2) => declare ? _ref1 ^ _ref2 : (!_ref1 && _ref2);

        public static bool IsVariableWithRefModifier(Expr expr) =>
            expr is Expr.GetReference getRef && getRef._ref;

        public static (bool _ref, bool _readonly) GetVariableModifiers(Expr expr) =>
            !CannotBeRef(expr, out var getRef) ?
                (getRef.GetLastData()?._ref == true, getRef.GetLastData()?._readonly == true) :
                (false, false);

        public static bool CannotBeRef(Expr expr, out Expr.GetReference getRef) =>
            (getRef = expr as Expr.GetReference) == null || getRef.IsMethodCall();

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

            foreach (var (_ref, type) in symbolTable.returnFrameData.returnDatas)
            {
                if (type != null)
                {
                    MustMatchType((expr._returnType.type, type), (expr.refReturn, _ref), (false, false), true, true);
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
            symbolTable.returnFrameData.returnDatas.Clear();
        }

        public static void ValidateCall(Expr.Call expr, Expr.Function callee)
        {
            if (!expr.constructor && callee.constructor)
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.ConstructorCalledAsMethod, expr.name.location, []));
            }
            else if (expr.constructor && !callee.constructor)
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.MethodCalledAsConstructor, expr.name.location, []));
            }

            if (expr.callee != null)
            {
                if (expr.instanceCall && callee.modifiers["static"])
                {
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.StaticMethodCalledFromInstanceContext, expr.name.location, []));
                }
                if (!expr.instanceCall && !callee.modifiers["static"] && !expr.constructor)
                {
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InstanceMethodCalledFromStaticContext, expr.name.location, []));
                }
            }
            ValidateFunctionParameterModifiers(expr);
        }
        public static void ValidateFunctionParameterModifiers(Expr.Invokable invokable)
        {
            for (int i = 0; i < invokable.internalFunction.Arity; i++)
            {
                if (invokable.internalFunction.parameters[i].modifiers["ref"])
                {
                    if (!(SymbolTableSingleton.SymbolTable.Current is Expr.Function { constructor: true }) 
                        && GetVariableModifiers(invokable.Arguments[i])._readonly 
                        && !invokable.internalFunction.parameters[i].modifiers["readonly"])
                    {
                        Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(
                            Diagnostic.DiagnosticName.ReadonlyFieldModified_Ref,
                            "readonly " + ((Expr.GetReference)invokable.Arguments[i]).GetLastType().ToString(),
                            "ref " + invokable.internalFunction.parameters[i].stack.type.ToString()
                        ));
                    }
                    ValidateRefVariable(invokable.Arguments[i], invokable.internalFunction.parameters[i].stack.type, true, invokable is Expr.Call);
                }
            }
        }
        public static void ValidateRefVariable(Expr expr, Expr.Type type, bool assign, bool call)
        {
            if (!call && expr is Expr.GetReference getRef)
            {
                getRef._ref = true;
            }

            if (CannotBeRef(expr, out _))
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(
                        assign? 
                        Diagnostic.DiagnosticName.InvalidParameterModifier_Ref : 
                        Diagnostic.DiagnosticName.InvalidFunctionModifier_Ref
                    )
                );
            }

            bool _ref2 = IsVariableWithRefModifier(expr);

            if (IsInvalidRef(true, true, _ref2))
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(
                    Diagnostic.DiagnosticName.TypeMismatch,
                    type.ToString(),
                    "ref " + type.ToString()
                ));
            }
        }

        public static Expr.DataType ToDataTypeOrDefault(Expr.Type type)
        {
            if (type is Expr.DataType dataType)
                return dataType;

            if (defualtLiteralTypes.TryGetValue((Parser.LiteralTokenType)type.name.type, out var defaultType))
            {
                return defaultType.Value;
            }
            throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic($"Default literal type of literalTokenType '{type.name.type}' is not defined"));
        }
    }
}
