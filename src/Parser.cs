using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Espionage.Expr;

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
            this.index = -1;
            advance();
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
                Expr.Block block;
                if (definitionType.type == "function")
                {
                    Expect("LPAREN", "Expected '(' after function name");
                    List<Expr.Parameter> parameters = new();
                    while (!TypeMatch("RPAREN"))
                    {
                        Expect("IDENTIFIER", "Expected identifier as function parameter type");
                        Token type = previous();
                        Expect("IDENTIFIER", "Expected identifier as function parameter");
                        Token variable = previous();

                        parameters.Add(new Expr.Parameter(type, variable));
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
                    block = GetBlock(definitionType.type);
                    return new Expr.Function(name, parameters, block);
                }
                else if (definitionType.type == "class")
                {
                    block = GetBlock(definitionType.type);
                    return new Expr.Class(name, block);
                }
            }
            return Conditional();
        }

        private Expr Conditional()
        {
            if (!isAtEnd() && TypeMatch("if", "else", "while"))
            {
                Token conditionalType = previous();
                Expr.Block block;
                if (conditionalType.type == "if")
                {
                    // Important Note: change to conditional ( '==', '>=', etc. ) once they're implmented
                    Expect("LPAREN", "Expected '(' after conditional");
                    Expr condition;
                    condition = Logical();
                    Expect("RPAREN", "Expected ')' after condition");
                    block = GetBlock(conditionalType.type);
                    return new Expr.Conditional(conditionalType, condition, block);
                }
                else if (conditionalType.type == "else")
                {
                    if (current.lexeme == "if")
                    {
                        // Important Note: change to conditional ( '==', '>=', etc. ) once they're implmented
                        Expect("LPAREN", "Expected '(' after conditional");
                        Expr condition;
                        condition = Logical();
                        Expect("RPAREN", "Expected ')' after condition");
                        conditionalType.type = "else if";
                        block = GetBlock(conditionalType.type);
                        return new Expr.Conditional(conditionalType, condition, block);
                    }
                    block = GetBlock(conditionalType.type);
                    return new Expr.Conditional(conditionalType, null, block);
                }
                else if (conditionalType.type == "while")
                {
                    throw new NotImplementedException();
                }
            }
            return Semicolon();
        }

        private Expr Semicolon()
        {
            Expr expr = NoSemicolon();
            Expect("SEMICOLON", "Expected ';' after expression");
            return expr;
        }

        private Expr NoSemicolon()
        {
            Expr expr = Logical();
            return expr;
        }

        private Expr Logical()
        {
            Expr expr = Bitwise();
            while (!isAtEnd() && TypeMatch("AND", "OR"))
            {
                Token op = previous();
                Expr right = Bitwise();
                expr = new Expr.Binary(expr, op, right);
            }
            return expr;
        }

        private Expr Bitwise()
        {
            Expr expr = Equality();
            while (!isAtEnd() && TypeMatch("B_OR", "B_AND", "B_XOR", "B_NOT"))
            {
                Token op = previous();
                Expr right = Equality();
                expr = new Expr.Binary(expr, op, right);
            }
            return expr;
        }

        private Expr Equality()
        {
            Expr expr = Comparison();
            while (!isAtEnd() && TypeMatch("EQUALTO", "NOTEQUALTO"))
            {
                Token op = previous();
                Expr right = Comparison();
                expr = new Expr.Binary(expr, op, right);
            }
            return expr;
        }

        private Expr Comparison()
        {
            Expr expr = Additive();
            while (!isAtEnd() && TypeMatch("GREATEREQUAL", "LESSEQUAL", "GREATER", "LESS"))
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
            while (!isAtEnd() && TypeMatch("PLUS", "MINUS"))
            {
                Token op = previous();
                Expr right = Multiplicative();
                expr = new Expr.Binary(expr, op, right);
            }
            return expr;
        }

        private Expr Multiplicative()
        {
            Expr expr = Primary();
            while (!isAtEnd() && TypeMatch("MULTIPLY", "DIVIDE", "MODULO"))
            {
                Token op = previous();
                Expr right = Primary();
                expr = new Expr.Binary(expr, op, right);
            }
            return expr;
        }

        

        private Expr Primary()
        {
            if (!isAtEnd())
            {
                if (TypeMatch("NUMBER", "STRING"))
                {
                    return new Expr.Literal(previous());
                }

                if (TypeMatch("LPAREN", "RPAREN"))
                {
                    Expr expr = Logical();
                    Expect("RPAREN", "Expected ')' after expression.");
                    return new Expr.Grouping(expr);
                }

                if (TypeMatch("IDENTIFIER"))
                {
                    Expr expr;
                    var variable = previous();
                    if (TypeMatch("DOT"))
                    {
                        var getter = GetGetter(variable);
                        expr = GetSetter(getter);
                        if (expr == null)
                        {
                            expr = getter;
                        }
                    }
                    else if (TypeMatch("EQUALS", "PLUSEQUALS", "MINUSEQUALS"))
                    {
                        Expr right = Logical();
                        expr = new Expr.Assign(new Expr.Variable(variable), right);
                    }
                    else if (TypeMatch("IDENTIFIER"))
                    {
                        Token name = previous();
                        Expect("EQUALS", "Expected '=' when declaring variable");
                        Expr value = Logical();
                        if (value is Expr.Literal)
                        {
                            expr = new Expr.Primitive(Primitives.ToPrimitive(variable, name, (Expr.Literal)value));
                        }
                        else
                        {
                            expr = new Expr.Declare(variable, name, value);
                        }
                    }
                    else if (TypeMatch("LPAREN"))
                    {
                        expr = Call(new Expr.Variable(variable));
                    }
                    else
                    {
                        expr = new Expr.Variable(variable);
                    }
                    return expr;
                }

                if (TypeMatch("return"))
                {
                    Expr value = Additive();
                    return new Expr.Return(value);
                }

                if (TypeMatch("null"))
                {
                    return new Expr.Keyword(previous());
                }

                if (TypeMatch("new"))
                {
                    Expect("IDENTIFIER", "Expected class name after 'new' keyword");
                    var _className = previous();
                    Expect("LPAREN", "Expected '(' starting constructor parameters");

                    return new Expr.New(_className, GetArgs());
                }
            }
            throw End();
        }

        private Expr.Get GetGetter(Token getter)
        {
            Expr.Var get;
            if (TypeMatch("DOT"))
            {
                if (TypeMatch("IDENTIFIER"))
                {
                    get = GetGetter(previous());
                }
                else
                {
                    throw new Errors.ParseError(ErrorType.ParserException, "Trailing Dot", "Trailing '.' character");
                }
            }
            else
            {
                Expect("IDENTIFIER", "Expected variable name after '.'");
                get = new Expr.Variable(previous());
            }

            return new Expr.Get(getter, get);
        }

        private Expr.Assign GetSetter(Expr.Var variable)
        {
            if (TypeMatch("EQUALS", "PLUSEQUALS", "MINUSEQUALS"))
            {
                Expr right = Logical();
                return new Expr.Assign(variable, right);
            }
            else
            {
                return null;
            }
        }

        private Exception End()
        {
            return new Errors.ParseError(ErrorType.ParserException, "Expression Reached Unexpected End", $"Expression '{((previous() != null)? previous().lexeme : "")}' reached an unexpected end");
        }
        private Expr.Call Call(Expr.Var name)
        {
            return new Expr.Call(name, GetArgs());
        }

        private List<Expr> GetArgs()
        {
            List<Expr> arguments = new();
            while (!TypeMatch("RPAREN"))
            {
                arguments.Add(Logical());
                if (TypeMatch("RPAREN"))
                {
                    break;
                }
                Expect("COMMA", "Expected ',' between parameters");
            }
            return arguments;
        }

        private Expr.Block GetBlock(string bodytype)
        {
            List<Expr> bodyExprs = new();
            Expect("LBRACE", "Expected '{' before " + bodytype + " body");
            while (!TypeMatch("RBRACE"))
            {
                bodyExprs.Add(Start());
                if (isAtEnd())
                {
                    Expect("RBRACE", "Expected '}' after block");
                }
                if (TypeMatch("RBRACE"))
                {
                    break;
                }
            }
            return new Expr.Block(bodyExprs);
        }

        private bool TypeMatch(params string[] types)
        {
            if (current == null)
            {
                return false;
            }
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

        private bool TypeMatch(Dictionary<string, Primitives.PrimitiveType>.KeyCollection types)
        {
            if (current == null)
            {
                return false;
            }
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

        private void Expect(string type, string errorMessage)
        {
            if (current != null && current.type == type)
            {
                advance();
                return;
            }

            throw new Errors.ParseError(ErrorType.ParserException, $"Expected {type}", errorMessage + $"{((current != null)? "\nGot: '" + current.lexeme + "' Instead" : "")}");
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
