using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Raze
{
    internal class TokenList
    {
        // Higher tokens have a higher precedence
        public static readonly Dictionary<string, string> Tokens = new Dictionary<string, string>()
        {
            // Non Finite Tokens
            { "BINARY", /*lang=regex*/@"[0-1]+[b]" },
            { "HEX", /*lang=regex*/@"0[xX][0-9a-fA-F]+" },
            { "FLOAT", /*lang=regex*/@"[0-9]+[.][0-9]+" },
            { "INTEGER", /*lang=regex*/@"[0-9]+" },
            { "IDENTIFIER", /*lang=regex*/@"[a-zA-Z_][a-zA-Z0-9_]*" },
            { "STRING", /*lang=regex*/"[\"][^\"]*[\"]" },
            { "WHITESPACE", /*lang=regex*/@"[\s]" },

            // Comparison Operators
            { "EQUALTO", /*lang=regex*/"[=][=]" },
            { "GREATEREQUAL", /*lang=regex*/"[>][=]" },
            { "LESSEQUAL", /*lang=regex*/"[<][=]" },
            { "NOTEQUALTO", /*lang=regex*/"[!][=]" },

            // Assignment Operators
            { "PLUSPLUS", /*lang=regex*/"[+][+]" },
            { "MINUSMINUS", /*lang=regex*/"[-][-]" },
            { "EQUALS", "=" },

            // Logical Operators
            { "OR", /*lang=regex*/"[|][|]" },
            { "AND", /*lang=regex*/"[&][&]" },
            
            // Mathematical Operators
            { "PLUS", "[+]" },
            { "MINUS", "[-]" },
            { "DIVIDE", "[/]" },
            { "MULTIPLY", "[*]" },
            { "MODULO", "[%]" },

            // Bitwise Operators
            { "SHIFTRIGHT", /*lang=regex*/"[>][>]" },
            { "SHIFTLEFT", /*lang=regex*/"[<][<]" },

            { "B_OR", "[|]" },
            { "B_AND", "[&]" },
            { "B_XOR", @"[\^]" },
            { "B_NOT", @"[~]" },

            // GreaterThan and LessThan
            { "GREATER", "[>]" },
            { "LESS", "[<]" },

            // Unary Operators
            { "NOT", "[!]" },

            // Braces and Brackets
            { "LPAREN", "[(]" },
            { "RPAREN", "[)]" },
            { "LBRACE", "[{]" },
            { "RBRACE", "[}]" },
            { "LBRACKET", "[[]" },
            { "RBRACKET", "[]]" },

            // Others
            { "COMMA", "[,]" },
            { "DOT", "[.]" },
            { "COLON", "[:]" },
            { "SEMICOLON", "[;]" },
            { "DOLLAR", "[$]" },
            { "COMMENT", /*lang=regex*/"(?m)[#].*$" },
        };

        public static readonly HashSet<string> Reserved = new HashSet<string>()
        {
            "if",
            "else",
            "null",
            "true",
            "false",
            "class",
            "return",
            "for",
            "while",
            "function",
            "new",
            "asm",
            "define",
            "is",
            "primitive"
        };

        /*
         * Contextual Keywords:
         * 
         *      sizeof
         */
    }
}
