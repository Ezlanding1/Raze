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

internal abstract class Error : Exception
{
    public Error() : base()
    {

    }
    public abstract string ComposeErrorMessage();

    // An error raised that is impossible to reach, undefined behavior, or otherwise unexpected
    public class ImpossibleError : Error
    {
        string details;

        public ImpossibleError(string details)
        {
            this.details = details;
        }

        public override string ComposeErrorMessage() => $"{ErrorType.ImpossibleException}\n{details}";
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
            $"{ErrorType.LexerException}\n{name}: {details}\nLine: {line}, COL: {col + 1}";
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

    // An error raised during backend ( Raze.Assembler )
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

    // An error raised during codegen ( Raze.Syntaxes )
    public class CodeGenError : Error
    {
        string name, details;

        public CodeGenError(string name, string details)
        {
            this.name = name;
            this.details = details;
        }

        public override string ComposeErrorMessage() =>
            $"{ErrorType.BackendException}\n{name}: {details}";
    }
}
