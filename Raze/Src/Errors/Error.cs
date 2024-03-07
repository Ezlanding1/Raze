using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public enum ErrorType
{
    ImpossibleException,
    LexerException,
    ParserException,
    AnalyzerException,
    BackendException,
    CodeGenException
}

public abstract class Error
{
    public abstract string ComposeErrorMessage();

    // An error raised that is impossible to reach, undefined behavior, or otherwise unexpected
    public class ImpossibleError : Error
    {
        string details;
        string? stackTrace;

        public ImpossibleError(string details, string? stackTrace)
        {
            this.details = details;
            this.stackTrace = stackTrace;
        }
        public ImpossibleError(string details) : this(details, GetStackTrace())
        {
        }
        public ImpossibleError(Exception exception) : this(exception.Message, exception.StackTrace)
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

        public override string ComposeErrorMessage() => $"{ErrorType.ImpossibleException}\n{details}\n{(Diagnostics.debugErrors? stackTrace : "")}";
    }

    // An error raised durning Lexing ( Raze.Lexer )
    public class LexError : Error
    {
        int line, col;
        string name, details;

        public LexError(int line, int col, string name, string details)
        {
            this.line = line;
            this.col = col;
            this.name = name;
            this.details = details;
        }

        public override string ComposeErrorMessage() => 
            $"{ErrorType.LexerException}\n{name}: {details}\nLine: {line}, Col: {col + 1}";
    }

    // An error raised during Parsing ( Raze.Parser )
    public class ParseError : Error
    {
        string name, details;

        public ParseError(string name, string details)
        {
            this.name = name;
            this.details = details;
        }

        public override string ComposeErrorMessage() => 
            $"{ErrorType.ParserException}\n{name}: {details}";
    }

    // An error raised during Analysis ( Raze.Analyzer )
    public class AnalyzerError : Error
    {
        string name, details, path;

        public AnalyzerError(string name, string details)
        {
            this.name = name;
            this.details = details;
            this.path = "at:\n\t" + SymbolTableSingleton.SymbolTable.Current?.ToString();
        }

        public override string ComposeErrorMessage() =>
            $"{ErrorType.AnalyzerException}\n{name}: {details}\n{path}";
    }

    // An error raised during codegen ( Raze.CodeGen )
    public class BackendError : Error
    {
        string name, details;

        public BackendError(string name, string details)
        {
            this.name = name;
            this.details = details;
        }

        public override string ComposeErrorMessage() =>
            $"{ErrorType.BackendException}\n{name}: {details}";
    }
}
