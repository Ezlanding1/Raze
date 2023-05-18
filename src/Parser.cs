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

        public static readonly string[] Literals = 
        { 
            "INTEGER", 
            "FLOAT", 
            "STRING", 
            "BINARY", 
            "HEX",
            "BOOLEAN"
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

        private Expr Definition()
        {
            if (!isAtEnd() && TypeMatch("function", "class", "asm", "define", "primitive"))
            {
                Token definitionType = previous();
                if (definitionType.type == "function")
                {
                    var function = new Expr.Function();
                    Expr.TypeReference _return;

                    while (function.modifiers.ContainsKey(current.lexeme))
                    {
                        function.modifiers[current.lexeme] = true;
                        advance();
                    }

                    if (peek().type == "IDENTIFIER")
                    {
                        Expect("IDENTIFIER", "function return type");
                        _return = new(new());
                        _return.typeName.Enqueue(previous());

                        if (TypeMatch("DOT"))
                        {
                            _return = GetTypeGetter();
                        }
                    }
                    else
                    {
                        _return = new(new());
                        _return.typeName.Enqueue(new Token("void", "void"));
                    }

                    Expect("IDENTIFIER", definitionType.type + " name");
                    Token name = previous();
                    Expect("LPAREN", "'(' after function name");
                    List<Expr.Parameter> parameters = new();
                    while (!TypeMatch("RPAREN"))
                    {
                        Expect("IDENTIFIER", "identifier as function parameter type");
                        Expr.TypeReference type = GetTypeGetter();

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
                            throw new Errors.ParseError("Unexpected End In Function Parameters", $"Function '{name.lexeme}' reached an unexpected end during it's parameters");
                        }
                    }
                    function.Add(_return, name, parameters, GetBlock(definitionType.type));
                    return function;
                }
                else if (definitionType.type == "class")
                {
                    Expect("IDENTIFIER", definitionType.type + " name");
                    Token name = previous();

                    if (Literals.Contains(name.lexeme))
                    {
                        throw new Errors.ParseError("Invalid Class", $"The name of a class may not be a literal ({name.lexeme})");
                    }

                    return new Expr.Class(name, GetBlock(definitionType.type));
                }
                else if (definitionType.type == "asm")
                {
                    var instructions = GetAsmInstructions();
                    return new Expr.Assembly(instructions.Item1, instructions.Item2);
                }
                else if (definitionType.type == "define")
                {
                    Expect("IDENTIFIER", "name of 'Define'");
                    var name = previous();

                    return new Expr.Define(name, Literal()
                        ?? throw new Errors.ParseError("Invalid Define", "The value of 'Define' should be a literal"));
                }
                else if (definitionType.type == "primitive")
                {
                    Expect("class", "'class' keyword");
                    Expect("IDENTIFIER", "name of primitive type");
                    var name = previous();

                    if (Literals.Contains(name.lexeme))
                    {
                        throw new Errors.ParseError("Invalid Primitive", $"The name of a primitive may not be a literal ({name.lexeme})");
                    }

                    Expect("is", "'is' keyword");

                    List<string> literals = new();
                    if (TypeMatch("LBRACE"))
                    {
                        while (!TypeMatch("RBRACE"))
                        {
                            Expect("IDENTIFIER", "token literal of primitive type");
                            literals.Add(previous().lexeme);
                            if (isAtEnd())
                            {
                                Expect("RBRACE", "'}' after token literals of primitive type");
                            }
                        }
                    }
                    else
                    {
                        Expect("IDENTIFIER", "token literal of primitive type");
                        literals.Add(previous().lexeme);
                    }

                    if (!literals.All(Literals.Contains))
                    {
                        throw new Errors.ParseError("Invalid Primitive", $"The literal of a primitive must be a valid literal ({string.Join(", ", Literals)})");
                    }

                    ExpectValue("IDENTIFIER", "sizeof", "'sizeof' keyword");
                    Expect("INTEGER", "size (in bytes) of primitive");
                    Token size = previous();

                    if (!new List<string>() { "8", "4", "2", "1" }.Contains(size.lexeme))
                    {
                        throw new Errors.ParseError("Invalid Primitive", "The size of primitive classes must be the integers '8', '4', '2', or '1'");
                    }

                    var block = GetBlock(definitionType.type);
                    return new Expr.Primitive(name, literals, int.Parse(size.lexeme), block);
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

                switch (conditionalType.type)
                {
                    case "if":
                    {
                        Expr condition = GetCondition();
                        block = GetBlock(conditionalType.type);
                        var expr = new Expr.If(condition, block);
                        while (TypeMatch("else"))
                        {
                            if (TypeMatch("if"))
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
                        if (current.type == "if")
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
                        block = GetBlock(conditionalType.type);
                        return new Expr.While(condition, block);
                    }
                    case "for":
                    {
                        Expect("LPAREN", "'(' after 'for'");
                        var initExpr = Logical();
                        Expect("SEMICOLON", "';' after expression");
                        var condition = Logical();
                        Expect("SEMICOLON", "';' after expression");
                        var updateExpr = Logical();
                        Expect("RPAREN", "')' after update of For Statement");
                        block = GetBlock(conditionalType.type);
                        return new Expr.For(condition, block, initExpr, updateExpr);
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
            Expr expr = Return();
            return expr;
        }

        private Expr Return()
        {
            Expr expr;

            if (TypeMatch("return"))
            {
                if (current.type == "SEMICOLON")
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
            return Is();
        }

        private Expr Is()
        {
            Expr expr = Primary();
            while (!isAtEnd() && TypeMatch("is"))
            {
                Expect("IDENTIFIER", "type after 'is' operator");
                expr = new Expr.Is(expr, GetTypeGetter());
            }
            return expr;
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

                if (TypeMatch("LPAREN"))
                {
                    Expr expr = Logical();
                    Expect("RPAREN", "')' after expression.");
                    return new Expr.Grouping(expr);
                }

                if (TypeMatch("IDENTIFIER", "this"))
                {
                    Expr expr = null;
                    
                    var variable = GetGetter();

                    if (TypeMatch("IDENTIFIER", "this"))
                    {
                        if (variable.Item2)
                        {
                            throw new Errors.ParseError("Invalid Assign Statement", "Cannot assign to a non-variable");
                        }

                        var name = previous();
                        if (name.type == "this")
                        {
                            throw new Errors.ParseError("Invalid 'This' Keyword", "The 'this' keyword may only be used in a member to reference the enclosing class");
                        }
                        Expect("EQUALS", "'=' when declaring variable");
                        Expr value = NoSemicolon();

                        expr = new Expr.Declare((Expr.TypeReference)variable.Item1, name, value);
                    }
                    else if (TypeMatch("EQUALS"))
                    {
                        if (variable.Item2)
                        {
                            throw new Errors.ParseError("Invalid Assign Statement", "Cannot assign to a non-variable");
                        }
                        expr = new Expr.Assign((Expr.Variable)variable.Item1, NoSemicolon());
                    }
                    else if (TypeMatch(new string[] { "PLUS", "MINUS", "MULTIPLY", "DIVIDE", "MODULO" }, new string[] { "EQUALS" }))
                    {
                        if (variable.Item2)
                        {
                            throw new Errors.ParseError("Invalid Assign Statement", "Cannot assign to a non-variable");
                        }
                        var sign = tokens[index - 2];
                        expr = new Expr.Assign((Expr.Variable)variable.Item1, sign, NoSemicolon());
                    }
                    else if (TypeMatch("PLUSPLUS", "MINUSMINUS"))
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

                if (TypeMatch("null", "true", "false"))
                {
                    return new Expr.Keyword(previous().lexeme);
                }

                if (TypeMatch("new"))
                {
                    Expect("IDENTIFIER", "identifier after new expression");
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

        private (Expr.GetReference, bool) GetGetter()
        {
            Queue<Token> typeName = new Queue<Token>();
            typeName.Enqueue(previous());

            if (peek().type != "DOT" && TypeMatch("LPAREN"))
            {
                return (new Expr.Call(typeName.Dequeue(), null, null, GetArgs()), true);
            }

            while (TypeMatch("DOT"))
            {
                Expect("IDENTIFIER", "variable name after '.'");
                var variable = previous();

                if (TypeMatch("LPAREN"))
                {
                    var args = GetArgs();
                    return (new Expr.Call(variable, typeName, (peek().type != "DOT")? null : GetGetter().Item1, args), true);
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

            if (peek().type != "DOT" && TypeMatch("LPAREN"))
            {
                return new Expr.Call(typeName.Dequeue(), null, null, GetArgs());
            }

            while (TypeMatch("DOT"))
            {
                Expect("IDENTIFIER", "variable name after '.'");

                if (TypeMatch("LPAREN"))
                {
                    return new Expr.Call(previous(2), typeName, null, GetArgs());
                }
                else
                {
                    typeName.Enqueue(previous());
                }
            }

            throw Expected("LPAREN", "'(' after type in new expression");
        }

        private Expr.TypeReference GetTypeGetter()
        {
            Queue<Token> typeName = new Queue<Token>();
            typeName.Enqueue(previous());

            while (TypeMatch("DOT"))
            {
                Expect("IDENTIFIER", "variable name after '.'");
                typeName.Enqueue(previous());
            }

            return new Expr.TypeReference(typeName);
        }


        private Exception End()
        {
            return new Errors.ParseError("Expression Reached Unexpected End", $"Expression '{((previous() != null)? previous().lexeme : "")}' reached an unexpected end");
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
            Expect("RPAREN", "')' after conditional");
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
        
        private (List<Instruction>, Dictionary<Expr.Variable, Instruction.Pointer>) GetAsmInstructions()
        {
            List<Instruction> instructions = new();
            Dictionary<Expr.Variable, Instruction.Pointer> variables = new();

            Expect("LBRACE", "'{' before Assembly Block body");
            while (!TypeMatch("RBRACE"))
            {
                if (TypeMatch("IDENTIFIER"))
                {
                    var op = previous();

                    if (TypeMatch("IDENTIFIER", "DOLLAR"))
                    {
                        // Unary
                        Instruction.Value value;

                        if (previous().type == "DOLLAR")
                        {
                            Expect("IDENTIFIER", "after escape '$'");
                            var ptr = new Instruction.Pointer(0, 0);
                            Queue<Token> queue = new();
                            queue.Enqueue(previous());
                            variables[new Expr.Variable(queue)] = ptr;
                            value = ptr;
                        }
                        else
                        {
                            var identifier = previous();
                            if (InstructionInfo.Registers.TryGetValue(identifier.lexeme, out var reg))
                            {
                                value = new Instruction.Register(reg.Item1, reg.Item2);
                            }
                            else
                            {
                                throw new Errors.ParseError("Invalid Assembly Register", $"Invalid assembly register given '{identifier}'");
                            }
                        }

                        if (TypeMatch("COMMA"))
                        {
                            // Binary

                            if (TypeMatch("IDENTIFIER"))
                            {
                                var identifier = previous();
                                if (InstructionInfo.Registers.TryGetValue(identifier.lexeme, out var reg))
                                {
                                    instructions.Add(new Instruction.Binary(op.lexeme, value, new Instruction.Register(reg.Item1, reg.Item2)));
                                }
                                else
                                {
                                    throw new Errors.ParseError("Invalid Assembly Register", $"Invalid assembly register given '{identifier.lexeme}'");
                                }
                            }
                            else if (TypeMatch("INTEGER", "FLOATING", "STRING", "HEX", "BINARY"))
                            {
                                instructions.Add(new Instruction.Binary(op.lexeme, value, new Instruction.Literal(previous().lexeme, previous().type)));
                            }
                            else if (TypeMatch("DOLLAR"))
                            {
                                Expect("IDENTIFIER", "after escape '$'");
                                var ptr = new Instruction.Pointer(0, 0);
                                Queue<Token> queue = new();
                                queue.Enqueue(previous());
                                variables[new Expr.Variable(queue)] = ptr;
                                instructions.Add(new Instruction.Binary(op.lexeme, value, ptr));
                            }
                            else
                            {
                                Expected("IDENTIFIER, INTEGER, FLOAT, STRING, HEX, BINARY", "operand after comma ','");
                            }
                        }
                        else
                        {
                            instructions.Add(new Instruction.Unary(op.lexeme, value));
                        }
                    }
                    else
                    {
                        // Zero
                        instructions.Add(new Instruction.Zero(op.lexeme));
                    }
                    Expect("SEMICOLON", "';' after Assembly statement");
                }
                else
                {
                    throw new Errors.ParseError("Invalid Assembly Statement", $"'{current.lexeme}' is invalid in an assembly block");
                }

                if (isAtEnd())
                {
                    Expect("RBRACE", "'}' after Assembly Block");
                }
                if (TypeMatch("RBRACE"))
                {
                    break;
                }
            }
            return (instructions, variables);
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
            throw Expected(type, errorMessage);
        }

        private void ExpectValue(string type, string value, string errorMessage)
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
