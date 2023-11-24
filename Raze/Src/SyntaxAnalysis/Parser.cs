using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public class Parser
{
    List<Token> tokens;
    List<Expr> expressions;
    Token? current;
    int index;

    internal const LiteralTokenType VoidTokenType = (LiteralTokenType)(-1);
    internal enum LiteralTokenType
    {
        INTEGER = Token.TokenType.INTEGER,
        FLOATING = Token.TokenType.FLOATING,
        STRING = Token.TokenType.STRING,
        BINARY = Token.TokenType.BINARY,
        HEX = Token.TokenType.HEX,
        BOOLEAN = Token.TokenType.BOOLEAN,
        REF_STRING = Token.TokenType.REF_STRING
    }

    internal static readonly Token.TokenType[] SynchronizationTokens =
    {
        Token.TokenType.SEMICOLON,
        Token.TokenType.COMMA,
        Token.TokenType.RBRACE,
        Token.TokenType.RBRACKET,
        Token.TokenType.RPAREN
    };

    public Parser(List<Token> tokens)
    {
        this.tokens = tokens;
        this.expressions = new();
        this.index = -1;
        Advance();
    }

    public List<Expr> Parse()
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

    private Expr.Definition? _Definition()
    {
        if (ReservedValueMatch("function", "class", "primitive"))
        {
            Token definitionType = Previous();
            if (definitionType.lexeme == "function")
            {
                ExprUtils.Modifiers modifiers = ExprUtils.Modifiers.FunctionModifierTemplate();

                if (IsAtEnd()) return null;

                while (modifiers.ContainsModifier(current.lexeme))
                {
                    modifiers[current.lexeme] = true;
                    Advance();
                }

                Expr.TypeReference _return = new(null);

                Expect(Token.TokenType.IDENTIFIER, definitionType.lexeme + " name");

                if (current.type == Token.TokenType.DOT || current.type == Token.TokenType.IDENTIFIER)
                {
                    _return.typeName = GetTypeReference();

                    if (!Expect(Token.TokenType.IDENTIFIER, definitionType.lexeme + " name"))
                        return null;
                }

                Token name = Previous();

                Expect(Token.TokenType.LPAREN, "'(' after function name");
                if (IsAtEnd()) return null;

                List<Expr.Parameter> parameters = new();
                while (!TypeMatch(Token.TokenType.RPAREN))
                {
                    ExprUtils.Modifiers paramModifers = ExprUtils.Modifiers.ParameterModifierTemplate();

                    paramModifers["ref"] = ReservedValueMatch("ref");

                    if (!Expect(Token.TokenType.IDENTIFIER, "identifier as function parameter type"))
                    {
                        Advance();
                        continue;
                    }
                    var typeName = GetTypeReference();

                    if (!Expect(Token.TokenType.IDENTIFIER, "identifier as function parameter"))
                    {
                        Advance();
                        continue;
                    }
                    Token variable = Previous();

                    parameters.Add(new Expr.Parameter(typeName, variable, paramModifers));
                    if (TypeMatch(Token.TokenType.RPAREN))
                    {
                        break;
                    }
                    Expect(Token.TokenType.COMMA, "',' between parameters");
                    if (IsAtEnd())
                    {
                        Diagnostics.errors.Push(new Error.ParseError("Unexpected End In Function Parameters", $"Function '{name.lexeme}' reached an unexpected end during its parameters"));
                        return new Expr.Function(modifiers, _return, name, parameters, new(new()));
                    }
                }

                return new Expr.Function(modifiers, _return, name, parameters, GetBlock(definitionType.lexeme));
            }
            else if (definitionType.lexeme == "class")
            {
                Expect(Token.TokenType.IDENTIFIER, definitionType.lexeme + " name");
                Token name = Previous();

                if (Enum.GetNames<LiteralTokenType>().Contains(name.type.ToString()))
                {
                    Diagnostics.errors.Push(new Error.ParseError("Invalid Class", $"The name of a class may not be a literal ({name.lexeme})"));
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
                    else if (!IsAtEnd())
                    {
                        Expr.Definition? definitionExpr = _Definition();
                        if (definitionExpr != null)
                        {
                            definitions.Add(definitionExpr);
                        }
                        else
                        {
                            Diagnostics.errors.Push(new Error.ParseError("Invalid Class Definition", $"A class may only contain declarations and definitions. {GotInfo()}"));
                            Advance();
                        }
                    }
                    else
                    {
                        Expect(Token.TokenType.RBRACE, "'}' after class body");
                        break;
                    }
                }

                return new Expr.Class(name, declarations, definitions, new(null));
            }
            else if (definitionType.lexeme == "primitive")
            {
                ExpectValue(Token.TokenType.RESERVED, "class", "'class' keyword");
                Expect(Token.TokenType.IDENTIFIER, definitionType.lexeme + " name");
                var name = Previous();

                if (Enum.GetNames<LiteralTokenType>().Contains(name.type.ToString()))
                {
                    Diagnostics.errors.Push(new Error.ParseError("Invalid Primitive", $"The name of a primitive may not be a literal ({name.lexeme})"));
                }

                ExpectValue(Token.TokenType.IDENTIFIER, "sizeof", "'sizeof' keyword");

                int size = -1;

                if (TypeMatch(Token.TokenType.INTEGER))
                {
                    if (!new List<string>() { "8", "4", "2", "1" }.Contains(Previous().lexeme))
                    {
                        Diagnostics.errors.Push(new Error.ParseError("Invalid Primitive", $"The size of primitive classes must be the integers '8', '4', '2', or '1'. Got: '{Previous().lexeme}'"));
                    }
                    else
                    {
                        size = int.Parse(Previous().lexeme);
                    }
                }
                else
                {
                    Expected("INTEGER", "size (in bytes) of primitive");
                }

                Expr.TypeReference type = null;

                if (ValueMatch("extends"))
                {
                    Expect(Token.TokenType.IDENTIFIER, "superclass of primitive type");
                    type = new(new());
                    type.typeName.Enqueue(Previous());

                    if (!Enum.GetNames<LiteralTokenType>().Contains(Previous().lexeme))
                    {
                        Diagnostics.errors.Push(new Error.ParseError("Invalid Primitive", $"The superclass of a primitive must be a valid literal ({string.Join(", ", Enum.GetValues<LiteralTokenType>())}). Got: '{Previous().lexeme}'"));
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
                    else if (!IsAtEnd())
                    {
                        Diagnostics.errors.Push(new Error.ParseError("Invalid Class Definition", $"A primitive class may only contain definitions. {GotInfo()}"));
                        Advance();
                    }
                    else
                    {
                        Expect(Token.TokenType.RBRACE, "'}' after primitive body");
                        break;
                    }
                }

                return new Expr.Primitive(name, definitions, size, type);
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
        else if (TypeMatch(Token.TokenType.LBRACE))
        {
            current = tokens[--index];
            return GetBlock("disconnected block");
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
                        if (ReservedValueMatch("if"))
                        {
                            Diagnostics.errors.Push(new Error.ParseError("Invalid Else If", "'else if' conditional has no matching 'if'"));
                            return new Expr.If(new(GetCondition(), GetBlock("else if")));
                        }
                        else
                        {
                            Diagnostics.errors.Push(new Error.ParseError("Invalid Else", "'else' conditional has no matching 'if'"));
                            return GetBlock("else");
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
        if (expr is not Expr.NoOp)
        {
            Expect(Token.TokenType.SEMICOLON, "';' after expression");
        }
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
            Expr expr;

            if (TypeMatch(Array.ConvertAll<LiteralTokenType, Token.TokenType>(Enum.GetValues<LiteralTokenType>(), new(x => (Token.TokenType)x))))
            {
                return new Expr.Literal(new LiteralToken((LiteralTokenType)Previous().type, Previous().lexeme));
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

                if (TypeMatch(Token.TokenType.EQUALS))
                {
                    if (variable.IsMethod())
                    {
                        Diagnostics.errors.Push(new Error.ParseError("Invalid Assign Statement", "Cannot assign to a method"));
                    }
                    expr = new Expr.Assign(variable, NoSemicolon());
                }
                else if (TypeMatch(new[] { Token.TokenType.PLUS, Token.TokenType.MINUS, Token.TokenType.MULTIPLY, Token.TokenType.DIVIDE, Token.TokenType.MODULO }, new[] { Token.TokenType.EQUALS }))
                {
                    if (variable.IsMethod())
                    {
                        Diagnostics.errors.Push(new Error.ParseError("Invalid Assign Statement", "Cannot assign to a method"));
                    }
                    var op = tokens[index - 2];
                    expr = new Expr.Assign(variable, new Expr.Binary(variable, op, NoSemicolon()));
                }
                else if (TypeMatch(Token.TokenType.LBRACKET))
                {
                    expr = new Expr.Binary(variable, Previous(), NoSemicolon());
                    Expect(Token.TokenType.RBRACKET, "']' after indexer");
                }
                else if (variable.IsMethod())
                {
                    expr = variable;
                }
                else if (TypeMatch(Token.TokenType.IDENTIFIER) || ReservedValueMatch("this"))
                {
                    expr = Declare(variable);
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
                var newCallExpr = GetNewGetter();
                if (newCallExpr == null)
                {
                    return new Expr.InvalidExpr();
                }
                return new Expr.New(newCallExpr);
            }
        }

        End();
        Advance();
        return new Expr.InvalidExpr();
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
                Diagnostics.errors.Push(new Error.ParseError("Invalid Class Definition", $"A class may only contain declarations and definitions"));
                return null;
            }
            Expect(Token.TokenType.SEMICOLON, "';' after expression");
            return declare;
        }
        return null;
    }

    private Expr.Declare Declare(Expr.GetReference variable)
    {
        if (variable is not Expr.AmbiguousGetReference)
        {
            Diagnostics.errors.Push(new Error.ParseError("Invalid Declare Statement", "Cannot declare to a non-type value"));
        }

        var name = Previous();
        if (name.lexeme == "this")
        {
            Diagnostics.errors.Push(new Error.ParseError("Invalid 'This' Keyword", "The 'this' keyword may only be used in a member to reference the enclosing class"));
            name.type = Token.TokenType.IDENTIFIER;
        }

        Expect(Token.TokenType.EQUALS, "'=' when declaring variable");

        return new Expr.Declare(((Expr.AmbiguousGetReference)variable).typeName, name, ReservedValueMatch("ref"), NoSemicolon());
    }

    private Expr.GetReference GetReference()
    {
        List<Expr.Getter> getters = new();
        Expr.AmbiguousGetReference callee = new(GetTypeReference());

        if (TypeMatch(Token.TokenType.LPAREN))
        {
            getters.Add(new Expr.Call(callee.typeName.StackPop(), GetArgs(), (callee.typeName.Count == 0) ? null : callee));
        }
        else
        {
            callee.instanceCall = true;
            return callee;
        }

        while (TypeMatch(Token.TokenType.DOT))
        {
            Expect(Token.TokenType.IDENTIFIER, "variable name after '.'");

            if (TypeMatch(Token.TokenType.LPAREN))
            {
                var call = new Expr.Call(Previous(2), GetArgs(), new Expr.InstanceGetReference(getters));
                getters = new() { call };
            }
            else
            {
                getters.Add(new Expr.Get(Previous()));
            }
        }

        return new Expr.InstanceGetReference(getters);
    }

    private Expr.Call? GetNewGetter()
    {
        Expr.AmbiguousGetReference callee = new(GetTypeReference());
        callee.instanceCall = false;

        if (TypeMatch(Token.TokenType.LPAREN))
        {
            return new Expr.Call(callee.typeName.StackPop(), GetArgs(), (callee.typeName.Count == 0) ? null : callee);
        }

        Expected(Token.TokenType.LPAREN.ToString(), "'(' after type in new expression");
        return null;
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


    private void End()
    {
        Diagnostics.errors.Push(new Error.ParseError("Expression Reached Unexpected End", $"Expression '{Previous().lexeme}' reached an unexpected end"));
    }

    private List<Expr> GetArgs()
    {
        List<Expr> arguments = new();
        while (!TypeMatch(Token.TokenType.RPAREN))
        {
            arguments.Add(Logical());

            if (TypeMatch(Token.TokenType.COMMA))
            {
                if (TypeMatch(Token.TokenType.COMMA))
                {
                    Diagnostics.errors.Push(new Error.ParseError("Invalid Function Arguments", "Unexpected comma ','"));
                    while (TypeMatch(Token.TokenType.COMMA))
                    {
                        Diagnostics.errors.Push(new Error.ParseError("Invalid Function Arguments", "Unexpected comma ','"));
                    }
                }
                if (TypeMatch(Token.TokenType.RPAREN))
                {
                    Diagnostics.errors.Push(new Error.ParseError("Invalid Function Arguments", "Unexpected comma ','"));
                    break;
                }
            }
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
        if (!TypeMatch(Token.TokenType.LBRACE))
        {
            Expected("LBRACE", "'{' before " + bodytype + " body");
            Advance();
            return new();
        }
        while (!TypeMatch(Token.TokenType.RBRACE))
        {
            bodyExprs.Add(Start());
            if (IsAtEnd())
            {
                Expect(Token.TokenType.RBRACE, "'}' after " + bodytype + " body");
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
                if (returned) { Diagnostics.errors.Push(new Error.ParseError("Invalid Assembly Block", "Only one return may appear in an assembly block")); }
                returned = true;
                localReturn = true;
            }
            if (TypeMatch(Token.TokenType.IDENTIFIER))
            {
                var op = Previous();

                if (TypeMatch(Token.TokenType.IDENTIFIER, Token.TokenType.DOLLAR, Token.TokenType.INTEGER, Token.TokenType.FLOATING, Token.TokenType.STRING, Token.TokenType.HEX, Token.TokenType.BINARY))
                {
                    // Unary
                    AssemblyExpr.Value? value;

                    if (Previous().type == Token.TokenType.DOLLAR)
                    {
                        if (!(TypeMatch(Token.TokenType.IDENTIFIER) || ReservedValueMatch("this")))
                        {
                            Expected("IDENTIFIER, 'this'", "after escape '$'");
                        }
                        value = null;
                        variables.Add(new Expr.AmbiguousGetReference(Previous(), true));
                    }
                    else if (Previous().type == Token.TokenType.IDENTIFIER)
                    {
                        var identifier = Previous();
                        if (InstructionUtils.Registers.TryGetValue(identifier.lexeme, out var reg))
                        {
                            value = new AssemblyExpr.Register(reg.Item1, reg.Item2);
                        }
                        else
                        {
                            Diagnostics.errors.Push(new Error.ParseError("Invalid Assembly Register", $"Invalid assembly register '{identifier}'"));
                            value = new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.TMP, AssemblyExpr.Register.RegisterSize._8Bits);
                        }
                    }
                    else
                    {
                        value = new AssemblyExpr.Literal((LiteralTokenType)Previous().type, Previous().lexeme);
                    }

                    if (TypeMatch(Token.TokenType.COMMA))
                    {
                        // Binary

                        if (TypeMatch(Token.TokenType.IDENTIFIER))
                        {
                            var identifier = Previous();
                            if (InstructionUtils.Registers.TryGetValue(identifier.lexeme, out var reg))
                            {
                                instructions.Add(new ExprUtils.AssignableInstruction.Binary(new AssemblyExpr.Binary(ConvertToInstruction(op.lexeme), value, new AssemblyExpr.Register(reg.Item1, reg.Item2)), ExprUtils.AssignableInstruction.Binary.AssignType.AssignNone, localReturn));
                            }
                            else
                            {
                                Diagnostics.errors.Push(new Error.ParseError("Invalid Assembly Register", $"Invalid assembly register given '{identifier.lexeme}'"));
                                instructions.Add(new ExprUtils.AssignableInstruction.Binary(new AssemblyExpr.Binary(ConvertToInstruction(op.lexeme), value, new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.TMP, AssemblyExpr.Register.RegisterSize._8Bits)), ExprUtils.AssignableInstruction.Binary.AssignType.AssignNone, localReturn));
                            }
                        }
                        else if (TypeMatch(Token.TokenType.INTEGER, Token.TokenType.FLOATING, Token.TokenType.STRING, Token.TokenType.REF_STRING, Token.TokenType.HEX, Token.TokenType.BINARY))
                        {
                            instructions.Add(new ExprUtils.AssignableInstruction.Binary(new AssemblyExpr.Binary(ConvertToInstruction(op.lexeme), value, new AssemblyExpr.Literal((LiteralTokenType)Previous().type, Previous().lexeme)), (value == null) ? ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst : ExprUtils.AssignableInstruction.Binary.AssignType.AssignNone, localReturn));
                        }
                        else if (TypeMatch(Token.TokenType.DOLLAR))
                        {
                            if (!(TypeMatch(Token.TokenType.IDENTIFIER) || ReservedValueMatch("this")))
                            {
                                Expected("IDENTIFIER, 'this'", "after escape '$'");
                            }
                            variables.Add(new Expr.AmbiguousGetReference(Previous(), true));
                            instructions.Add(new ExprUtils.AssignableInstruction.Binary(new AssemblyExpr.Binary(ConvertToInstruction(op.lexeme), value, null), (value == null) ? (ExprUtils.AssignableInstruction.Binary.AssignType.AssignFirst | ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond) : ExprUtils.AssignableInstruction.Binary.AssignType.AssignSecond, localReturn));
                        }
                        else
                        {
                            Expected("IDENTIFIER, INTEGER, FLOAT, STRING, HEX, BINARY", "operand after comma ','");
                        }
                    }
                    else
                    {
                        instructions.Add(new ExprUtils.AssignableInstruction.Unary(new AssemblyExpr.Unary(ConvertToInstruction(op.lexeme), value), (value == null) ? ExprUtils.AssignableInstruction.Unary.AssignType.AssignFirst : ExprUtils.AssignableInstruction.Unary.AssignType.AssignNone, localReturn));
                    }
                }
                else
                {
                    if (localReturn) { Diagnostics.errors.Push(new Error.ParseError("Invalid Assembly Block", "Return on a zero instruction is not allowed")); }
                    // Zero
                    instructions.Add(new ExprUtils.AssignableInstruction.Zero(new AssemblyExpr.Zero(ConvertToInstruction(op.lexeme))));
                }
                Expect(Token.TokenType.SEMICOLON, "';' after Assembly statement");
            }
            else
            {
                Diagnostics.errors.Push(new Error.ParseError("Invalid Assembly Statement", $"'{Previous().lexeme}' is invalid in an assembly block"));
                Advance();
            }

            if (IsAtEnd())
            {
                Expect(Token.TokenType.RBRACE, "'}' after Assembly Block");
                break;
            }
            if (TypeMatch(Token.TokenType.RBRACE))
            {
                break;
            }
        }
        return (instructions, variables);
    }

    private AssemblyExpr.Instruction ConvertToInstruction(string strInstruction)
    {
        if (Enum.TryParse(strInstruction, out AssemblyExpr.Instruction instruction))
        {
            return instruction;
        }
        Diagnostics.errors.Push(new Error.ParseError("Invalid Assembly Block", $"Instruction '{strInstruction}' not supported"));
        return 0;
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

    private bool Expect(Token.TokenType type, string errorMessage)
    {
        if (current != null && current.type == type)
        {
            Advance();
            return true;
        }
        Expected(type.ToString(), errorMessage);

        if (index+1 < tokens.Count && !SynchronizationTokens.Contains(Previous(-1).type))
        {
            Advance();
            return Expect(type, errorMessage);
        }
        return false;
    }

    private void ExpectValue(Token.TokenType type, string value, string errorMessage)
    {
        if (current != null && current.type == type && current.lexeme == value)
        {
            Advance();
            return;
        }
        Expected(type + " : " + value, errorMessage);
    }

    private void Expected(string type, string errorMessage)
    {
        Diagnostics.errors.Push(new Error.ParseError($"{type}", "Expected " + errorMessage + "\n" + GotInfo()));
    }

    private string GotInfo() => $"Got: '{current?.lexeme}'";

    private Token Previous(int sub = 1)
    {
        return tokens[index - sub];
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
}
