using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Espionage
{
    internal class Lexer
    {
        List<Token> tokens;
        TokenDefinition[] tokenDefinitions;
        string text;
        int line;
        int col;
        int index;

        public Lexer(string text)
        {
            this.tokens = new();
            this.text = text;
            this.line = 1;
            // IMPORTANT NOTE: should col be 1 or 0 indexed?
            this.col = 0;
            this.index = 0;
            initRegex();
        }
        private void initRegex()
        {
            tokenDefinitions = new TokenDefinition[TokenList.Tokens.Count];
            int count = 0;
            foreach (var key in TokenList.Tokens.Keys)
            {
                tokenDefinitions[count] = new TokenDefinition(key, TokenList.Tokens[key]);
                count++;
            }
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
                        if (TokenList.reserved.Contains(lexeme))
                        {
                            return new Token(pattern.type, GetLiteral(pattern.type, match.ToString()), match.ToString());
                        }
                    }
                    if (pattern.type == "COMMENT")
                    {
                        return null;
                    }
                    return new Token(pattern.type, GetLiteral(pattern.type, match.ToString()), match.ToString());
                }
            }
            // IMPORTANT NOTE: add error here
            if (lexeme[0] == '"')
                throw new Errors.LexError(ErrorType.LexerException, line, col, "Non Terminated String", $"String: \'{GetLiteral("STRING", lexeme)}\'\nwas not ternimated");
            if (lexeme[0] == '.')
                throw new Errors.LexError(ErrorType.LexerException, line, col, "Invalid Formatted Number", $"{lexeme.Split()[0]} is incorectly formatted");

            throw new Errors.LexError(ErrorType.LexerException, line, col, "Illegal Char Error", $"Character '{lexeme[0]}' is Illegal");
        }

        private string GetLiteral(string type, string lexeme)
        {
            if (type == "STRING")
            {
                return lexeme.Substring(1, lexeme.Length - 2);
            }
            return lexeme;
        }
    }
    class TokenDefinition{
        public string type;
        string pattern;
        public Regex? regex;

        public TokenDefinition(string type, string pattern)
        {
            this.type = type;
            this.pattern = pattern;
            if (pattern.Length == 1)
                regex = null;
            else
                regex = new Regex(pattern);
        }

        public Match Match(string str)
        {
            if (regex == null)
                //IMPORTANT NOTE: make sure this is actually more memory efficient
                return new Regex(pattern).Match(str);
            else
                return regex.Match(str);
        }
    }
    class Token
    {
        internal string type;
        internal string lexeme;
        internal object literal;

        public Token(string type)
        {
            this.type = type;
            this.lexeme = null;
            this.literal = null;
        }
        public Token(string type, object literal, string lexeme)
        {
            this.type = type;
            this.literal = literal;
            this.lexeme = lexeme;
        }

        public override string ToString()
        {
            //return $"{{ {type} }}:{{ {literal} }}:{{ {lexeme} }}";
            return $"{{ {type} }}:{{ {Regex.Replace((literal ?? "").ToString(), "[\r\n]|[\n]", "")} }}:{{ {Regex.Replace(lexeme, "[\r\n]|[\n]", "")} }}";
        }
    }
}
