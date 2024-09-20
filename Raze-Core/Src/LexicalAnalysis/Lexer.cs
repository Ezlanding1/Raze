using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Raze;

public class Lexer
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

            Token.TokenType.HEX or
            Token.TokenType.BINARY => new Token(pattern, match.ToString()[2..], location),

            Token.TokenType.UNSIGNED_INTEGER => new Token(pattern, match.ToString()[..^1], location),

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

        stringEscapeCodes = new (Regex, char)[12];

        stringEscapeCodes[0] = (new Regex(@"\\\\"), '\\');
        stringEscapeCodes[1] = (new Regex(@"\\a"), '\a');
        stringEscapeCodes[2] = (new Regex(@"\\b"), '\b');
        stringEscapeCodes[3] = (new Regex(@"\\f"), '\f');
        stringEscapeCodes[4] = (new Regex(@"\\n"), '\n');
        stringEscapeCodes[5] = (new Regex(@"\\r"), '\r');
        stringEscapeCodes[6] = (new Regex(@"\\t"), '\t');
        stringEscapeCodes[7] = (new Regex(@"\\v"), '\v');
        stringEscapeCodes[8] = (new Regex(@"\\'"), '\'');
        stringEscapeCodes[9] = (new Regex(@"\\\"""), '\"');
        stringEscapeCodes[10] =  (new Regex(@"\\\?"), '?');
        stringEscapeCodes[11] =  (new Regex(@"\\0"), '\0');
    }
}

// Holds a token's regex and literal
class TokenDefinition
{
    public Token.TokenType type;
    public Regex regex;

    public TokenDefinition(Token.TokenType type, string pattern)
    {
        this.type = type;
        regex = new Regex(pattern);
    }

    public Match Match(string str)
    {
        return regex.Match(str);
    }
}
