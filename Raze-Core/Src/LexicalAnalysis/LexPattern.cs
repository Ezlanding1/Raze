using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Raze;

public partial class Lexer
{
    private Token LexIdentifier(Match match, Token.TokenType pattern)
    {
        if (TokenList.Reserved.Contains(match.ToString()))
        {
            return new Token(Token.TokenType.RESERVED, match.ToString(), location);
        }
        return new Token(pattern, match.ToString(), location);
    }

    private Token LexString(Match match, Token.TokenType pattern)
    {
        string str = match.ToString();
        if (str.Length == 2)
        {
            Diagnostics.Report(new Diagnostic.LexDiagnostic(Diagnostic.DiagnosticName.EmptyStringLiteral, location, []));
        }
        if (str[^1] != '\'' || str.Length == 1)
        {
            Diagnostics.Report(new Diagnostic.LexDiagnostic(Diagnostic.DiagnosticName.NonTerminatedString, location, [(str.Length <= StringTruncateLength) ? str + "'" : str.Substring(0, StringTruncateLength) + "'..."]));
        }
        return new Token(pattern, Escape(str[1..^1]), location);
    }

    private Token LexRefString(Match match, Token.TokenType pattern)
    {
        string str = match.ToString();
        if (str[^1] != '\"' || str.Length == 1)
        {
            Diagnostics.Report(new Diagnostic.LexDiagnostic(Diagnostic.DiagnosticName.NonTerminatedString, location, [(str.Length <= StringTruncateLength) ? str + "'" : str.Substring(0, StringTruncateLength) + "'..."]));
        }
        return new Token(pattern, Escape(str[1..^1]), location);
    }

    private Token LexFloating(Match match, Token.TokenType pattern)
    {
        string floating = match.ToString();
        if (floating[^1] == '.')
        {
            Diagnostics.Report(new Diagnostic.LexDiagnostic(Diagnostic.DiagnosticName.InvalidFormattedNumber, location, floating));
        }
        return new Token(pattern, floating, location);
    }
}
