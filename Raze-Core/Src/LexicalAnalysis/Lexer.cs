using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Raze;

public partial class Lexer
{
    const int StringTruncateLength = 40;

    List<Token> tokens;
    TokenDefinition[] tokenDefinitions;
    public static (Regex, char)[] stringEscapeCodes;
    StreamReader streamReader;
    Location location;

    public Lexer(FileInfo fileName)
    {
        this.tokens = new();
        this.streamReader = new StreamReader(fileName.OpenRead());
        location = new(1, 1);
        InitRegex();
    }

    public List<Token> Tokenize()
    {
        string? line;
        while ((line = streamReader.ReadLine()) != null)
        {
            while (location.Idx < line.Length)
            {
                Token? token = Generate(line[location.Idx..]);
                if (token != null)
                    tokens.Add(token);
            }
            location = new(location.Ln + 1, 1);
        }
        return tokens;
    }

    private Token? Generate(string lexeme)
    {
        Match match = Match.Empty;

        var tokenDefinition = tokenDefinitions.FirstOrDefault(
            pattern => 
            {
                match = pattern.Match(lexeme);
                return match.Success && match.Index == 0;
            }
        );

        if (tokenDefinition == null)
        {
            Diagnostics.Report(new Diagnostic.LexDiagnostic(Diagnostic.DiagnosticName.IllegalCharError, location, lexeme[0]));
            location = new(location.Ln, location.Col + 1);
            return null;
        }

        var pattern = tokenDefinition.type;
        Token? token = pattern switch
        {
            Token.TokenType.WHITESPACE or
            Token.TokenType.COMMENT => null,

            Token.TokenType.IDENTIFIER => LexIdentifier(match, pattern),
            Token.TokenType.STRING => LexString(match, pattern),
            Token.TokenType.REF_STRING => LexRefString(match, pattern),
            Token.TokenType.FLOATING => LexFloating(match, pattern),

            _ => new Token(pattern, match.ToString(), location)
        };

        location = new(location.Ln, location.Col + match.Length);
        return token;
    }

    private static string Escape(string str)
    {
        foreach ((Regex, char) regex in stringEscapeCodes)
        {
            str = regex.Item1.Replace(str, regex.Item2.ToString());
        }
        return str;
    }

    private void InitRegex()
    {
        InitEscape();

        tokenDefinitions = new TokenDefinition[TokenList.Tokens.Count];
        int count = 0;
        foreach (var key in TokenList.Tokens.Keys)
        {
            tokenDefinitions[count] = new TokenDefinition(key, TokenList.Tokens[key]);
            count++;
        }
    }
    
    private void InitEscape()
    {
        // Intializes the Regexes for escape characters
        // In accordance to the escape character standard: https://www.ibm.com/docs/en/zos/2.4.0?topic=set-escape-sequences

        stringEscapeCodes =
        [
            (TokenList.Patterns.EscapeBackslash(), '\\'),
            (TokenList.Patterns.EscapeA(), '\a'),
            (TokenList.Patterns.EscapeB(), '\b'),
            (TokenList.Patterns.EscapeF(), '\f'),
            (TokenList.Patterns.EscapeN(), '\n'),
            (TokenList.Patterns.EscapeR(), '\r'),
            (TokenList.Patterns.EscapeT(), '\t'),
            (TokenList.Patterns.EscapeV(), '\v'),
            (TokenList.Patterns.EscapeSingleQuote(), '\''),
            (TokenList.Patterns.EscapeDoubleQuote(), '\"'),
            (TokenList.Patterns.EscapeQuestionMark(), '?'),
            (TokenList.Patterns.Escape0(), '\0'),
        ];
    }
}

// Holds a token's regex and literal
class TokenDefinition(Token.TokenType type, Regex pattern)
{
    public Token.TokenType type = type;
    public Regex regex = pattern;

    public Match Match(string str)
    {
        return regex.Match(str);
    }
}
