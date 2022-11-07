﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Espionage
{
    internal class TokenList
    {
        //IMPORTANT NOTE: CHANGE STRINGS TO int/enum for better performance?
        //IMPORTANT NOTE: switch from type : token to token : type?
        //NOTE: higher tokens have a higher precedence
        public static readonly Dictionary<string, string> Tokens = new Dictionary<string, string>()
        {
            // Non Finite Tokens
            { "NUMBERDOT", /*lang=regex*/@"[0-9]+[.][0-9]+" },
            { "NUMBER", /*lang=regex*/@"[0-9]+" },
            { "IDENTIFIER", /*lang=regex*/@"[a-zA-Z]+" },
            { "STRING", /*lang=regex*/@"[""].*[""]" },
            { "WHITESPACE", /*lang=regex*/@"[\s]" },

            // Comparison Operators
            { "EQUALTO", /*lang=regex*/"[=][=]" },
            { "GREATEREQUAL", /*lang=regex*/"[>][=]" },
            { "LESSEQUAL", /*lang=regex*/"[<][=]" },
            { "NOTEQUALTO", /*lang=regex*/"[!][=]" },

            // Assignment Operators
            { "PLUSEQUALS", /*lang=regex*/"[+][=]" },
            { "MINUSEQUALS", /*lang=regex*/"[-][=]" },
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

            // Others
            { "COMMA", "[,]" },
            { "DOT", "[.]" },
            { "SEMICOLON", "[;]" },
            { "COMMENT", /*lang=regex*/"(?m)[#].*$" },
        };

        public static readonly HashSet<string> reserved = new HashSet<string>()
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
            "new"
        };
    }
}