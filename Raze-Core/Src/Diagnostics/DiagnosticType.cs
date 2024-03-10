using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public abstract partial class Diagnostic
{
    public enum DiagnosticType
    {
        Impossible,
        Driver,
        Lexer,
        Parser,
        Analyzer,
        Backend
    }

    public enum Severity
    {
        Exception,
        Error,
        Warning,
        Info
    }
}
