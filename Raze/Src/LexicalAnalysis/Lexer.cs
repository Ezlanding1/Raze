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
    int line;
    int col;
    int index;

    public Lexer(FileInfo fileName)
    {
        this.tokens = new();
        this.streamReader = new StreamReader(fileName.OpenRead());
        this.line = 1;
        this.col = 0;
        this.index = 0;
        InitRegex();
    }

    public List<Token> Tokenize()
    {
        string line;
        while ((line = streamReader.ReadLine()) != null)
        {
            string lexeme;
            while ((lexeme = line.Substring(index)) != "")
            {
                Token token = Generate(lexeme);
                if (token != null)
                    tokens.Add(token);
            }
            index = 0;
        }
        return tokens;
    }

    private Token Generate(string lexeme)
    {
        foreach (TokenDefinition pattern in tokenDefinitions)
        {
            var match = pattern.Match(lexeme);

            if (match.Success && match.Index == 0)
            {
                index += match.Length;
                col += match.Length;

                if (pattern.type == Token.TokenType.WHITESPACE)
                {
                    if (lexeme[0] == '\n')
                    {
                        line++;
                        col = 0;
                    }
                    return null;
                }
                if (pattern.type == Token.TokenType.IDENTIFIER)
                {
                    if (TokenList.Reserved.Contains(match.ToString()))
                    {
                        return new Token(Token.TokenType.RESERVED, match.ToString());
                    }
                }
                if (pattern.type == Token.TokenType.COMMENT)
                {
                    return null;
                }
                if (pattern.type == Token.TokenType.STRING)
                {
                    string str = match.ToString();
                    if (str.Length == 2)
                    {
                        Diagnostics.ReportError(new Error.LexError(line, col, "Invalid Literal", "Empty String Literal"));
                    }
                    if (str[^1] != '\'' || str.Length == 1)
                    {
                        Diagnostics.ReportError(new Error.LexError(line, col, "Non Terminated String", $"\'{((str.Length <= StringTruncateLength) ? str + "'" : str.Substring(0, StringTruncateLength) + "'...")} was not terminated"));
                    }
                    return new Token(pattern.type, Escape(str[1..^1]));
                }
                if (pattern.type == Token.TokenType.REF_STRING)
                {
                    string str = match.ToString();
                    if (str[^1] != '\"' || str.Length == 1)
                    {
                        Diagnostics.ReportError(new Error.LexError(line, col, "Non Terminated String", $"\'{((str.Length <= StringTruncateLength) ? str + "'" : str.Substring(0, StringTruncateLength) + "'...")} was not terminated"));
                    }
                    return new Token(pattern.type, Escape(str[1..^1]));
                }
                if (pattern.type == Token.TokenType.FLOATING)
                {
                    string floating = match.ToString();
                    if (floating[^1] == '.')
                    {
                        Diagnostics.ReportError(new Error.LexError(line, col, "Invalid Formatted Number", $"'{floating}' is incorectly formatted"));
                    }
                    return new Token(pattern.type, floating);
                }
                if (pattern.type == Token.TokenType.HEX || pattern.type == Token.TokenType.BINARY)
                {
                    return new Token(pattern.type, match.ToString()[2..]);
                }
                return new Token(pattern.type, match.ToString());
            }
        }

        Diagnostics.ReportError(new Error.LexError(line, col, "Illegal Char Error", $"Character '{lexeme[0]}' is Illegal"));
        index++;
        return null;
    }

    private string Escape (string str)
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
