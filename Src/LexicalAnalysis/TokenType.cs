using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

internal partial class Token
{
    internal enum TokenType : byte
    {
        BINARY,           
        HEX,            
        FLOATING,           
        INTEGER,            
        IDENTIFIER,            
        STRING,
        REF_STRING,
        BOOLEAN,
        WHITESPACE,            
        EQUALTO,            
        GREATEREQUAL,            
        LESSEQUAL,            
        NOTEQUALTO,            
        PLUSPLUS,            
        MINUSMINUS,            
        EQUALS,
        OR,            
        AND,            
        PLUS,
        MINUS,
        DIVIDE,
        MULTIPLY,
        MODULO,
        SHIFTRIGHT,
        SHIFTLEFT,
        B_OR,
        B_AND,
        B_XOR,
        B_NOT,
        GREATER,
        LESS,
        NOT,
        LPAREN,
        RPAREN,
        LBRACE,
        RBRACE,
        LBRACKET,
        RBRACKET,
        COMMA,
        DOT,
        COLON,
        SEMICOLON,
        DOLLAR,
        COMMENT,
        RESERVED
    }
}
