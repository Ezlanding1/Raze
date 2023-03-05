using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze
{
    public enum ErrorType
    {
        ImpossibleException,
        LexerException,
        ParserException,
        AnalyzerException,
        BackendException,
        CodeGenException
    }

    internal abstract class Errors : Exception
    {
        private Errors(string error) : base(error)
        {

        }

        // An error raised that is impossible to reach, undefined behavior, or otherwise unexpected
        public class ImpossibleError : Errors
        {
            public ImpossibleError(string details)
                : base($"{ErrorType.ImpossibleException}\n{details}")
            {

            }
        }

        // An error raised durning Lexing ( Raze.Lexer )
        public class LexError : Errors
        {
            public LexError(int line, int col, string name, string details)
                : base($"{ErrorType.LexerException}\n{name}: {details}\nLine: {line}, COL: {col + 1}")
            {

            }
        }

        // An error raised during Parsing ( Raze.Parser )
        public class ParseError : Errors
        {
            public ParseError(string name, string details)
                : base($"{ErrorType.ParserException}\n{name}: {details}")
            {

            }
        }

        // An error raised during Analysis ( Raze.Analyzer )
        public class AnalyzerError : Errors
        {
            public AnalyzerError(string name, string details)
                : base($"{ErrorType.AnalyzerException}\n{name}: {details}" + ((!SymbolTableSingleton.SymbolTable.CurrentIsTop()) ? ("\nat:\n\t" + SymbolTableSingleton.SymbolTable.Current.self.QualifiedName) : ""))
            {

            }
        }

        // An error raised during backend ( Raze.Assembler )
        public class BackendError : Errors
        {
            public BackendError(string name, string details)
                : base($"{ErrorType.BackendException}\n{name}: {details}")
            {

            }
        }

        // An error raised during codegen ( Raze.Syntaxes )
        public class CodeGenError : Errors
        {
            public CodeGenError(string name, string details)
                : base($"{ErrorType.CodeGenException}\n{name}: {details}")
            {

            }
        }
    }
}
