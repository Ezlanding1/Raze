using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze
{
    public enum ErrorType
    {
        LexerException,
        ParserException,
        BackendException
    }

    internal abstract class Errors : Exception
    {
        private Errors(string error) : base(error)
        {

        }

        public class LexError : Errors
        {
            public LexError(int line, int col, string name, string details)
                : base($"{ErrorType.LexerException}\n{name}: {details}\nLine: {line}, COL: {col + 1}")
            {

            }
        }

        public class ParseError : Errors
        {
            public ParseError(string name, string details)
                : base($"{ErrorType.ParserException}\n{name}: {details}")
            {

            }
        }

        public class BackendError : Errors
        {
            public BackendError(string name, string details)
                : base(CreateBackend(ErrorType.BackendException, name, details))
            {

            }
        }

        private static string CreateBackend(ErrorType e, string name, string details)
        {
            string str = $"{e}\n{name}: {details}";
            return (str + "\n" + ((SymbolTableSingleton.SymbolTable.Current.self.QualifiedName != "")? ("at:\n\t" + SymbolTableSingleton.SymbolTable.Current.self.QualifiedName) : ""));
        }
    }
}
