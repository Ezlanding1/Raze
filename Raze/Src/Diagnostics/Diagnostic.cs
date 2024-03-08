using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public abstract partial class Diagnostic
{
    internal DiagnosticName name;
    internal string details;
    internal Severity SeverityLevel => DiagnosticInfo[name].severity;

    internal abstract string GetDiagnosticMessage();

    private protected Diagnostic(DiagnosticName name, params object[] info)
    {
        this.name = name;
        this.details = string.Format(DiagnosticInfo[name].details, info);
    }
    
    internal string ComposeDiagnosticMessage() => GetDiagnosticHeader(this) + "\n" + GetDiagnosticMessage();

    // A diagnostic raised that is impossible to reach, undefined behavior, or otherwise unexpected
    public class ImpossibleDiagnostic : Diagnostic
    {
        string? stackTrace;

        public ImpossibleDiagnostic(string details, string? stackTrace) : base(DiagnosticName.Impossible)
        {
            this.details = details;
            this.stackTrace = stackTrace;
        }
        public ImpossibleDiagnostic(string details) : this(details, GetStackTrace())
        {
        }
        public ImpossibleDiagnostic(Exception exception) : this(exception.Message, exception.StackTrace)
        {
        }

        private static string GetStackTrace()
        {
            return new string(
                Environment.StackTrace
                    .SkipWhile(x => x != '\n').Skip(1)
                    .SkipWhile(x => x != '\n').Skip(1)
                    .SkipWhile(x => x != '\n').Skip(1)
                    .ToArray()
            );
        }

        internal override string GetDiagnosticMessage() => 
            $"{DiagnosticType.Impossible}{Severity.Error}\n{details}\n{(Diagnostics.debugErrors? stackTrace : "")}";
    }

    // A diagnostic raised durning Lexing ( Raze.Lexer )
    public class LexDiagnostic : Diagnostic
    {
        int line, col;

        public LexDiagnostic(DiagnosticName name, object[] info, int line, int col) : base(name, info)
        {
            this.line = line;
            this.col = col;
        }

        internal override string GetDiagnosticMessage() => 
            $"{DiagnosticType.Lexer}{SeverityLevel}\n{details}\nLine: {line}, Col: {col + 1}";
    }

    // An diagnostic raised during Parsing ( Raze.Parser )
    public class ParseDiagnostic : Diagnostic
    {
        public ParseDiagnostic(DiagnosticName name, params object[] info) : base(name, info)
        {
        }

        internal override string GetDiagnosticMessage() => 
            $"{DiagnosticType.Parser}{SeverityLevel}\n{details}";
    }

    // An diagnostic raised during Analysis ( Raze.Analyzer )
    public class AnalyzerDiagnostic : Diagnostic
    {
        string path;

        public AnalyzerDiagnostic(DiagnosticName name, params object[] info) : base(name, info)
        {
            this.path = "at:\n\t" + SymbolTableSingleton.SymbolTable.Current?.ToString();
        }

        internal override string GetDiagnosticMessage() =>
            $"{DiagnosticType.Analyzer}{SeverityLevel}\n{details}\n{path}";
    }

    // An diagnostic raised during codegen ( Raze.CodeGen )
    public class BackendDiagnostic : Diagnostic
    {
        public BackendDiagnostic(DiagnosticName name, params object[] info) : base(name, info)
        {
        }

        internal override string GetDiagnosticMessage() =>
            $"{DiagnosticType.Backend}{SeverityLevel}\n{details}";
    }
}
