using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Analyzer
{
    internal abstract class RuntimeLibrarySingleton<T>(string import, ExprUtils.QueueList<Token> name)
            where T : Expr.Definition
    {
        private protected string import = import;
        private protected ExprUtils.QueueList<Token> name = name;

        public RuntimeLibrarySingleton(string import, string name) : this(import, new ExprUtils.QueueList<Token>() { new(Token.TokenType.IDENTIFIER, name, Location.NoLocation) })
        {
        }
        public RuntimeLibrarySingleton(string import, List<string> name) : this(import, new ExprUtils.QueueList<Token>(name.Select(x => new Token(Token.TokenType.IDENTIFIER, x, Location.NoLocation)).ToList()))
        {
        }

        private protected static bool SetupRuntimeImportContext(string importName, ExprUtils.QueueList<Token> name)
        {
            var fileInfo = SymbolTable.runtimeImports[importName].fileInfo;
            SymbolTableSingleton.SymbolTable.currentFileInfo = fileInfo;

            var import = SymbolTableSingleton.SymbolTable.GetRuntimeImport(fileInfo);

            if (import == null)
                return false;

            SymbolTableSingleton.SymbolTable.SetContext(import.importClass);

            if (name.Count != 0)
            {
                InitialPass.HandleTypeNameReference(name);
            }

            return true;
        }

        public abstract T? Value { get; }
        private protected T? _value = null;
    }

    internal class RuntimeLibrarySingletonDataType : RuntimeLibrarySingleton<Expr.DataType>
    {
        public RuntimeLibrarySingletonDataType(string import, ExprUtils.QueueList<Token> name) : base(import, name)
        {
        }
        public RuntimeLibrarySingletonDataType(string import, string name) : base(import, name)
        {
        }
        public RuntimeLibrarySingletonDataType(string import, List<string> name) : base(import, name)
        {
        }

        public override Expr.DataType? Value => _value ??= GetRuntimeTypeFromEnvironment(import, name);

        private static Expr.DataType? GetRuntimeTypeFromEnvironment(string importName, ExprUtils.QueueList<Token> name)
        {
            using (new SaveContext()) using (new SaveImportData(SymbolTableSingleton.SymbolTable.currentFileInfo))
            {
                if (!SetupRuntimeImportContext(importName, name))
                    return null;

                Expr.DataType result = (Expr.DataType)SymbolTableSingleton.SymbolTable.Current!;

                if (result is SpecialObjects.Any)
                {
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.RequiredRuntimeTypeNotFound, name, importName));
                }
                return result;
            }
        }
    }

    internal class RuntimeLibrarySingletonFunction : RuntimeLibrarySingleton<Expr.Function>
    {
        string functionName;
        RuntimeLibrarySingletonDataType[] argumentTypes;

        public RuntimeLibrarySingletonFunction(string import, ExprUtils.QueueList<Token> name, string functionName, RuntimeLibrarySingletonDataType[] argumentTypes) : base(import, name)
        {
            this.functionName = functionName;
            this.argumentTypes = argumentTypes;
        }
        public RuntimeLibrarySingletonFunction(string import, string name, string functionName, RuntimeLibrarySingletonDataType[] argumentTypes) : base(import, name)
        {
            this.functionName = functionName;
            this.argumentTypes = argumentTypes;
        }
        public RuntimeLibrarySingletonFunction(string import, List<string> name, string functionName, RuntimeLibrarySingletonDataType[] argumentTypes) : base(import, name)
        {
            this.functionName = functionName;
            this.argumentTypes = argumentTypes;
        }
        public RuntimeLibrarySingletonFunction(string import, string functionName, RuntimeLibrarySingletonDataType[] argumentTypes) : base(import, new List<string>())
        {
            this.functionName = functionName;
            this.argumentTypes = argumentTypes;
        }

        public override Expr.Function? Value => _value ??= GetRuntimeFunctionFromEnvironment(import, name);

        private Expr.Function? GetRuntimeFunctionFromEnvironment(string importName, ExprUtils.QueueList<Token> name)
        {
            using (new SaveContext()) using (new SaveImportData(SymbolTableSingleton.SymbolTable.currentFileInfo))
            {
                if (!SetupRuntimeImportContext(importName, name))
                    return null;
                
                Expr.Function result = SymbolTableSingleton.SymbolTable.GetFunction(new(Token.TokenType.IDENTIFIER, functionName, Location.NoLocation), argumentTypes.Select(x => x.Value).ToArray());

                if (result == SymbolTableSingleton.SymbolTable.FunctionNotFoundDefinition)
                {
                    Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.RequiredRuntimeTypeNotFound, functionName, importName));
                }

                return result;
            }
        }
    }
}