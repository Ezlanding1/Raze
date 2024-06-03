using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Analyzer
{
    internal class SymbolTable
    {
        private Expr.Definition? current = null;
        public Expr.Definition? Current { get => current; private set => current = value; }

        public Expr.Function? main = null;

        public Expr.Import.FileInfo currentFileInfo = new(Diagnostics.mainFile);
        public List<Expr.Definition> globals => imports[currentFileInfo].globals;

        private List<(Token, Expr.StackData)> locals = new();
        public Stack<int> framePointer = new();
        public Dictionary<Expr.StackData, FrameData> frameData = new();
        public ReturnFrameData returnFrameData = new();

        private static Expr.StackData VarNotFoundData = new(TypeCheckUtils.anyType, false);
        private static Expr.Class ClassNotFoundDefinition = TypeCheckUtils.anyType;
        public Expr.Function FunctionNotFoundDefinition = SpecialObjects.GenerateAnyFunction();

        Dictionary<Expr.Import.FileInfo, ImportData> imports = new();
        public bool IsImport => currentFileInfo != new Expr.Import.FileInfo(Diagnostics.mainFile);

        public class ImportData(Expr.Class importClass, List<Expr> expressions, List<Expr.Definition> globals)
        {
            public readonly Expr.Class importClass = importClass; 
            public readonly List<Expr> expressions = expressions; 
            public readonly List<Expr.Definition> globals = globals;
        }

        public void SetContext(Expr.Definition? current)
        {
            this.current = current;
        }

        public ImportData GetMainImportData() => imports[new(Diagnostics.mainFile)];

        public void IterateImports(params Action<ImportData>[] actions)
        {
            foreach (var action in actions)
            {
                foreach (var import in imports)
                {
                    currentFileInfo = import.Key;
                    action.Invoke(import.Value);
                }
            }
        }

        public void AddMainImport(List<Token> tokens)
        {
            var import = new Expr.Import(new(Diagnostics.mainFile), true, new(new(null), false));

            var importClass = SpecialObjects.GenerateImportToplevelWrapper(import);
            imports[import.fileInfo] = new(importClass, null, NewGlobals());
            
            Parser parser = new Parser(tokens);
            List<Expr> expressions = parser.ParseImport();

            SpecialObjects.AddExprsToImportToplevelWrapper(importClass, expressions);
            imports[import.fileInfo] = new(importClass, expressions, this.globals);
        }

        public void AddImport(Expr.Import import)
        {
            if (!import.fileInfo.Exists) return;

            Expr.Class importClass = imports.TryGetValue(import.fileInfo, out var value)?
                value.importClass : 
                ParseImport(import);

            AddImportGlobalWrapper(import, importClass);
        }

        private void AddImportGlobalWrapper(Expr.Import import, Expr.Class importClass)
        {
            using (new SaveContext())
            {
                SetContext(importClass);
                if (import.importType.typeRef.typeName != null)
                {
                    InitialPass.HandleTypeNameReference(import.importType.typeRef.typeName);
                }
                import.importType.typeRef.type = (Expr.DataType)Current;
            }

            if (import.importType.importAll)
            {
                foreach (Expr.Definition definition in import.importType.typeRef.type.definitions)
                {
                    AddGlobal(definition);
                }
            }
            else
            {
                AddGlobal(import.importType.typeRef.type);
            }
        }

        private Expr.Class ParseImport(Expr.Import import)
        {
            var importClass = SpecialObjects.GenerateImportToplevelWrapper(import);
            imports[import.fileInfo] = new(importClass, null, NewGlobals());
            using (new SaveImportData(currentFileInfo))
            {
                currentFileInfo = import.fileInfo;

                Lexer lexer = new Lexer(import.fileInfo._fileInfo);
                var tokens = lexer.Tokenize();

                Parser parser = new Parser(tokens);
                List<Expr> expressions = parser.ParseImport();

                SpecialObjects.AddExprsToImportToplevelWrapper(importClass, expressions);
                imports[import.fileInfo] = new(importClass, expressions, this.globals);
                return importClass;
            }
        }

        private static List<Expr.Definition> NewGlobals() => [TypeCheckUtils.objectType];

        public void AddVariable(Token name, Expr.StackData variable, bool initializedOnDeclaration)
        {
            if (current is Expr.Function)
            {
                locals.Add(new(name, variable));
                if (!initializedOnDeclaration)
                    frameData[variable] = new FrameData();
            }
        }

        public void AddParameter(Token name, Expr.StackData parameter)
        {
            if (current is Expr.Function)
            {
                locals.Add(new(name, parameter));
            }
        }

        public void AddDefinition(Expr.Definition definition)
        {
            definition.enclosing = Current;
            SetContext(definition);
        }
        public void AddDefinition(Expr.DataType definition)
        {
            AddDefinition((Expr.Definition)definition);
            CheckDuplicates(definition.definitions);
        }

        public void CreateBlock() => framePointer.Push(locals.Count);

        public void RemoveBlock()
        {
            for (int i = locals.Count-1; i > framePointer.Peek(); i--)
            {
                frameData.Remove(locals[i].Item2);
            }
            locals.RemoveRange(framePointer.Peek(), locals.Count - framePointer.Pop());
        }

        // 'GetVariable' Methods:

        private Expr.StackData? _GetVariable(Token key, out bool isClassScoped, bool ignoreEnclosing)
        {
            if (current == null)
            {
                isClassScoped = false;
                return null;
            }

            if (current is Expr.Function)
            {
                var variableIdx = locals.FindLastIndex(x => key.lexeme == x.Item1.lexeme);

                if (variableIdx != -1)
                {
                    isClassScoped = false;
                    return locals[variableIdx].Item2;
                }
            }

            if (!ignoreEnclosing && !(current is Expr.Function && (((Expr.Function)current).modifiers["static"])))
            {
                var x = NearestEnclosingClass();
                switch (x)
                {
                    case Expr.Class:
                        {
                            if (TryGetValue(((Expr.Class)x).declarations, key, out var value))
                            {
                                isClassScoped = true;
                                return value.stack;
                            }
                            if (key.lexeme == "this")
                            {
                                isClassScoped = false;
                                return x._this;
                            }
                        }
                        break;
                    case Expr.Primitive:
                        {
                            if (key.lexeme == "this")
                            {
                                isClassScoped = false;
                                return x._this;
                            }
                        }
                        break;
                    case null:
                        break;
                }
            }
            isClassScoped = false;
            return null;
        }
        public Expr.StackData GetVariable(Token key, out bool isClassScoped, bool ignoreEnclosing=false, bool assigns=false)
        {
            var variable = _GetVariable(key, out isClassScoped, ignoreEnclosing);

            if (variable == null)
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.UndefinedReference, "variable", key.lexeme));
                return VarNotFoundData;
            }

            if (frameData.TryGetValue(variable, out FrameData? value))
            {
                if (!assigns && !value.initialized)
                {
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.VariableUsedBeforeInitialization, key.lexeme));
                }
                else if (assigns) 
                {
                    frameData[variable].initialized = true;
                }
            }

            return variable;
        }
        public Expr.StackData GetVariable(Token key, bool assigns=false)
        {
            return GetVariable(key, out _, false, assigns);
        }
        public bool TryGetVariable(Token key, out Expr.StackData symbol, out bool isClassScoped, bool ignoreEnclosing=false)
        {
            return (symbol = _GetVariable(key, out isClassScoped, ignoreEnclosing)) != null;
        }

        public bool IsLocallyScoped(string key) =>
            locals.FindLastIndex(x => key == x.Item1.lexeme) != -1;

        // 'GetDefinition' Methods:

        private Expr.Definition? _GetDefinition(Token key)
        {
            if (current == null)
            {
                if (TryGetValue(globals, key, out var globalValue))
                {
                    return globalValue;
                }
                return null;
            }

            if (current is Expr.Function)
            {
                Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("Requested function's definitions"));
            }

            if (TryGetValue(((Expr.DataType)current).definitions, key, out var value))
            {
                return value;
            }

            return null;
        }
        public Expr.Definition GetClass(Token key)
        {
            var _class = _GetDefinition(key);

            if (_class == null)
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.UndefinedReference, "class", key.lexeme));
                return new SpecialObjects.Any(key);
            }
            else if (_class is Expr.Function)
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.UndefinedReference, "function", key.lexeme));
                return ClassNotFoundDefinition;
            }
            return _class;
        }
        public bool TryGetDefinition(Token key, out Expr.Definition symbol)
        {
            return (symbol = _GetDefinition(key)) != null;
        }

        // 'GetDefinitionFullScope' methods

        public Expr.Definition? _GetDefinitionFullScope(Token key)
        {
            Expr.Definition? definition = null;

            Expr.Type? x = NearestEnclosingClass();

            while (x != null)
            {
                if (TryGetValue(((Expr.DataType)x).definitions, key, out var xValue))
                {
                    definition = xValue;
                }
                x = x.enclosing;
            }

            if (TryGetValue(globals, key, out var value))
            {
                definition = value;
            }
            
            return definition;
        }
        public Expr.Definition GetClassFullScope(Token key)
        {
            Expr.Definition? _class = _GetDefinitionFullScope(key);

            if (_class == null)
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.UndefinedReference, "class", key.lexeme));
                return new SpecialObjects.Any(key);
            }
            else if (_class is Expr.Function)
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.UndefinedReference, "function", key.lexeme));
                return ClassNotFoundDefinition;
            }
            return _class;
        }
        public bool TryGetDefinitionFullScope(Token key, out Expr.Definition symbol)
        {
            return (symbol = _GetDefinitionFullScope(key)) != null;
        }

        // 'GetFunction' Methods:

        private Expr.Function? _GetFunction(string key, Expr.Type[] types)
        {
            if (current == null || NearestEnclosingClass() == null)
            {
                if (TryGetFuncValue(globals, key, types, out var globalValue))
                {
                    return globalValue;
                }
                return null;
            }

            if (TryGetFuncValue(NearestEnclosingClass().definitions, key, types, out var value))
            {
                return value;
            }

            return null;
        }

        public Expr.Function GetFunction(string key, Expr.Type[] types)
        {
            Expr.Function? function = _GetFunction(key, types);

            if (function == null)
            {
                return FunctionSearchFail(key, types);
            }
            return function;
        }
        public bool TryGetFunction(string key, Expr.Type[] types, out Expr.Function symbol)
        {
            return (symbol = _GetFunction(key, types)) != null;
        }
        public Expr.Function FunctionSearchFail(string key, Expr.Type[] types)
        {
            if (!types.Any(x => x == TypeCheckUtils.anyType))
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.UndefinedReference, "function", Expr.Call.CallNameToString(key, types)));
            }
            return FunctionNotFoundDefinition;
        }

        public Expr.DataType? NearestEnclosingClass(Expr.Definition definition)
        {
            // Assumes a function is enclosed by a class (no nested functions)
            return (definition is Expr.Function function) ? (Expr.DataType)definition.enclosing : (Expr.DataType)definition;
        }
        public Expr.DataType? NearestEnclosingClass()
        {
            return NearestEnclosingClass(current);
        }


        public void UpContext()
        {
            if (current == null)
                Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("Up Context Called On 'GLOBAL' context (no enclosing)"));

            current = (Expr.Definition)current.enclosing;
        }

        public bool CurrentIsTop() => current == null;

        public void AddGlobal(Expr.Definition? definition)
        {
            if (definition is not null) 
                globals.Add(definition);
        }

        public void CheckGlobals() { CheckDuplicates(globals); }
        public void CheckDuplicates(List<Expr.Definition> definitions)
        {
            foreach (var duplicate in definitions.GroupBy(ToUniqueName).Where(x => x.Count() > 1).Select(x => x.ElementAt(0)))
            {
                if (duplicate is Expr.Function)
                {
                    if (duplicate.name.lexeme == "Main")
                    {
                        Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.MainDoubleDeclaration));
                    }

                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.DoubleDeclaration, "function", duplicate));
                }
                else if (duplicate is Expr.Primitive)
                {
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.DoubleDeclaration, "primitive class", duplicate.name.lexeme));
                }
                else
                {
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.DoubleDeclaration, "class", duplicate.name.lexeme));
                }
            }
        }
        public void CheckDuplicates(List<Expr.Declare> declarations)
        {
            foreach (var duplicate in declarations.GroupBy(x => x.name.lexeme).Where(x => x.Count() > 1).Select(x => x.ElementAt(0)))
            {
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.DoubleDeclaration, "variable", duplicate.name.lexeme));
            }
        }

        private string ToUniqueName(Expr.Definition definition)
        {
            if (definition is Expr.Function function)
            {
                return (function.enclosing != null ?
                        function.enclosing.ToString() + "." :
                        "")
                        + function.name.lexeme + getParameters();

                string getParameters()
                {
                    string res = "";
                    if (function.parameters.Count != 0)
                    {
                        foreach (var type in function.parameters)
                        {
                            if (type.typeName.Count == 0)
                            {
                                res += (type.stack.type);
                            }
                            else
                            {
                                res += (string.Join(".", type.typeName.ToList().ConvertAll(x => x.lexeme)) + ", ");
                            }
                        }
                    }
                    return res;
                }
            }
            else
            {
                return definition.name.lexeme;
            }
        }

        private bool TryGetValue<T>(List<T> list, Token key, out T value)  where T : Expr.Named
        {
            foreach (var item in list)
            {
                if (item.name.lexeme == key.lexeme)
                {
                    value = item;
                    return true;
                }
            }
            value = null;
            return false;
        }
        private bool TryGetFuncValue(List<Expr.Definition> list, string key, Expr.Type[] types, out Expr.Function value)
        {
            value = null;
            foreach (var item in list)
            {
                if (item.name.lexeme == key)
                {
                    if (item is not Expr.Function function)
                    {
                        Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.InvalidCall, item));
                        return false;
                    }
                    if (ParamMatch(types, function))
                    {
                        if (value == null)
                        { 
                            value = function;
                        }
                        else
                        {
                            Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.AmbiguousCall, value, function));
                            return false;
                        }
                    }
                }
            }

            return value != null;
        }

        private static bool ParamMatch(Expr.Type[] a, Expr.Function b)
        {
            if (a.Length != b.Arity)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (!a[i].Matches(b.parameters[i].stack.type))
                {
                    return false;
                }
            }
            return true;
        }

        public static bool MatchFunction(Expr.Function function1, Expr.Function function2) =>
            function1.name.lexeme == function2.name.lexeme && ParamMatch(function1.parameters.Select(x => x.stack.type).ToArray(), function2);

        public class FrameData
        {
            public bool initialized;

            public FrameData() { }
            public FrameData(bool initializedOnDeclaration)
            {
                initialized = initializedOnDeclaration;
            }
        }

        public class ReturnFrameData : FrameData
        {
            public bool initializedOnAnyBranch;
            public List<Expr.Type?> returnTypes = new();

            public void Initialized(Expr.Type? returnType)
            {
                initialized = true;
                initializedOnAnyBranch = true;
                returnTypes.Add(returnType);
            }
        }

        public void SetFrameDataStates(IEnumerable<bool> states)
        {
            for (int i = 0; i < locals.Count && frameData.ContainsKey(locals[i].Item2); i++)
            {
                frameData[locals[i].Item2].initialized = states.ElementAt(i);
            }
            returnFrameData.initialized = states.Last();
        }
        
        public IEnumerable<bool> GetFrameData()
        {
            foreach (var local in locals.Select(x => x.Item2))
            {
                if (frameData.TryGetValue(local, out FrameData? value))
                {
                    yield return value.initialized;
                }
            }
            yield return returnFrameData.initialized;
        }

        public void ResolveStates(List<bool> states)
        {
            SymbolTable symbolTable = SymbolTableSingleton.SymbolTable;

            for (int i = 0; i < symbolTable.locals.Count && symbolTable.frameData.ContainsKey(symbolTable.locals[i].Item2); i++)
            {
                states[i] &= symbolTable.frameData[symbolTable.locals[i].Item2].initialized;
                symbolTable.frameData[symbolTable.locals[i].Item2].initialized = false;
            }
            states[^1] &= symbolTable.returnFrameData.initialized;
            symbolTable.returnFrameData.initialized = false;
        }
    }
}
