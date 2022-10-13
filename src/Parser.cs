using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Espionage
{
    internal class Parser
    {
        List<Token> tokens;
        List<Expr> expressions;
        Token current;
        int index;
        public Parser(List<Token> tokens)
        {
            this.tokens = tokens;
            this.expressions = new();
            current = tokens[0];
            this.index = 0;
        }

        internal List<Expr> Parse()
        {
            while (!isAtEnd())
            {
                expressions.Add(Start());
            }
            return expressions;
        }
        //IMPORTANT NOTE: not everything can be in parenthesis. Make a canGoInParen function and make grouping call it
        //IMPORTANT NOTE: not everything can be in parameters. Make a canBeParam function and make "call" call it
        //IMPORTANT NOTE: make a getParams function
        private Expr Start()
        {
            return Definition();
        }

        private Expr Definition()
        {
            if (!isAtEnd() && TypeMatch("function", "class"))
            {
                Token definitionType = previous();
                Expect("IDENTIFIER", "Expected " + definitionType.type + " name");
                Token name = previous();
                List<Expr> expressions = new();
                if (definitionType.type == "function")
                {
                    Expect("LPAREN", "Expected '(' after function name");
                    List<Expr> parameters = new();
                    while (true)
                    {
                        parameters.Add(Assignment());
                        if (TypeMatch("RPAREN"))
                        {
                            break;
                        }
                        Expect("COMMA", "Expected ',' between parameters");
                        if (isAtEnd())
                        {
                            throw new Errors.ParseError(ErrorType.ParserException, "Unexpected End In Function Parameters", $"Function '{name.lexeme}' reached an unexpected end during it's parameters");
                        }
                    }
                    expressions = GetBlock(definitionType.type);
                    return new Expr.Function(name, parameters, expressions);
                }
                else if (definitionType.type == "class")
                {
                    expressions = GetBlock(definitionType.type);
                    return new Expr.Class(name, expressions);
                }
            }
            return Semicolon();
        }

        private Expr Semicolon()
        {
            Expr expr = Assignment();
            Expect("SEMICOLON", "Expected ';' after expression");
            return expr;
        }

        private Expr Assignment()
        {
            Expr expr = Logical();
            if (!isAtEnd())
            {
                if (current.type == "IDENTIFIER")
                {
                    Expr left = Additive();
                    Expect("EQUALS", "Expected '=' when declaring variable");
                    Token op = previous(); 
                    Expr right = Additive();
                    expr = new Expr.Declare(expr, left, op, right);
                }
                else if (TypeMatch("EQUALS", "PLUSEQUALS", "MINUSEQUALS"))
                {
                    Token op = previous();
                    Expr right = Additive();
                    expr = new Expr.Binary(expr, op, right);
                }
                //IMPORTANT NOTE: fix the precedence of ++ and -- (and add prefix modifier)
                else if (TypeMatch("PLUSPLUS", "MINUSMINUS"))
                {
                    Token op = previous();
                    expr = new Expr.Unary(op, expr);
                }
            }
            return expr;
        }

        private Expr Logical()
        {
            Expr expr = Bitwise();
            if (!isAtEnd() && TypeMatch("AND", "OR"))
            {
                Token op = previous();
                Expr right = Additive();
                expr = new Expr.Binary(expr, op, right);
            }
            return expr;
        }

        private Expr Bitwise()
        {
            Expr expr = Additive();
            if (!isAtEnd() && TypeMatch("B_OR", "B_AND", "B_XOR", "B_NOT"))
            {
                Token op = previous();
                Expr right = Additive();
                expr = new Expr.Binary(expr, op, right);
            }
            return expr;
        }

        private Expr Additive()
        {
            Expr expr = Multiplicative();
            if (!isAtEnd() && TypeMatch("PLUS", "MINUS"))
            {
                Token op = previous();
                Expr right = Additive();
                expr = new Expr.Binary(expr, op, right);
            }
            return expr;
        }

        private Expr Multiplicative()
        {
            Expr expr = Call();
            if (!isAtEnd() && TypeMatch("MULTIPLY", "DIVIDE", "MODULO"))
            {
                Token op = previous();
                Expr right = Additive();
                expr = new Expr.Binary(expr, op, right);
            }
            return expr;
        }

        private Expr Call()
        {
            Expr expr = Primary();
            if (!isAtEnd() && TypeMatch("LPAREN"))
            {
                List<Expr> arguments = new();
                while (true)
                {
                    arguments.Add(Assignment());
                    if (TypeMatch("RPAREN"))
                    {
                        break;
                    }
                    Expect("COMMA", "Expected ',' between parameters");
                }
                expr = new Expr.Call(expr, arguments);
            }
            return expr;
        }

        private Expr Primary()
        {
            if (!isAtEnd())
            {
                if (TypeMatch("NUMBER"))
                {
                    return new Expr.Literal(previous());
                }

                if (TypeMatch("LPAREN", "RPAREN"))
                {
                    Expr expr = Start();
                    Expect("RPAREN", "Expected ')' after expression.");
                    return new Expr.Grouping(expr);
                }

                if (TypeMatch("IDENTIFIER"))
                {
                    return new Expr.Variable(previous());
                }
            }
            throw End();
        }

        private Exception End()
        {
            return new Errors.ParseError(ErrorType.ParserException, "Expression Reached Unexpected End", $"Expression '{((previous() != null)? previous().lexeme : "")}' reached an unexpected end");
        }

        private List<Expr> GetBlock(string bodytype)
        {
            List<Expr> expressions = new();
            Expect("LBRACE", "Expect '{' before " + bodytype + " body");
            while (!isAtEnd())
            {
                expressions.Add(Start());
                if (isAtEnd())
                {
                    //IMPORTANT NOTE: ERROR HERE
                    throw new NotImplementedException();
                    //IMPORTANT NOTE: change this to say 'function'/'class' instead of block
                    //Expect("RBRACE", "Expect '}' after block");
                }
                if (TypeMatch("RBRACE"))
                {
                    break;
                }
            }
            return expressions;
        }

        private bool TypeMatch(params string[] types)
        {
            foreach (var type in types)
            {
                if (current.type == type)
                {
                    advance();
                    return true;
                }
            }
            return false;
        }
        private bool ValueMatch(string type, params string[] values)
        {
            if (current.type != type)
            {
                return false;
            }
            foreach (var value in values)
            {
                if ((current.literal??"").ToString() == value)
                {
                    advance();
                    return true;
                }
            }
            return false;
        }
        private void Expect(string type, string errorMessage)
        {
            if (current.type == type)
            {
                advance();
                return;
            }

            throw new Errors.ParseError(ErrorType.ParserException, type, errorMessage);
        }

        private Token previous()
        {
            if (!isAtEnd(index - 1))
            {
                return tokens[index - 1];
            }
            return null;
        }

        private Token peek()
        {
            if (!isAtEnd(index + 1))
            {
                return tokens[index + 1];
            }
            return null;
        }

        private void advance()
        {
            index++;
            if (!isAtEnd())
            {
                current = tokens[index];
                return;
            }
            current = null;
        }

        private bool isAtEnd()
        {
            return (index >= tokens.Count);
        }
        private bool isAtEnd(int idx)
        {
            return (idx >= tokens.Count);
        }
    }
}
