using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Raze;

internal partial class TokenList
{
    internal partial class Patterns
    {
        // Tokens
        [GeneratedRegex(@"0[bB][0-1]+")]
        public static partial Regex BINARY();
        [GeneratedRegex(@"0[xX][0-9a-fA-F]+")]
        public static partial Regex HEX();
        [GeneratedRegex(@"[0-9]+\.(?![a-zA-Z_])[0-9]*")]
        public static partial Regex FLOATING();
        [GeneratedRegex(@"[0-9]+[uU]")]
        public static partial Regex UNSIGNED_INTEGER();
        [GeneratedRegex(@"[0-9]+")]
        public static partial Regex INTEGER();
        [GeneratedRegex(@"[a-zA-Z_][a-zA-Z0-9_]*")]
        public static partial Regex IDENTIFIER();
        [GeneratedRegex("\"[^\"^\r^\n^;]*\"?")]
        public static partial Regex REF_STRING();
        [GeneratedRegex("\'[^\'^\r^\n^;]*\'?")]
        public static partial Regex STRING();
        [GeneratedRegex(@"\s")]
        public static partial Regex WHITESPACE();
        [GeneratedRegex("==")]
        public static partial Regex EQUALTO();
        [GeneratedRegex(">=")]
        public static partial Regex GREATEREQUAL();
        [GeneratedRegex("<=")]
        public static partial Regex LESSEQUAL();
        [GeneratedRegex("!=")]
        public static partial Regex NOTEQUALTO();
        [GeneratedRegex(@"\+\+")]
        public static partial Regex PLUSPLUS();
        [GeneratedRegex("--")]
        public static partial Regex MINUSMINUS();
        [GeneratedRegex("=")]
        public static partial Regex EQUALS();
        [GeneratedRegex(@"\|\|")]
        public static partial Regex OR();
        [GeneratedRegex("&&")]
        public static partial Regex AND();
        [GeneratedRegex(@"\+")]
        public static partial Regex PLUS();
        [GeneratedRegex("-")]
        public static partial Regex MINUS();
        [GeneratedRegex("/")]
        public static partial Regex DIVIDE();
        [GeneratedRegex(@"\*")]
        public static partial Regex MULTIPLY();
        [GeneratedRegex("%")]
        public static partial Regex MODULO();
        [GeneratedRegex(">>")]
        public static partial Regex SHIFTRIGHT();
        [GeneratedRegex("<<")]
        public static partial Regex SHIFTLEFT();
        [GeneratedRegex(@"\|")]
        public static partial Regex B_OR();
        [GeneratedRegex("&")]
        public static partial Regex B_AND();
        [GeneratedRegex(@"\^")]
        public static partial Regex B_XOR();
        [GeneratedRegex(@"~")]
        public static partial Regex B_NOT();
        [GeneratedRegex(">")]
        public static partial Regex GREATER();
        [GeneratedRegex("<")]
        public static partial Regex LESS();
        [GeneratedRegex("!")]
        public static partial Regex NOT();
        [GeneratedRegex(@"\(")]
        public static partial Regex LPAREN();
        [GeneratedRegex(@"\)")]
        public static partial Regex RPAREN();
        [GeneratedRegex("{")]
        public static partial Regex LBRACE();
        [GeneratedRegex("}")]
        public static partial Regex RBRACE();
        [GeneratedRegex(@"\[")]
        public static partial Regex LBRACKET();
        [GeneratedRegex(@"\]")]
        public static partial Regex RBRACKET();
        [GeneratedRegex(",")]
        public static partial Regex COMMA();
        [GeneratedRegex(@"\.")]
        public static partial Regex DOT();
        [GeneratedRegex(":")]
        public static partial Regex COLON();
        [GeneratedRegex(";")]
        public static partial Regex SEMICOLON();
        [GeneratedRegex(@"\$")]
        public static partial Regex DOLLAR();
        [GeneratedRegex("(?m)[#].*$")]
        public static partial Regex COMMENT();


        // Escape-Sequences
        [GeneratedRegex(@"\\\\")]
        public static partial Regex EscapeBackslash();
        [GeneratedRegex(@"\\a")]
        public static partial Regex EscapeA();
        [GeneratedRegex(@"\\b")]
        public static partial Regex EscapeB();
        [GeneratedRegex(@"\\f")]
        public static partial Regex EscapeF();
        [GeneratedRegex(@"\\n")]
        public static partial Regex EscapeN();
        [GeneratedRegex(@"\\r")]
        public static partial Regex EscapeR();
        [GeneratedRegex(@"\\t")]
        public static partial Regex EscapeT();
        [GeneratedRegex(@"\\v")]
        public static partial Regex EscapeV();
        [GeneratedRegex(@"\\'")]
        public static partial Regex EscapeSingleQuote();
        [GeneratedRegex(@"\\\""")]
        public static partial Regex EscapeDoubleQuote();
        [GeneratedRegex(@"\\\?")]
        public static partial Regex EscapeQuestionMark();
        [GeneratedRegex(@"\\0")]
        public static partial Regex Escape0();
    }
}