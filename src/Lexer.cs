using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Raze
{
    internal class Lexer
    {
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
                    if (pattern.type == "WHITESPACE")
                    {
                        if (lexeme[0] == '\n')
                        {
                            line++;
                            col = 0;
                        }
                        return null;
                    }
                    if (pattern.type == "IDENTIFIER")
                    {
                        if (TokenList.Reserved.Contains(match.ToString()))
                        {
                            return new Token(match.ToString(), match.ToString());
                        }
                    }
                    if (pattern.type == "COMMENT")
                    {
                        return null;
                    }
                    if (pattern.type == "STRING")
                    {
                        string str = match.ToString();
                        Escape(ref str);
                        Console.WriteLine(str);
                        return new Token(pattern.type, str);
                    }
                    return new Token(pattern.type, match.ToString());
                }
            }
            if (lexeme[0] == '"')
                throw new Errors.LexError(line, col, "Non Terminated String", $"String: \'{((lexeme.Length <= 40) ? lexeme + "'": lexeme.Substring(0, 40) + "'...")}\nwas not ternimated");
            if (lexeme[0] == '.')
                throw new Errors.LexError(line, col, "Invalid Formatted Number", $"{lexeme.Split()[0]} is incorectly formatted");

            throw new Errors.LexError(line, col, "Illegal Char Error", $"Character '{lexeme[0]}' is Illegal");
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

    }

    // Holds a token's regex and literal
    class TokenDefinition
    {
        public string type;
        public Regex regex;

        public TokenDefinition(string type, string pattern)
        {
            this.type = type;
            regex = new Regex(pattern);
        }

        public Match Match(string str)
        {
            return regex.Match(str);
        }
    }
}
