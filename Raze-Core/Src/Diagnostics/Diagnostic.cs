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

    private protected Diagnostic(DiagnosticName name, params object?[] info)
    {
        this.name = name;
        this.details = string.Format(DiagnosticInfo[name].details, info);
    }

    internal string ComposeDiagnosticMessage() =>
        GetDiagnosticHeader(this) + "\n" +
        GetDiagnosticMessage();

    // A diagnostic raised that is impossible to reach, undefined behavior, or otherwise unexpected
    public class ImpossibleDiagnostic : Diagnostic
    {
        Location location = Location.NoLocation;
        string? stackTrace;

        public ImpossibleDiagnostic(string details, string? stackTrace) : base(DiagnosticName.Impossible, details)
        {
            this.stackTrace = stackTrace;
        }
        public ImpossibleDiagnostic(string details) : this(details, GetStackTrace())
        {
        }
        public ImpossibleDiagnostic(Exception exception) : this(exception.Message, exception.StackTrace)
        {
        }
        public ImpossibleDiagnostic(Location location, string details) : this(details)
        {
            this.location = location;
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
            $"{DiagnosticType.Impossible}{SeverityLevel}\n{details}{(Diagnostics.debugErrors? "\n" + stackTrace : "")}\n{GetLocation(location)}";
    }

    // A diagnostic raised during Driver ( Raze_Driver )
    public class DriverDiagnostic : Diagnostic
    {
        public DriverDiagnostic(DiagnosticName name, params object?[] info) : base(name, info)
        {
        }

        internal override string GetDiagnosticMessage() =>
            $"{DiagnosticType.Driver}{SeverityLevel}\n{details}";
    }

    // A diagnostic raised during Lexing ( Raze.Lexer )
    public class LexDiagnostic : Diagnostic
    {
        Location location;

        public LexDiagnostic(DiagnosticName name, Location location, params object?[] info) : base(name, info)
        {
            this.location = location;
        }

        internal override string GetDiagnosticMessage() => 
            $"{DiagnosticType.Lexer}{SeverityLevel}\n{details}\n{GetFileNameInfo()}\n{GetLocation(location)}";
    }

    // An diagnostic raised during Parsing ( Raze.Parser )
    public class ParseDiagnostic : Diagnostic
    {
        Location location = Location.NoLocation;

        public ParseDiagnostic(DiagnosticName name, params object?[] info) : base(name, info)
        {
        }
        public ParseDiagnostic(DiagnosticName name, Location location, params object?[] info) : base(name, info)
        {
            this.location = location;
        }

        internal override string GetDiagnosticMessage() => 
            $"{DiagnosticType.Parser}{SeverityLevel}\n{details}\n{GetFileNameInfo()}\n{GetLocation(location)}";
    }

    // An diagnostic raised during Analysis ( Raze.Analyzer )
    public class AnalyzerDiagnostic : Diagnostic
    {
        string path = "";
        Location location = Location.NoLocation;

        public AnalyzerDiagnostic(DiagnosticName name, params object?[] info) : base(name, info)
        {
            if (SymbolTableSingleton.SymbolTable.Current is not null)
            {
                this.path = "\nat:\n\t" + SymbolTableSingleton.SymbolTable.Current.ToString();
            }
        }
        public AnalyzerDiagnostic(DiagnosticName name, Location location, params object?[] info) : this(name, info)
        {
            this.location = location;
        }

        internal override string GetDiagnosticMessage() =>
            $"{DiagnosticType.Analyzer}{SeverityLevel}\n{details}{path}\n{GetFileNameInfo()}\n{GetLocation(location)}";
    }

    // An diagnostic raised during codegen ( Raze.CodeGen )
    public class BackendDiagnostic : Diagnostic
    {
        Location location = Location.NoLocation;
        public BackendDiagnostic(DiagnosticName name, params object?[] info) : base(name, info)
        {
        }
        public BackendDiagnostic(DiagnosticName name, Location location, params object?[] info) : this(name, info)
        {
            this.location = location;
        }

        internal override string GetDiagnosticMessage() =>
            $"{DiagnosticType.Backend}{SeverityLevel}\n{details}\n{GetFileNameInfo()}\n{GetLocation(location)}";
    }
}
