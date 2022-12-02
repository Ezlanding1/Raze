using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime;
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

        private Expr Start()
        {
            return Definition();
        }

        private Expr Definition()
        {
            if (!isAtEnd() && TypeMatch("function", "class", "asm", "define"))
            {
                Token definitionType = previous();
                if (definitionType.type == "function")
                {
                    var function = new Expr.Function();
                    string _returnType;
                    if (peek().type == "IDENTIFIER")
                    {
                        if (!function.modifiers.ContainsKey(current.lexeme))
                        {
                            Expect("IDENTIFIER", "function return type");
                            _returnType = previous().lexeme;
                        }
                        else
                        {
                            _returnType = "void";
                        }
                    }
                    else
                    {
                        _returnType = "void";
                    }
                    while (peek().type != "LPAREN")
                    {
                        Expect("IDENTIFIER", "an identifier as a function modifier");
                        var modifier = previous();
                        if (function.modifiers.ContainsKey(previous().lexeme))
                        {
                            function.modifiers[modifier.lexeme] = true;
                        }
                        else
                        {
                            throw new Errors.ParseError(ErrorType.ParserException, "Invalid Modifier", $"The modifier '{modifier.lexeme}' does not exist");
                        }
                    }
                    Expect("IDENTIFIER", definitionType.type + " name");
                    Token name = previous();
                    Expect("LPAREN", "'(' after function name");
                    List<Expr.Parameter> parameters = new();
                    while (!TypeMatch("RPAREN"))
                    {
                        Expect("IDENTIFIER", "identifier as function parameter type");
                        Token type = previous();
                        Expect("IDENTIFIER", "identifier as function parameter");
                        Token variable = previous();

                        parameters.Add(new Expr.Parameter(type, variable));
                        if (TypeMatch("RPAREN"))
                        {
                            break;
                        }
                        Expect("COMMA", "',' between parameters");
                        if (isAtEnd())
                        {
                            throw new Errors.ParseError(ErrorType.ParserException, "Unexpected End In Function Parameters", $"Function '{name.lexeme}' reached an unexpected end during it's parameters");
                        }
                    }
                    function.Add(_returnType, name, parameters, GetBlock(definitionType.type));
                    return function;
                }
                else if (definitionType.type == "class")
                {
                    Expect("IDENTIFIER", definitionType.type + " name");
                    Token name = previous();
                    return new Expr.Class(name, GetBlock(definitionType.type));
                }
                else if (definitionType.type == "asm")
                {
                    return new Expr.Assembly(GetAsmInstructions());
                }
                else if (definitionType.type == "define")
                {
                    Expect("IDENTIFIER", "name of 'Define'");
                    string name = previous().lexeme;

                    return new Expr.Define(name, Literal() ?? throw new Errors.ParseError(ErrorType.ParserException, "Incorrect Define", "The value of 'Define' should be a literal"));
                }
            }
            return Conditional();
        }

        private Expr Conditional()
        {
            if (!isAtEnd() && TypeMatch("if", "else", "while", "for"))
            {
                Token conditionalType = previous();
                Expr.Block block;

                conditionalType.type = (TypeMatch("if"))?  "else if" : conditionalType.type;

                switch (conditionalType.type)
                {
                    case "if":
                    {
                        Expr condition = GetCondition();
                        block = GetBlock(conditionalType.type);
                        return new Expr.If(conditionalType, condition, block);
                    }
                    case "else if":
                    {
                        Expr condition = GetCondition();
                        block = GetBlock(conditionalType.type);
                        return new Expr.ElseIf(conditionalType, condition, block);
                    }
                    case "else":
                    {
                        block = GetBlock(conditionalType.type);
                        return new Expr.Else(conditionalType, block);
                    }
                    case "while":
                    {
                        Expr condition = GetCondition();
                        block = GetBlock(conditionalType.type);
                        return new Expr.While(conditionalType, condition, block);
                    }
                    case "for":
                    {
                        throw new NotImplementedException();
                    }
                }
            }
            return Semicolon();
        }

        private Expr Semicolon()
        {
            Expr expr = NoSemicolon();
            Expect("SEMICOLON", "';' after expression");
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
            Expr expr = Unary();
            while (!isAtEnd() && TypeMatch("MULTIPLY", "DIVIDE", "MODULO"))
            {
                Token op = previous();
                Expr right = Unary();
                expr = new Expr.Binary(expr, op, right);
            }
            return expr;
        }

        private Expr Unary()
        {
            while (!isAtEnd() && TypeMatch("NOT", "MINUS"))
            {
                Token op = previous();
                Expr right = Unary();
                return new Expr.Unary(op, right);
            }
            return Primary();
        }

        private Expr Primary()
        {
            if (!isAtEnd())
            {
                Expr? x = null;
                if ((x = Literal()) != null)
                {
                    return x;
                }

                if (TypeMatch("LPAREN", "RPAREN"))
                {
                    Expr expr = Logical();
                    Expect("RPAREN", "')' after expression.");
                    return new Expr.Grouping(expr);
                }

                if (TypeMatch("IDENTIFIER"))
                {
                    Expr expr;
                    Expr.Var variable = new Expr.Variable(previous());

                    if (TypeMatch("DOT"))
                    {
                        variable = GetGetter(variable.variable);
                    }
                    if (TypeMatch("EQUALS"))
                    {
                        expr = new Expr.Assign(variable, NoSemicolon());
                    }
                    else if (TypeMatch(new string[] { "PLUS", "MINUS", "MULTIPLY", "DIVIDE", "MODULO" }, new string[] { "EQUALS" }))
                    {
                        var sign = tokens[index-2];
                        expr = new Expr.Assign(variable, sign, NoSemicolon());
                    }
                    else if (TypeMatch("PLUSPLUS", "MINUSMINUS"))
                    {
                        expr = new Expr.Unary(previous(), variable);
                    }
                    else if (TypeMatch("IDENTIFIER"))
                    {
                        Token name = previous();
                        Expect("EQUALS", "'=' when declaring variable");
                        Expr value = NoSemicolon();
                        expr = (value is Expr.Literal)
                            ? new Expr.Primitive(Primitives.ToPrimitive(variable.variable, name, (Expr.Literal)value))
                            : new Expr.Declare(variable.variable, name, value);
                    }
                    else if (TypeMatch("LPAREN"))
                    {
                        expr = Call(variable);
                    }
                    else
                    {
                        expr = variable;
                    }
                    return expr;
                }


                if (TypeMatch("return"))
                {
                    if (current.type == "SEMICOLON")
                    {
                        return new Expr.Return(new Expr.Keyword("void"), true);
                    }
                    Expr value = Additive();
                    return new Expr.Return(value);
                }

                if (TypeMatch("null", "true", "false"))
                {
                    return new Expr.Keyword(previous().lexeme);
                }

                if (TypeMatch("new"))
                {
                    Expect("IDENTIFIER", "class name after 'new' keyword");
                    var _className = previous();
                    Expect("LPAREN", "'(' starting constructor parameters");

                    return new Expr.New(_className, GetArgs());
                }
            }
            throw End();
        }

        private Expr.Literal? Literal()
        {
            if (TypeMatch("NUMBER", "STRING"))
            {
                return new Expr.Literal(previous());
            }
            return null;
        }

        private Expr.Var GetGetter(Token getter)
        {
            Expect("IDENTIFIER", "variable name after '.'");

            Expr.Var get = new Expr.Variable(previous());
            if (TypeMatch("DOT"))
            {
                get = new Expr.Get(get.variable, GetGetter(current));
            }
            else
            {
                return get;
            }

            return new Expr.Get(getter, get);
        }

        private Exception End()
        {
            return new Errors.ParseError(ErrorType.ParserException, "Expression Reached Unexpected End", $"Expression '{((previous() != null)? previous().lexeme : "")}' reached an unexpected end");
        }
        private Expr.Call Call(Expr.Var name)
        {
            return new Expr.Call(name, GetArgs(), false);
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
                Expect("COMMA", "',' between parameters");
            }
            return arguments;
        }

        private Expr GetCondition()
        {
            Expect("LPAREN", "'(' after conditional");
            var condition = Logical();
            Expect("RPAREN", "')' after condition");
            return condition;
        }

        private Expr.Block GetBlock(string bodytype)
        {
            return new Expr.Block(GetBlockItems(bodytype));
        }

        private List<Expr> GetBlockItems(string bodytype)
        {
            List<Expr> bodyExprs = new();
            Expect("LBRACE", "'{' before " + bodytype + " body");
            while (!TypeMatch("RBRACE"))
            {
                bodyExprs.Add(Start());
                if (isAtEnd())
                {
                    Expect("RBRACE", "'}' after block");
                }
                if (TypeMatch("RBRACE"))
                {
                    break;
                }
            }
            return bodyExprs;
        }
        
        private List<string> GetAsmInstructions()
        {
            List<string> asmExprs = new();
            Expect("LBRACE", "'{' before Assembly Block body");
            while (!TypeMatch("RBRACE"))
            {
                asmExprs.Add(GetInstruction());

                if (isAtEnd())
                {
                    Expect("RBRACE", "'}' after Assembly Block");
                }
                if (TypeMatch("RBRACE"))
                {
                    break;
                }
            }
            return asmExprs;
        }

        private string GetInstruction()
        {
            string instruction = "";
            Expect("LBRACE", "'{' before Assembly Block body");
            int step = 0;
            while (!TypeMatch("RBRACE"))
            {
                if (TypeMatch("IDENTIFIER")) 
                {
                    switch (step)
                    {
                        case 0:
                            instruction += previous().lexeme;
                            break;
                        case 1:
                            instruction += ("\t" + previous().lexeme);
                            break;
                        default:
                            instruction += (" " + previous().lexeme);
                            break;
                    }
                    step++;
                }
                else if (TypeMatch("DOT", "LBRACKET"))
                {
                    switch (step)
                    {
                        case 0:
                            instruction += previous().lexeme;
                            instruction += current.lexeme;
                            break;
                        case 1:
                            instruction += ("\t" + previous().lexeme);
                            instruction += current.lexeme;
                            break;
                        default:
                            instruction += (" " + previous().lexeme);
                            instruction += current.lexeme;
                            break;
                    }
                    step++;
                    advance();
                }
                else
                {
                    instruction += current.lexeme;
                    advance();
                }
                if (isAtEnd())
                {
                    Expect("RBRACE", "'}', '{}' must surround Assembly instruction");
                }
                if (TypeMatch("RBRACE"))
                {
                    break;
                }
            }
            return instruction;
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

        private bool TypeMatch(string[] type1, string[] type2)
        {
            if (current == null)
            {
                return false;
            }
            int t = 0;
            foreach (var type in type1)
            {
                if (current.type == type)
                {
                    advance();
                    t++;
                    break;
                }
            }
            foreach (var type in type2)
            {
                if (current.type == type)
                {
                    advance();
                    t++;
                    break;
                }
            }
            if (t == 1)
                back();
            return (t == 2);
        }

        private void Expect(string type, string errorMessage)
        {
            if (current != null && current.type == type)
            {
                advance();
                return;
            }

            throw new Errors.ParseError(ErrorType.ParserException, $"{type}", "Expected " + errorMessage + $"{((current != null)? "\nGot: '" + current.lexeme + "' Instead" : "")}");
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

        private void back()
        {
            index--;
            if (!isAtEnd(index - 1))
            {
                current = tokens[index];
                return;
            }
            current = null;
        }

        private bool isAtEnd()
        {
            return (index >= tokens.Count || index < 0);
        }
        private bool isAtEnd(int idx)
        {
            return (idx >= tokens.Count || idx < 0);
        }
    }
}
