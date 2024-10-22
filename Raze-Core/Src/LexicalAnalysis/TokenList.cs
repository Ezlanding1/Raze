using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Raze;

internal partial class TokenList
{
    // Higher tokens have a higher precedence
    public static readonly Dictionary<Token.TokenType, Regex> Tokens = new Dictionary<Token.TokenType, Regex>()
    {
        // Non Finite Tokens
        { Token.TokenType.FLOATING, Patterns.FLOATING() },
        { Token.TokenType.UNSIGNED_INTEGER, Patterns.UNSIGNED_INTEGER() },
        { Token.TokenType.INTEGER, Patterns.INTEGER() },
        { Token.TokenType.IDENTIFIER, Patterns.IDENTIFIER() },
        { Token.TokenType.REF_STRING, Patterns.REF_STRING() },
        { Token.TokenType.STRING, Patterns.STRING() },
        { Token.TokenType.WHITESPACE, Patterns.WHITESPACE() },

        // Comparison Operators
        { Token.TokenType.EQUALTO, Patterns.EQUALTO() },
        { Token.TokenType.GREATEREQUAL, Patterns.GREATEREQUAL() },
        { Token.TokenType.LESSEQUAL, Patterns.LESSEQUAL() },
        { Token.TokenType.NOTEQUALTO, Patterns.NOTEQUALTO() },

        // Assignment Operators
        { Token.TokenType.PLUSPLUS, Patterns.PLUSPLUS() },
        { Token.TokenType.MINUSMINUS, Patterns.MINUSMINUS() },
        { Token.TokenType.EQUALS, Patterns.EQUALS() },

        // Logical Operators
        { Token.TokenType.OR, Patterns.OR() },
        { Token.TokenType.AND, Patterns.AND() },
        
        // Mathematical Operators
        { Token.TokenType.PLUS, Patterns.PLUS() },
        { Token.TokenType.MINUS, Patterns.MINUS() },
        { Token.TokenType.DIVIDE, Patterns.DIVIDE() },
        { Token.TokenType.MULTIPLY, Patterns.MULTIPLY() },
        { Token.TokenType.MODULO, Patterns.MODULO() },

        // Bitwise Operators
        { Token.TokenType.SHIFTRIGHT, Patterns.SHIFTRIGHT() },
        { Token.TokenType.SHIFTLEFT, Patterns.SHIFTLEFT() },

        { Token.TokenType.B_OR, Patterns.B_OR() },
        { Token.TokenType.B_AND, Patterns.B_AND() },
        { Token.TokenType.B_XOR, Patterns.B_XOR() },
        { Token.TokenType.B_NOT, Patterns.B_NOT() },

        // GreaterThan and LessThan
        { Token.TokenType.GREATER, Patterns.GREATER() },
        { Token.TokenType.LESS, Patterns.LESS() },

        // Unary Operators
        { Token.TokenType.NOT, Patterns.NOT() },

        // Braces and Brackets
        { Token.TokenType.LPAREN, Patterns.LPAREN() },
        { Token.TokenType.RPAREN, Patterns.RPAREN() },
        { Token.TokenType.LBRACE, Patterns.LBRACE() },
        { Token.TokenType.RBRACE, Patterns.RBRACE() },
        { Token.TokenType.LBRACKET, Patterns.LBRACKET() },
        { Token.TokenType.RBRACKET, Patterns.RBRACKET() },

        // Others
        { Token.TokenType.COMMA, Patterns.COMMA() },
        { Token.TokenType.DOT, Patterns.DOT() },
        { Token.TokenType.COLON, Patterns.COLON() },
        { Token.TokenType.SEMICOLON, Patterns.SEMICOLON() },
        { Token.TokenType.DOLLAR, Patterns.DOLLAR() },
        { Token.TokenType.COMMENT, Patterns.COMMENT() },
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
        "is",
        "as",
        "primitive",
        "this",
        "ref",
        "import",
        "from",
        "extends",
        "trait",
        "heapalloc",
        "readonly"
    };

    /*
     * Contextual Keywords:
     * 
     *      sizeof
     *      void
     */
}
