using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Raze;

internal class Lexer
{
    const int StringTruncateLength = 40;

    List<Token> tokens;
    TokenDefinition[] tokenDefinitions;
    (Regex, string)[] regexes;
    string text;
    int line;
    int col;
    int index;

    public Lexer(string text)
    {
        this.tokens = new();
        this.text = text;
        this.line = 1;
        this.col = 0;
        this.index = 0;
        InitRegex();
    }

    internal List<Token> Tokenize()
    {
        string lexeme;
        while ((lexeme = text.Substring(index)) != "")
        {
            Token token = Generate(lexeme);
            if (token != null)
                tokens.Add(token);
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

                if (ExceedsMaxSize(pattern.type, match.ToString()))
                {
                    Diagnostics.errors.Push(new Error.LexError(line, col, "Invalid Literal", $"Size of literal '{match.ToString()}' exceeds max size of literal (8 bytes)"));
                    return new Token(pattern.type, "000");
                }

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
                    Escape(ref str);
                    Console.WriteLine(str);
                    if (str[^1] != '\"' || str.Length == 1)
                    {
                        Diagnostics.errors.Push(new Error.LexError(line, col, "Non Terminated String", $"\'{((str.Length <= StringTruncateLength) ? str + "'" : str.Substring(0, StringTruncateLength) + "'...")} was not terminated"));
                    }
                    return new Token(pattern.type, str);
                }
                if (pattern.type == Token.TokenType.FLOATING)
                {
                    string floating = match.ToString();
                    if (floating[^1] == '.')
                    {
                        Diagnostics.errors.Push(new Error.LexError(line, col, "Invalid Formatted Number", $"'{floating}' is incorectly formatted"));
                    }
                    return new Token(pattern.type, floating);
                }
                return new Token(pattern.type, match.ToString());
            }
        }

        Diagnostics.errors.Push(new Error.LexError(line, col, "Illegal Char Error", $"Character '{lexeme[0]}' is Illegal"));
        index++;
        return null;
    }

    private void Escape (ref string str)
    {
        foreach ((Regex,string) regex in regexes)
        {
            str = regex.Item1.Replace(str, regex.Item2);
        }
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

        regexes = new (Regex, string)[11];

        regexes[0] = ( new Regex("[\\\\][\\\\]"), "\", 0x5c, \"");
        regexes[1] = ( new Regex("[\\\\][a]"), "\", 0x7, \"");
        regexes[2] = ( new Regex("[\\\\][b]"),  "\", 0x8, \"");
        regexes[3] = ( new Regex("[\\\\][f]"),  "\", 0xc, \"");
        regexes[4] = ( new Regex("[\\\\][n]"),  "\", 0xa, \"");
        regexes[5] = ( new Regex("[\\\\][r]"),  "\", 0xd, \"");
        regexes[6] = ( new Regex("[\\\\][t]"),  "\", 0x9, \"");
        regexes[7] = ( new Regex("[\\\\][v]"), "\", 0xb, \"");
        regexes[8] = (new Regex("[\\\\][']"), "\", 0x27, \"");
        regexes[9] = ( new Regex("[\\\\][\"]"), "\", 0x22, \"");
        regexes[10] = ( new Regex("[\\\\][?]"), "\", 0x3f, \"");
    }

    private bool ExceedsMaxSize(Token.TokenType type, string value) => type switch
    {
        Token.TokenType.INTEGER => (value.Length == 19) ? !long.TryParse(value, out _) : value.Length > 19,
        Token.TokenType.FLOATING => (!double.TryParse(value, out var d)) || (d == double.PositiveInfinity || d == double.NegativeInfinity),
        Token.TokenType.STRING => false,
        Token.TokenType.BINARY => 64 < value[2..].Length,
        Token.TokenType.HEX => (value.Length == 18) ? ((!long.TryParse(value[2..], NumberStyles.AllowHexSpecifier, null, out var h)) || h < 0) : value.Length > 18,
        Token.TokenType.BOOLEAN => false,
        _ => false
    };
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
