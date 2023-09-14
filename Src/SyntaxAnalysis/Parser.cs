using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

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
        Advance();
    }

    internal List<Expr> Parse()
    {
        while (!IsAtEnd())
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
        if (ReservedValueMatch("function", "class", "primitive"))
        {
            Token definitionType = Previous();
            if (definitionType.lexeme == "function")
            {
                ExprUtils.Modifiers modifiers = new(
                    "static",
                    "unsafe",
                    "operator",
                    "inline"
                );

                Expr.TypeReference _return;

                if (IsAtEnd())
                {
                    throw End();
                }

                while (modifiers.ContainsModifier(current.lexeme))
                {
                    modifiers[current.lexeme] = true;
                    Advance();
                }

                if (Peek().type == Token.TokenType.IDENTIFIER)
                {
                    Expect(Token.TokenType.IDENTIFIER, "function return type");
                    _return = new(new());
                    _return.typeName.Enqueue(Previous());

                    if (TypeMatch(Token.TokenType.DOT))
                    {
                        _return = new Expr.TypeReference(GetTypeReference());
                    }
                }
                else
                {
                    _return = new(new());
                    _return.typeName.Enqueue(new Token(Token.TokenType.RESERVED, "void"));
                }

                Expect(Token.TokenType.IDENTIFIER, definitionType.lexeme + " name");
                Token name = Previous();
                Expect(Token.TokenType.LPAREN, "'(' after function name");
                List<Expr.Parameter> parameters = new();
                while (!TypeMatch(Token.TokenType.RPAREN))
                {
                    ExprUtils.Modifiers paramModifers = new("ref", "inlineRef");

                    
                    paramModifers["ref"] = ReservedValueMatch("ref");
                    paramModifers["inlineRef"] = true;

                    Expect(Token.TokenType.IDENTIFIER, "identifier as function parameter type");
                    var typeName = GetTypeReference();

                    Expect(Token.TokenType.IDENTIFIER, "identifier as function parameter");
                    Token variable = Previous();

                    parameters.Add(new Expr.Parameter(typeName, variable, paramModifers));
                    if (TypeMatch(Token.TokenType.RPAREN))
                    {
                        break;
                    }
                    Expect(Token.TokenType.COMMA, "',' between parameters");
                    if (IsAtEnd())
                    {
                        throw new Error.ParseError("Unexpected End In Function Parameters", $"Function '{name.lexeme}' reached an unexpected end during it's parameters");
                    }
                }

                List<Expr> block = new();

                Expect(Token.TokenType.LBRACE, "'{' before function body");
                while (!TypeMatch(Token.TokenType.RBRACE))
                {
                    block.Add(Start());

                    if (IsAtEnd())
                    {
                        Expect(Token.TokenType.RBRACE, "'}' after function body");
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
                Expect(Token.TokenType.IDENTIFIER, definitionType.lexeme + " name");
                Token name = Previous();

                if (Literals.Contains(name.type))
                {
                    throw new Error.ParseError("Invalid Class", $"The name of a class may not be a literal ({name.lexeme})");
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
                            throw new Error.ParseError("Invalid Class Definition", $"A class may only contain declarations and definitions. Got '{Previous().lexeme}'");
                        }
                    }

                    if (IsAtEnd())
                    {
                        Expect(Token.TokenType.RBRACE, "'}' after class body");
                    }
                }

                return new Expr.Class(name, declarations, definitions, new(null));
            }
            else if (definitionType.lexeme == "primitive")
            {
                ExpectValue(Token.TokenType.RESERVED, "class", "'class' keyword");
                Expect(Token.TokenType.IDENTIFIER, definitionType.lexeme + " name");
                var name = Previous();

                if (Literals.Contains(name.type))
                {
                    throw new Error.ParseError("Invalid Primitive", $"The name of a primitive may not be a literal ({name.lexeme})");
                }

                ExpectValue(Token.TokenType.IDENTIFIER, "sizeof", "'sizeof' keyword");
                Expect(Token.TokenType.INTEGER, "size (in bytes) of primitive");
                Token size = Previous();

                if (!new List<string>() { "8", "4", "2", "1" }.Contains(size.lexeme))
                {
                    throw new Error.ParseError("Invalid Primitive", "The size of primitive classes must be the integers '8', '4', '2', or '1'");
                }

                Expr.TypeReference type = new(null);

                if (ValueMatch("extends"))
                {
                    Expect(Token.TokenType.IDENTIFIER, "superclass of primitive type");
                    type = new(new());
                    type.typeName.Enqueue(Previous());


                    if (!Enum.TryParse<Token.TokenType>(Previous().lexeme, out var literalEnum) || !Literals.Contains(literalEnum))
                    {
                        throw new Error.ParseError("Invalid Primitive", $"The superclass of a primitive must be a valid literal ({string.Join(", ", Literals)}). Got '{Previous().lexeme}'");
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
                        throw new Error.ParseError("Invalid Class Definition", $"A primitive class may only contain definitions. Got '{Previous().lexeme}'");
                    }

                    if (IsAtEnd())
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
        return SymbolTableSingleton.SymbolTable.AddGlobal(_Definition()) ?? Entity();
    }

    private Expr Entity()
    {
        if (ReservedValueMatch("asm"))
        {
            var instructions = GetAsmInstructions();
            return new Expr.Assembly(instructions.Item1, instructions.Item2);
        }
        return Conditional();
    }

    private Expr Conditional()
    {
        if (ReservedValueMatch("if", "else", "while", "for"))
        {
            Token conditionalType = Previous();
            Expr.Block block;

            switch (conditionalType.lexeme)
            {
                case "if":
                {
                    var expr = new Expr.If(new(GetCondition(), GetBlock(conditionalType.lexeme)));

                    while (ReservedValueMatch("else"))
                    {
                        if (ReservedValueMatch("if"))
                        {
                            expr.conditionals.Add(new Expr.Conditional(GetCondition(), GetBlock("else if")));
                        }
                        else
                        {
                            expr._else = GetBlock("else");
                        }
                    }
                    return expr;
                }
                case "else":
                {
                    if (current.lexeme == "if")
                    {
                        throw new Error.AnalyzerError("Invalid Else If", "'else if' conditional has no matching 'if'");
                    }
                    else
                    {
                        throw new Error.AnalyzerError("Invalid Else", "'else' conditional has no matching 'if'");
                    }
                    
                }
                case "while":
                {
                    Expr condition = GetCondition();
                    block = GetBlock(conditionalType.lexeme + " loop");
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
                    block = GetBlock(conditionalType.lexeme + " loop");
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
        while (TypeMatch(Token.TokenType.AND, Token.TokenType.OR))
        {
            Token op = Previous();
            Expr right = Bitwise();
            expr = new Expr.Logical(expr, op, right);
        }
        return expr;
    }

    private Expr Bitwise()
    {
        Expr expr = Equality();
        while (TypeMatch(Token.TokenType.B_OR, Token.TokenType.B_AND, Token.TokenType.B_XOR, Token.TokenType.B_NOT))
        {
            Token op = Previous();
            Expr right = Equality();
            expr = new Expr.Binary(expr, op, right);
        }
        return expr;
    }

    private Expr Equality()
    {
        Expr expr = Comparison();
        while (TypeMatch(Token.TokenType.EQUALTO, Token.TokenType.NOTEQUALTO))
        {
            Token op = Previous();
            Expr right = Comparison();
            expr = new Expr.Binary(expr, op, right);
        }
        return expr;
    }

    private Expr Comparison()
    {
        Expr expr = Shift();
        while (TypeMatch(Token.TokenType.GREATEREQUAL, Token.TokenType.LESSEQUAL, Token.TokenType.GREATER, Token.TokenType.LESS))
        {
            Token op = Previous();
            Expr right = Shift();
            expr = new Expr.Binary(expr, op, right);
        }
        return expr;
    }

    private Expr Shift()
    {
        Expr expr = Additive();
        while (TypeMatch(Token.TokenType.SHIFTRIGHT, Token.TokenType.SHIFTLEFT))
        {
            Token op = Previous();
            Expr right = Additive();
            expr = new Expr.Binary(expr, op, right);
        }
        return expr;
    }

    private Expr Additive()
    {
        Expr expr = Multiplicative();
        while (TypeMatch(Token.TokenType.PLUS, Token.TokenType.MINUS))
        {
            Token op = Previous();
            Expr right = Multiplicative();
            expr = new Expr.Binary(expr, op, right);
        }
        return expr;
    }

    private Expr Multiplicative()
    {
        Expr expr = Unary();
        while (TypeMatch(Token.TokenType.MULTIPLY, Token.TokenType.DIVIDE, Token.TokenType.MODULO))
        {
            Token op = Previous();
            Expr right = Unary();
            expr = new Expr.Binary(expr, op, right);
        }
        return expr;
    }

    private Expr Unary()
    {
        while (TypeMatch(Token.TokenType.NOT, Token.TokenType.MINUS))
        {
            Token op = Previous();
            Expr right = Unary();
            return new Expr.Unary(op, right);
        }
        return Incrementative();
    }

    private Expr Incrementative()
    {
        Expr expr = Is();
        while (TypeMatch(Token.TokenType.PLUSPLUS, Token.TokenType.MINUSMINUS))
        {
            Token op = Previous();
            expr = new Expr.Unary(op, expr);
        }
        return expr;
    }

    private Expr Is()
    {
        Expr expr = Primary();
        while (ReservedValueMatch("is"))
        {
            Expect(Token.TokenType.IDENTIFIER, "type after 'is' operator");
            expr = new Expr.Is(expr, new Expr.TypeReference(GetTypeReference()));
        }
        return expr;
    }

    private Expr Primary()
    {
        if (!IsAtEnd())
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
                var variable = GetReference();

                if (TypeMatch(Token.TokenType.IDENTIFIER) || ReservedValueMatch("this"))
                {
                    expr = Declare(variable);
                }
                else if (TypeMatch(Token.TokenType.EQUALS))
                {
                    if (variable.IsMethod())
                    {
                        throw new Error.ParseError("Invalid Assign Statement", "Cannot assign to a non-variable");
                    }
                    expr = new Expr.Assign(variable, NoSemicolon());
                }
                else if (TypeMatch(new[] { Token.TokenType.PLUS, Token.TokenType.MINUS, Token.TokenType.MULTIPLY, Token.TokenType.DIVIDE, Token.TokenType.MODULO }, new[] { Token.TokenType.EQUALS }))
                {
                    if (variable.IsMethod())
                    {
                        throw new Error.ParseError("Invalid Assign Statement", "Cannot assign to a non-variable");
                    }
                    var op = tokens[index - 2];
                    expr = new Expr.Assign(variable, new Expr.Binary(variable, op, NoSemicolon()));
                }
                else
                {
                    expr = variable;
                }

                return expr;
            }

            if (ReservedValueMatch("null", "true", "false"))
            {
                return new Expr.Keyword(Previous().lexeme);
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
            return new Expr.Literal(Previous());
        }
        return null;
    }

    private Expr.Declare? FullDeclare()
    {
        if (TypeMatch(Token.TokenType.IDENTIFIER))
        {
            Expr.Declare declare;

            var variable = GetReference();

            if (TypeMatch(Token.TokenType.IDENTIFIER))
            {
                declare = Declare(variable);
            }
            else
            {
                throw new Error.ParseError("Invalid Class Definition", $"A class may only contain declarations and definitions");
            }
            Expect(Token.TokenType.SEMICOLON, "';' after expression");
            return declare;
        }
        return null;
    }

    private Expr.Declare Declare(Expr.GetReference variable)
    {
        if (variable.HasMethod())
        {
            throw new Error.ParseError("Invalid Declare Statement", "Cannot declare to a non-type value");
        }

        var name = Previous();
        if (name.lexeme == "this")
        {
            throw new Error.ParseError("Invalid 'This' Keyword", "The 'this' keyword may only be used in a member to reference the enclosing class");
        }
        Expect(Token.TokenType.EQUALS, "'=' when declaring variable");

        return new Expr.Declare(variable.ToTypeReference().typeName, name, ReservedValueMatch("ref"), NoSemicolon());
    }

    private Expr.GetReference GetReference()
    {
        List<Expr.Getter> getters = new();
        Expr.TypeReference callee = new(GetTypeReference());

        if (TypeMatch(Token.TokenType.LPAREN))
        {
            getters.Add(new Expr.Call(callee.typeName.StackPop(), GetArgs(), (callee.typeName.Count == 0) ? new(null) : callee, false));
        }
        else
        {
            return callee.ToGetReference();
        }

        while (TypeMatch(Token.TokenType.DOT))
        {
            Expect(Token.TokenType.IDENTIFIER, "variable name after '.'");

            if (TypeMatch(Token.TokenType.LPAREN))
            {
                getters.Add(new Expr.Call(Previous(2), GetArgs(), new(null), true));
            }
            else
            {
                getters.Add(new Expr.Get(Previous()));
            }
        }

        return new Expr.GetReference(getters, callee.typeName != null || (callee.typeName == null && getters.Count == 0));
    }

    private Expr.Call GetNewGetter()
    {
        Expr.TypeReference callee = new(GetTypeReference());

        if (TypeMatch(Token.TokenType.LPAREN))
        {
            return new Expr.Call(callee.typeName.StackPop(), GetArgs(), (callee.typeName.Count == 0) ? new(null) : callee, false);
        }

        throw Expected(Token.TokenType.LPAREN.ToString(), "'(' after type in new expression");
    }

    private ExprUtils.QueueList<Token> GetTypeReference()
    {
        ExprUtils.QueueList<Token> typeName = new();
        typeName.Enqueue(Previous());

        while (TypeMatch(Token.TokenType.DOT))
        {
            Expect(Token.TokenType.IDENTIFIER, "variable name after '.'");
            typeName.Enqueue(Previous());
        }

        return typeName;
    }


    private Exception End()
    {
        return new Error.ParseError("Expression Reached Unexpected End", $"Expression '{((Previous() != null)? Previous().lexeme : "")}' reached an unexpected end");
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
            if (IsAtEnd())
            {
                Expect(Token.TokenType.RBRACE, "'}' after " + bodytype + " body");
            }
            if (TypeMatch(Token.TokenType.RBRACE))
            {
                break;
            }
        }
        return bodyExprs;
    }
    
    private (List<ExprUtils.AssignableInstruction>, List<Expr.GetReference>) GetAsmInstructions()
    {
        List<ExprUtils.AssignableInstruction> instructions = new();
        List<Expr.GetReference> variables = new();

        Expect(Token.TokenType.LBRACE, "'{' before Assembly Block body");

        bool returned = false;

        while (!TypeMatch(Token.TokenType.RBRACE))
        {
            bool localReturn = false;
            if (ReservedValueMatch("return"))
            {
                if (returned) { throw new Error.ParseError("Invalid Assembly Block", "Only one return may appear in an assembly block"); }
                returned = true;
                localReturn = true;
            }
            if (TypeMatch(Token.TokenType.IDENTIFIER))
            {
                var op = Previous();

                if (TypeMatch(Token.TokenType.IDENTIFIER, Token.TokenType.DOLLAR))
                {
                    // Unary
                    Instruction.Value? value;

                    if (Previous().type == Token.TokenType.DOLLAR)
                    {
                        Expect(Token.TokenType.IDENTIFIER, "after escape '$'");
                        value = null;
                        variables.Add(new Expr.GetReference(new(){ new Expr.Get(Previous()) }));
                    }
                    else
                    {
                        var identifier = Previous();
                        if (InstructionUtils.Registers.TryGetValue(identifier.lexeme, out var reg))
                        {
                            value = new Instruction.Register(reg.Item1, reg.Item2);
                        }
                        else
                        {
                            throw new Error.ParseError("Invalid Assembly Register", $"Invalid assembly register given '{identifier}'");
                        }
                    }

                    if (TypeMatch(Token.TokenType.COMMA))
                    {
                        // Binary

                        if (TypeMatch(Token.TokenType.IDENTIFIER))
                        {
                            var identifier = Previous();
                            if (InstructionUtils.Registers.TryGetValue(identifier.lexeme, out var reg))
                            {
                                instructions.Add(new ExprUtils.AssignableInstruction.Binary(new Instruction.Binary(op.lexeme, value, new Instruction.Register(reg.Item1, reg.Item2)), ExprUtils.AssignableInstruction.Binary.AssignType.AssignNone, localReturn));
                            }
                            else
                            {
                                throw new Error.ParseError("Invalid Assembly Register", $"Invalid assembly register given '{identifier.lexeme}'");
                            }
                        }
                        else if (TypeMatch(Token.TokenType.INTEGER, Token.TokenType.FLOATING, Token.TokenType.STRING, Token.TokenType.HEX, Token.TokenType.BINARY))
                        {
                            instructions.Add(new ExprUtils.AssignableInstruction.Binary(new Instruction.Binary(op.lexeme, value, new Instruction.Literal(Previous().type, Previous().lexeme)), (value == null)? ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst : ExprUtils.AssignableInstruction.Binary.AssignType.AssignNone, localReturn));
                        }
                        else if (TypeMatch(Token.TokenType.DOLLAR))
                        {
                            Expect(Token.TokenType.IDENTIFIER, "after escape '$'");
                            variables.Add(new Expr.GetReference(new() { new Expr.Get(Previous()) }));
                            instructions.Add(new ExprUtils.AssignableInstruction.Binary(new Instruction.Binary(op.lexeme, value, null), (value == null)? (ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst | ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond) : ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond, localReturn));
                        }
                        else
                        {
                            Expected("IDENTIFIER, INTEGER, FLOAT, STRING, HEX, BINARY", "operand after comma ','");
                        }
                    }
                    else
                    {
                        instructions.Add(new ExprUtils.AssignableInstruction.Unary(new Instruction.Unary(op.lexeme, value), (value == null)? ExprUtils.AssignableInstruction.Unary.AssignType.AssignFirst : ExprUtils.AssignableInstruction.Unary.AssignType.AssignNone, localReturn));
                    }
                }
                else
                {
                    if (localReturn) { throw new Error.ParseError("Invalid Assembly Block", "Return on a zero instruction is not allowed"); }
                    // Zero
                    instructions.Add(new ExprUtils.AssignableInstruction.AssignableInstructionZ(new Instruction.Zero(op.lexeme)));
                }
                Expect(Token.TokenType.SEMICOLON, "';' after Assembly statement");
            }
            else
            {
                throw new Error.ParseError("Invalid Assembly Statement", $"'{current.lexeme}' is invalid in an assembly block");
            }

            if (IsAtEnd())
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
                Advance();
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
                Advance();
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
                Advance();
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
                Advance();
                t++;
                break;
            }
        }
        foreach (var type in type2)
        {
            if (current.type == type)
            {
                Advance();
                t++;
                break;
            }
        }
        if (t == 1)
            current = tokens[--index];
        return (t == 2);
    }

    private void Expect(Token.TokenType type, string errorMessage)
    {
        if (current != null && current.type == type)
        {
            Advance();
            return;
        }
        throw Expected(type.ToString(), errorMessage);
    }

    private void ExpectValue(Token.TokenType type, string value, string errorMessage)
    {
        if (current != null && current.type == type && current.lexeme == value)
        {
            Advance();
            return;
        }
        throw Expected(type + " : " + value, errorMessage);
    }

    private Error.ParseError Expected(string type, string errorMessage)
    {
        return new Error.ParseError($"{type}", "Expected " + errorMessage + $"{((current != null) ? "\nGot: '" + current.lexeme + "' Instead" : "")}");
    }

    private Token Previous(int sub=1)
    {
        if (!IsAtEnd(index - sub))
        {
            return tokens[index - sub];
        }
        Diagnostics.errors.Push(new Error.ImpossibleError("Requested the token before the first token"));
        return null;
    }

    private Token Peek()
    {
        if (!IsAtEnd(index + 1))
        {
            return tokens[index + 1];
        }
        throw End();
    }

    private void Advance()
    {
        index++;
        if (!IsAtEnd())
        {
            current = tokens[index];
            return;
        }
        current = null;
    }

    private bool IsAtEnd()
    {
        return (index >= tokens.Count || index < 0);
    }
    private bool IsAtEnd(int idx)
    {
        return (idx >= tokens.Count || idx < 0);
    }
}
