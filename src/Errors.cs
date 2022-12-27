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

    internal static class Errors
    {
        public class LexError : Exception
        {
            public LexError(ErrorType e, int line, int col, string name, string details)
                : base($"{e}\n{name}: {details}\nLine: {line}, COL: {col + 1}")
            {

            }
        }

        public class ParseError : Exception
        {
            public ParseError(ErrorType e, string name, string details)
                : base($"{e}\n{name}: {details}")
            {

            }
        }

        public class BackendError : Exception
        {
            public BackendError(ErrorType e, string name, string details, Analyzer.CallStack? callStack)
                : base(CreateBackend(e, name, details, callStack))
            {

            }
            public BackendError(ErrorType e, string name, string details)
                : base(CreateBackend(e, name, details, null))
            {

            }
        }

        private static string CreateBackend(ErrorType e, string name, string details, Analyzer.CallStack? callStack)
        {
            string str = $"{e}\n{name}: {details}";
            if (callStack == null)
            {
                return str;
            }
            return (str + "\n" + callStack.ToString());
        }
    }
}
