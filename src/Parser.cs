using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze
{
    internal class Parser
    {
        List<Token> tokens;
        List<Expr> expressions;
        Token? current;
        int index;

        public static readonly Token.TokenType[] Literals = 
        { 
            Token.TokenType.INTEGER, 
            Token.TokenType.FLOATING, 
            Token.TokenType.STRING, 
            Token.TokenType.BINARY, 
            Token.TokenType.HEX,
            Token.TokenType.BOOLEAN
        };

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

        private Expr.Definition _Definition()
        {
            if (!isAtEnd() && ReservedValueMatch("function", "class", "primitive"))
            {
                Token definitionType = previous();
                if (definitionType.lexeme == "function")
                {
                    Dictionary<string, bool> modifiers = new()
                    {
                        { "static", false },
                        { "unsafe", false },
                        { "operator", false },
                        { "inline", false }
                    };

                    Expr.TypeReference _return;

                    while (modifiers.ContainsKey(current.lexeme))
                    {
                        modifiers[current.lexeme] = true;
                        advance();
                    }

                    if (peek().type == Token.TokenType.IDENTIFIER)
                    {
                        Expect(Token.TokenType.IDENTIFIER, "function return type");
                        _return = new(new());
                        _return.typeName.Enqueue(previous());

                        if (TypeMatch(Token.TokenType.DOT))
                        {
                            _return = new Expr.TypeReference(GetTypeGetter());
                        }
                    }
                    else
                    {
                        _return = new(new());
                        _return.typeName.Enqueue(new Token(Token.TokenType.RESERVED, "void"));
                    }

                    Expect(Token.TokenType.IDENTIFIER, definitionType.type + " name");
                    Token name = previous();
                    Expect(Token.TokenType.LPAREN, "'(' after function name");
                    List<Expr.Parameter> parameters = new();
                    while (!TypeMatch(Token.TokenType.RPAREN))
                    {
                        Expect(Token.TokenType.IDENTIFIER, "identifier as function parameter type");
                        var typeName = GetTypeGetter();

                        Expect(Token.TokenType.IDENTIFIER, "identifier as function parameter");
                        Token variable = previous();

                        parameters.Add(new Expr.Parameter(typeName, variable));
                        if (TypeMatch(Token.TokenType.RPAREN))
                        {
                            break;
                        }
                        Expect(Token.TokenType.COMMA, "',' between parameters");
                        if (isAtEnd())
                        {
                            throw new Errors.ParseError("Unexpected End In Function Parameters", $"Function '{name.lexeme}' reached an unexpected end during it's parameters");
                        }
                    }

                    List<Expr> block = new();

                    Expect(Token.TokenType.LBRACE, "'{' before class body");
                    while (!TypeMatch(Token.TokenType.RBRACE))
                    {
                        block.Add(Start());

                        if (isAtEnd())
                        {
                            Expect(Token.TokenType.RBRACE, "'}' after block");
                        }
                        if (TypeMatch(Token.TokenType.RBRACE))
                        {
                            break;
                        }
                    }

                    return new Expr.Function(modifiers, _return, name, parameters, block);
                }
                else if (definitionType.lexeme == "class")
                {
                    Expect(Token.TokenType.IDENTIFIER, definitionType.type + " name");
                    Token name = previous();

                    if (Literals.Contains(name.type))
                    {
                        throw new Errors.ParseError("Invalid Class", $"The name of a class may not be a literal ({name.lexeme})");
                    }

                    List<Expr.Declare> declarations = new();
                    List<Expr.Definition> definitions = new();

                    Expect(Token.TokenType.LBRACE, "'{' before class body");
                    while (!TypeMatch(Token.TokenType.RBRACE))
                    {
                        Expr.Declare? declExpr = FullDeclare();
                        if (declExpr != null)
                        {
                            declarations.Add(declExpr);
                        }
                        else
                        {
                            Expr.Definition? definitionExpr = _Definition();
                            if (definitionExpr != null)
                            {
                                definitions.Add(definitionExpr);
                            }
                            else
                            {
                                throw new Errors.ParseError("Invalid Class Definition", $"A class may only contain declarations and definitions. Got '{previous().lexeme}'");
                            }
                        }

                        if (isAtEnd())
                        {
                            Expect(Token.TokenType.RBRACE, "'}' after block");
                        }
                    }

                    return new Expr.Class(name, declarations, definitions, new(null));
                }
                else if (definitionType.lexeme == "primitive")
                {
                    ExpectValue(Token.TokenType.RESERVED, "class", "'class' keyword");
                    Expect(Token.TokenType.IDENTIFIER, "name of primitive type");
                    var name = previous();

                    if (Literals.Contains(name.type))
                    {
                        throw new Errors.ParseError("Invalid Primitive", $"The name of a primitive may not be a literal ({name.lexeme})");
                    }

                    ExpectValue(Token.TokenType.IDENTIFIER, "sizeof", "'sizeof' keyword");
                    Expect(Token.TokenType.INTEGER, "size (in bytes) of primitive");
                    Token size = previous();

                    if (!new List<string>() { "8", "4", "2", "1" }.Contains(size.lexeme))
                    {
                        throw new Errors.ParseError("Invalid Primitive", "The size of primitive classes must be the integers '8', '4', '2', or '1'");
                    }

                    Expr.TypeReference type = new(null);

                    if (ValueMatch("extends"))
                    {
                        Expect(Token.TokenType.IDENTIFIER, "superclass of primitive type");
                        type = new(new());
                        type.typeName.Enqueue(previous());


                        if (!Enum.TryParse<Token.TokenType>(previous().lexeme, out var literalEnum) || !Literals.Contains(literalEnum))
                        {
                            throw new Errors.ParseError("Invalid Primitive", $"The superclass of a primitive must be a valid literal ({string.Join(", ", Literals)}). Got '{previous().lexeme}'");
                        }
                    }

                    List<Expr.Definition> definitions = new();
                    Expect(Token.TokenType.LBRACE, "'{' before primitive class body");
                    while (!TypeMatch(Token.TokenType.RBRACE))
                    {
                        Expr.Definition? bodyExpr = _Definition();
                        if (bodyExpr != null)
                        {
                            definitions.Add(bodyExpr);
                        }
                        else
                        {
                            throw new Errors.ParseError("Invalid Class Definition", $"A class may only contain declarations and definitions. Got '{previous().lexeme}'");
                        }

                        if (isAtEnd())
                        {
                            Expect(Token.TokenType.RBRACE, "'}' after block");
                        }
                    }

                    return new Expr.Primitive(name, definitions, int.Parse(size.lexeme), type);
                }
            }
            return null;
        }

        private Expr Definition()
        {
            return _Definition() ?? Entity();
        }

        private Expr Entity()
        {
            if (!isAtEnd() && ReservedValueMatch("asm", "define"))
            {
                Token definitionType = previous();
                if (definitionType.lexeme == "asm")
                {
                    var instructions = GetAsmInstructions();
                    return new Expr.Assembly(instructions.Item1, instructions.Item2);
                }
                else if (definitionType.lexeme == "define")
                {
                    Expect(Token.TokenType.IDENTIFIER, "name of 'Define'");
                    var name = previous();

                    return new Expr.Define(name, Literal()
                        ?? throw new Errors.ParseError("Invalid Define", "The value of 'Define' should be a literal"));
                }
            }
            return Conditional();
        }

        private Expr Conditional()
        {
            if (!isAtEnd() && ReservedValueMatch("if", "else", "while", "for"))
            {
                Token conditionalType = previous();
                Expr.Block block;

                switch (conditionalType.lexeme)
                {
                    case "if":
                    {
                        Expr condition = GetCondition();
                        block = GetBlock(conditionalType.lexeme);
                        var expr = new Expr.If(condition, block);
                        while (ReservedValueMatch("else"))
                        {
                            if (ReservedValueMatch("if"))
                            {
                                condition = GetCondition();
                                block = GetBlock("if else");
                                expr.ElseIfs.Add(new Expr.ElseIf(condition, block));
                            }
                            else
                            {
                                block = GetBlock("else");
                                expr._else = new Expr.Else(block);
                            }
                        }
                        return expr;
                    }
                    case "else":
                    {
                        if (current.lexeme == "if")
                        {
                            throw new Errors.AnalyzerError("Invalid Else If", "'else if' conditional has no matching 'if'");
                        }
                        else
                        {
                            throw new Errors.AnalyzerError("Invalid Else", "'else' conditional has no matching 'if'");
                        }
                        
                    }
                    case "while":
                    {
                        Expr condition = GetCondition();
                        block = GetBlock(conditionalType.lexeme);
                        return new Expr.While(condition, block);
                    }
                    case "for":
                    {
                        Expect(Token.TokenType.LPAREN, "'(' after 'for'");
                        var initExpr = Logical();
                        Expect(Token.TokenType.SEMICOLON, "';' after expression");
                        var condition = Logical();
                        Expect(Token.TokenType.SEMICOLON, "';' after expression");
                        var updateExpr = Logical();
                        Expect(Token.TokenType.RPAREN, "')' after update of For Statement");
                        block = GetBlock(conditionalType.lexeme);
                        return new Expr.For(condition, block, initExpr, updateExpr);
                    }
                }
            }
            return Semicolon();
        }

        private Expr Semicolon()
        {
            Expr expr = NoSemicolon();
            Expect(Token.TokenType.SEMICOLON, "';' after expression");
            return expr;
        }

        private Expr NoSemicolon()
        {
            Expr expr = Return();
            return expr;
        }

        private Expr Return()
        {
            Expr expr;

            if (ReservedValueMatch("return"))
            {
                if (current.type == Token.TokenType.SEMICOLON)
                {
                    expr = new Expr.Return(new Expr.Keyword("void"), true);
                }
                else
                {
                    expr = new Expr.Return(Logical(), false);
                }
            }
            else
            {
                expr = Logical();
            }
            return expr;
        }

        private Expr Logical()
        {
            Expr expr = Bitwise();
            while (!isAtEnd() && TypeMatch(Token.TokenType.AND, Token.TokenType.OR))
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
            while (!isAtEnd() && TypeMatch(Token.TokenType.B_OR, Token.TokenType.B_AND, Token.TokenType.B_XOR, Token.TokenType.B_NOT))
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
            while (!isAtEnd() && TypeMatch(Token.TokenType.EQUALTO, Token.TokenType.NOTEQUALTO))
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
            while (!isAtEnd() && TypeMatch(Token.TokenType.GREATEREQUAL, Token.TokenType.LESSEQUAL, Token.TokenType.GREATER, Token.TokenType.LESS))
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
            while (!isAtEnd() && TypeMatch(Token.TokenType.PLUS, Token.TokenType.MINUS))
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
            while (!isAtEnd() && TypeMatch(Token.TokenType.MULTIPLY, Token.TokenType.DIVIDE, Token.TokenType.MODULO))
            {
                Token op = previous();
                Expr right = Unary();
                expr = new Expr.Binary(expr, op, right);
            }
            return expr;
        }

        private Expr Unary()
        {
            while (!isAtEnd() && TypeMatch(Token.TokenType.NOT, Token.TokenType.MINUS))
            {
                Token op = previous();
                Expr right = Unary();
                return new Expr.Unary(op, right);
            }
            return Is();
        }

        private Expr Is()
        {
            Expr expr = Primary();
            while (!isAtEnd() && ReservedValueMatch("is"))
            {
                Expect(Token.TokenType.IDENTIFIER, "type after 'is' operator");
                expr = new Expr.Is(expr, new Expr.TypeReference(GetTypeGetter()));
            }
            return expr;
        }

        private Expr Primary()
        {
            if (!isAtEnd())
            {
                Expr? expr = Literal();

                if (expr != null)
                {
                    return expr;
                }

                if (TypeMatch(Token.TokenType.LPAREN))
                {
                    Expr logical = Logical();
                    Expect(Token.TokenType.RPAREN, "')' after expression.");
                    return new Expr.Grouping(logical);
                }

                if (TypeMatch(Token.TokenType.IDENTIFIER) || ReservedValueMatch("this"))
                {
                    var variable = GetGetter();

                    if (TypeMatch(Token.TokenType.IDENTIFIER) || ReservedValueMatch("this"))
                    {
                        expr = Declare(variable);
                    }
                    else if (TypeMatch(Token.TokenType.EQUALS))
                    {
                        if (variable.Item2)
                        {
                            throw new Errors.ParseError("Invalid Assign Statement", "Cannot assign to a non-variable");
                        }
                        expr = new Expr.Assign((Expr.Variable)variable.Item1, NoSemicolon());
                    }
                    else if (TypeMatch(new[] { Token.TokenType.PLUS, Token.TokenType.MINUS, Token.TokenType.MULTIPLY, Token.TokenType.DIVIDE, Token.TokenType.MODULO }, new[] { Token.TokenType.EQUALS }))
                    {
                        if (variable.Item2)
                        {
                            throw new Errors.ParseError("Invalid Assign Statement", "Cannot assign to a non-variable");
                        }
                        var sign = tokens[index - 2];
                        expr = new Expr.Assign((Expr.Variable)variable.Item1, sign, NoSemicolon());
                    }
                    else if (TypeMatch(Token.TokenType.PLUSPLUS, Token.TokenType.MINUSMINUS))
                    {
                        if (variable.Item2)
                        {
                            throw new Errors.ParseError("Invalid Unary Operator", "Cannot assign to a non-variable");
                        }
                        expr = new Expr.Unary(previous(), (Expr.Variable)variable.Item1);
                    }
                    else
                    {
                        expr = variable.Item1;
                    }

                    return expr;
                }

                if (ReservedValueMatch("null", "true", "false"))
                {
                    return new Expr.Keyword(previous().lexeme);
                }

                if (ReservedValueMatch("new"))
                {
                    Expect(Token.TokenType.IDENTIFIER, "identifier after new expression");
                    return new Expr.New(GetNewGetter());
                }
            }
            throw End();
        }

        private Expr.Literal? Literal()
        {
            if (TypeMatch(Literals))
            {
                return new Expr.Literal(previous());
            }
            return null;
        }

        private Expr.Declare? FullDeclare()
        {
            if (TypeMatch(Token.TokenType.IDENTIFIER))
            {
                Expr.Declare declare = null;

                var variable = GetGetter();

                if (TypeMatch(Token.TokenType.IDENTIFIER))
                {
                    declare = Declare(variable);
                }
                else
                {
                    throw new Errors.ParseError("Invalid Class Definition", $"A class may only contain declarations and definitions");
                }
                Expect(Token.TokenType.SEMICOLON, "';' after expression");
                return declare;
            }
            return null;
        }

        private Expr.Declare Declare((Expr.GetReference, bool) variable)
        {
            if (variable.Item2)
            {
                throw new Errors.ParseError("Invalid Assign Statement", "Cannot assign to a non-variable");
            }

            var name = previous();
            if (name.lexeme == "this")
            {
                throw new Errors.ParseError("Invalid 'This' Keyword", "The 'this' keyword may only be used in a member to reference the enclosing class");
            }
            Expect(Token.TokenType.EQUALS, "'=' when declaring variable");

            return new Expr.Declare(variable.Item1.typeName, name, NoSemicolon());
        }

        private (Expr.GetReference, bool) GetGetter()
        {
            Queue<Token> typeName = new Queue<Token>();
            typeName.Enqueue(previous());

            if (peek().type != Token.TokenType.DOT && TypeMatch(Token.TokenType.LPAREN))
            {
                return (new Expr.Call(typeName.Dequeue(), null, null, GetArgs()), true);
            }

            while (TypeMatch(Token.TokenType.DOT))
            {
                Expect(Token.TokenType.IDENTIFIER, "variable name after '.'");
                var variable = previous();

                if (TypeMatch(Token.TokenType.LPAREN))
                {
                    var args = GetArgs();
                    return (new Expr.Call(variable, typeName, (peek().type != Token.TokenType.DOT) ? null : GetGetter().Item1, args), true);
                }
                else
                {
                    typeName.Enqueue(variable);
                }
            }

            return (new Expr.Variable(typeName), false);
        }

        private Expr.Call GetNewGetter()
        {
            Queue<Token> typeName = new Queue<Token>();
            typeName.Enqueue(previous());

            if (peek().type != Token.TokenType.DOT && TypeMatch(Token.TokenType.LPAREN))
            {
                return new Expr.Call(typeName.Dequeue(), null, null, GetArgs());
            }

            while (TypeMatch(Token.TokenType.DOT))
            {
                Expect(Token.TokenType.IDENTIFIER, "variable name after '.'");

                if (TypeMatch(Token.TokenType.LPAREN))
                {
                    return new Expr.Call(previous(2), typeName, null, GetArgs());
                }
                else
                {
                    typeName.Enqueue(previous());
                }
            }

            throw Expected(Token.TokenType.LPAREN.ToString(), "'(' after type in new expression");
        }

        private Queue<Token> GetTypeGetter()
        {
            Queue<Token> typeName = new Queue<Token>();
            typeName.Enqueue(previous());

            while (TypeMatch(Token.TokenType.DOT))
            {
                Expect(Token.TokenType.IDENTIFIER, "variable name after '.'");
                typeName.Enqueue(previous());
            }

            return typeName;
        }


        private Exception End()
        {
            return new Errors.ParseError("Expression Reached Unexpected End", $"Expression '{((previous() != null)? previous().lexeme : "")}' reached an unexpected end");
        }

        private List<Expr> GetArgs()
        {
            List<Expr> arguments = new();
            while (!TypeMatch(Token.TokenType.RPAREN))
            {
                arguments.Add(Logical());
                if (TypeMatch(Token.TokenType.RPAREN))
                {
                    break;
                }
                Expect(Token.TokenType.COMMA, "',' between parameters");
            }
            return arguments;
        }

        private Expr GetCondition()
        {
            Expect(Token.TokenType.LPAREN, "'(' after conditional");
            var condition = Logical();
            Expect(Token.TokenType.RPAREN, "')' after conditional");
            return condition;
        }

        private Expr.Block GetBlock(string bodytype)
        {
            return new Expr.Block(GetBlockItems(bodytype));
        }

        private List<Expr> GetBlockItems(string bodytype)
        {
            List<Expr> bodyExprs = new();
            Expect(Token.TokenType.LBRACE, "'{' before " + bodytype + " body");
            while (!TypeMatch(Token.TokenType.RBRACE))
            {
                bodyExprs.Add(Start());
                if (isAtEnd())
                {
                    Expect(Token.TokenType.RBRACE, "'}' after block");
                }
                if (TypeMatch(Token.TokenType.RBRACE))
                {
                    break;
                }
            }
            return bodyExprs;
        }
        
        private (List<Expr.Assembly.AssignableInstruction>, List<Expr.Variable>) GetAsmInstructions()
        {
            List<Expr.Assembly.AssignableInstruction> instructions = new();
            List<Expr.Variable> variables = new();

            Expect(Token.TokenType.LBRACE, "'{' before Assembly Block body");
            while (!TypeMatch(Token.TokenType.RBRACE))
            {
                if (TypeMatch(Token.TokenType.IDENTIFIER))
                {
                    var op = previous();

                    if (TypeMatch(Token.TokenType.IDENTIFIER, Token.TokenType.DOLLAR))
                    {
                        // Unary
                        Instruction.Value? value;

                        if (previous().type == Token.TokenType.DOLLAR)
                        {
                            Expect(Token.TokenType.IDENTIFIER, "after escape '$'");
                            Queue<Token> queue = new();
                            queue.Enqueue(previous());
                            value = null;
                            variables.Add(new Expr.Variable(queue));
                        }
                        else
                        {
                            var identifier = previous();
                            if (InstructionUtils.Registers.TryGetValue(identifier.lexeme, out var reg))
                            {
                                value = new Instruction.Register(reg.Item1, reg.Item2);
                            }
                            else
                            {
                                throw new Errors.ParseError("Invalid Assembly Register", $"Invalid assembly register given '{identifier}'");
                            }
                        }

                        if (TypeMatch(Token.TokenType.COMMA))
                        {
                            // Binary

                            if (TypeMatch(Token.TokenType.IDENTIFIER))
                            {
                                var identifier = previous();
                                if (InstructionUtils.Registers.TryGetValue(identifier.lexeme, out var reg))
                                {
                                    instructions.Add(new Expr.Assembly.AssignableInstructionBin(new Instruction.Binary(op.lexeme, value, new Instruction.Register(reg.Item1, reg.Item2)), Expr.Assembly.AssignableInstructionBin.AssignType.AssignNone));
                                }
                                else
                                {
                                    throw new Errors.ParseError("Invalid Assembly Register", $"Invalid assembly register given '{identifier.lexeme}'");
                                }
                            }
                            else if (TypeMatch(Token.TokenType.INTEGER, Token.TokenType.FLOATING, Token.TokenType.STRING, Token.TokenType.HEX, Token.TokenType.BINARY))
                            {
                                instructions.Add(new Expr.Assembly.AssignableInstructionBin(new Instruction.Binary(op.lexeme, value, new Instruction.Literal(previous().type, previous().lexeme)), (value == null)? Expr.Assembly.AssignableInstructionBin.AssignType.AssignFirst : Expr.Assembly.AssignableInstructionBin.AssignType.AssignNone));
                            }
                            else if (TypeMatch(Token.TokenType.DOLLAR))
                            {
                                Expect(Token.TokenType.IDENTIFIER, "after escape '$'");
                                Queue<Token> queue = new();
                                queue.Enqueue(previous());
                                variables.Add(new Expr.Variable(queue));
                                instructions.Add(new Expr.Assembly.AssignableInstructionBin(new Instruction.Binary(op.lexeme, value, null), (value == null)? (Expr.Assembly.AssignableInstructionBin.AssignType.AssignFirst | Expr.Assembly.AssignableInstructionBin.AssignType.AssignSecond) : Expr.Assembly.AssignableInstructionBin.AssignType.AssignSecond));
                            }
                            else
                            {
                                Expected("IDENTIFIER, INTEGER, FLOAT, STRING, HEX, BINARY", "operand after comma ','");
                            }
                        }
                        else
                        {
                            instructions.Add(new Expr.Assembly.AssignableInstructionUn(new Instruction.Unary(op.lexeme, value), (value == null)? Expr.Assembly.AssignableInstructionUn.AssignType.AssignFirst : Expr.Assembly.AssignableInstructionUn.AssignType.AssignNone));
                        }
                    }
                    else
                    {
                        // Zero
                        instructions.Add(new Expr.Assembly.AssignableInstructionZ(new Instruction.Zero(op.lexeme)));
                    }
                    Expect(Token.TokenType.SEMICOLON, "';' after Assembly statement");
                }
                else
                {
                    throw new Errors.ParseError("Invalid Assembly Statement", $"'{current.lexeme}' is invalid in an assembly block");
                }

                if (isAtEnd())
                {
                    Expect(Token.TokenType.RBRACE, "'}' after Assembly Block");
                }
                if (TypeMatch(Token.TokenType.RBRACE))
                {
                    break;
                }
            }
            return (instructions, variables);
        }

        private bool TypeMatch(params Token.TokenType[] types)
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

        private bool ValueMatch(params string[] types)
        {
            if (current == null)
            {
                return false;
            }
            foreach (var type in types)
            {
                if (current.lexeme == type)
                {
                    advance();
                    return true;
                }
            }
            return false;
        }

        private bool ReservedValueMatch(params string[] types)
        {
            // Note: Only checks lexeme and assumes there are no cases where an identifier has the name as a reserved keyword
            // This also applies when checking for 'void' and 'this' in other places of the code
            if (current == null)
            {
                return false;
            }
            foreach (var type in types)
            {
                if (current.lexeme == type)
                {
                    advance();
                    return true;
                }
            }
            return false;
        }

        private bool TypeMatch(Token.TokenType[] type1, Token.TokenType[] type2)
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

        private void Expect(Token.TokenType type, string errorMessage)
        {
            if (current != null && current.type == type)
            {
                advance();
                return;
            }
            throw Expected(type.ToString(), errorMessage);
        }

        private void ExpectValue(Token.TokenType type, string value, string errorMessage)
        {
            if (current != null && current.type == type && current.lexeme == value)
            {
                advance();
                return;
            }
            throw Expected(type + " : " + value, errorMessage);
        }

        private Errors.ParseError Expected(string type, string errorMessage)
        {
            return new Errors.ParseError($"{type}", "Expected " + errorMessage + $"{((current != null) ? "\nGot: '" + current.lexeme + "' Instead" : "")}");
        }

        private Token? previous(int sub=1)
        {
            if (!isAtEnd(index - sub))
            {
                return tokens[index - sub];
            }
            return null;
        }

        private Token? peek()
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
