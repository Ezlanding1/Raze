using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Espionage
{
    public enum ErrorType
    {
        LexerException,
        ParserException,
        BackendException
    }

    internal static class Errors
    {
        public class LexError : Exception
        {
            public LexError(ErrorType e, int line, int col, string name, string details)
                : base($"{e}, LINE: {line}, COL: {col + 1}, {name}: {details}")
            {

            }
        }

        public class ParseError : Exception
        {
            public ParseError(ErrorType e, string name, string details)
                : base($"{e}, {name}: {details}")
            {

            }
        }

        public class BackendError : Exception
        {
            public BackendError(ErrorType e, string name, string details)
                : base($"{e}, {name}: {details}")
            {

            }
        }
    }
}
