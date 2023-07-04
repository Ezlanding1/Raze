using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Raze;

internal class TokenList
{
    // Higher tokens have a higher precedence
    public static readonly Dictionary<Token.TokenType, string> Tokens = new Dictionary<Token.TokenType, string>()
    {
        // Non Finite Tokens
        { Token.TokenType.BINARY, /*lang=regex*/@"[0-1]+[b]" },
        { Token.TokenType.HEX, /*lang=regex*/@"0[xX][0-9a-fA-F]+" },
        { Token.TokenType.FLOATING, /*lang=regex*/@"[0-9]+[.][0-9]+" },
        { Token.TokenType.INTEGER, /*lang=regex*/@"[0-9]+" },
        { Token.TokenType.IDENTIFIER, /*lang=regex*/@"[a-zA-Z_][a-zA-Z0-9_]*" },
        { Token.TokenType.STRING, /*lang=regex*/"[\"][^\"]*[\"]" },
        { Token.TokenType.WHITESPACE, /*lang=regex*/@"[\s]" },

        // Comparison Operators
        { Token.TokenType.EQUALTO, /*lang=regex*/"[=][=]" },
        { Token.TokenType.GREATEREQUAL, /*lang=regex*/"[>][=]" },
        { Token.TokenType.LESSEQUAL, /*lang=regex*/"[<][=]" },
        { Token.TokenType.NOTEQUALTO, /*lang=regex*/"[!][=]" },

        // Assignment Operators
        { Token.TokenType.PLUSPLUS, /*lang=regex*/"[+][+]" },
        { Token.TokenType.MINUSMINUS, /*lang=regex*/"[-][-]" },
        { Token.TokenType.EQUALS, "=" },

        // Logical Operators
        { Token.TokenType.OR, /*lang=regex*/"[|][|]" },
        { Token.TokenType.AND, /*lang=regex*/"[&][&]" },
        
        // Mathematical Operators
        { Token.TokenType.PLUS, "[+]" },
        { Token.TokenType.MINUS, "[-]" },
        { Token.TokenType.DIVIDE, "[/]" },
        { Token.TokenType.MULTIPLY, "[*]" },
        { Token.TokenType.MODULO, "[%]" },

        // Bitwise Operators
        { Token.TokenType.SHIFTRIGHT, /*lang=regex*/"[>][>]" },
        { Token.TokenType.SHIFTLEFT, /*lang=regex*/"[<][<]" },

        { Token.TokenType.B_OR, "[|]" },
        { Token.TokenType.B_AND, "[&]" },
        { Token.TokenType.B_XOR, @"[\^]" },
        { Token.TokenType.B_NOT, @"[~]" },

        // GreaterThan and LessThan
        { Token.TokenType.GREATER, "[>]" },
        { Token.TokenType.LESS, "[<]" },

        // Unary Operators
        { Token.TokenType.NOT, "[!]" },

        // Braces and Brackets
        { Token.TokenType.LPAREN, "[(]" },
        { Token.TokenType.RPAREN, "[)]" },
        { Token.TokenType.LBRACE, "[{]" },
        { Token.TokenType.RBRACE, "[}]" },
        { Token.TokenType.LBRACKET, "[[]" },
        { Token.TokenType.RBRACKET, "[]]" },

        // Others
        { Token.TokenType.COMMA, "[,]" },
        { Token.TokenType.DOT, "[.]" },
        { Token.TokenType.COLON, "[:]" },
        { Token.TokenType.SEMICOLON, "[;]" },
        { Token.TokenType.DOLLAR, "[$]" },
        { Token.TokenType.COMMENT, /*lang=regex*/"(?m)[#].*$" },
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
        "primitive",
        "this",
        "ref"
    };

    /*
     * Contextual Keywords:
     * 
     *      sizeof
     *      void
     */
}
