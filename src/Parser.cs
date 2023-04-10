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
                    Expr.Get _return;

                    while (function.modifiers.ContainsKey(current.lexeme))
                    {
                        function.modifiers[current.lexeme] = true;
                        advance();
                    }

                    if (peek().type == "IDENTIFIER")
                    {
                        Expect("IDENTIFIER", "function return type");
                        _return = new(previous());

                        if (TypeMatch("DOT"))
                        {
                            _return = GetGetter(_return);
                        }
                    }
                    else
                    {
                        _return = new(new Token("void", "void"));
                    }

                    Expect("IDENTIFIER", definitionType.type + " name");
                    Token name = previous();
                    Expect("LPAREN", "'(' after function name");
                    List<Expr.Parameter> parameters = new();
                    while (!TypeMatch("RPAREN"))
                    {
                        Expect("IDENTIFIER", "identifier as function parameter type");
                        Expr.Get type = new(previous());

                        if (TypeMatch("DOT"))
                        {
                            type = GetGetter(type);
                        }

                        Expect("IDENTIFIER", "identifier as function parameter");
                        Token variable = previous();

                        parameters.Add(new Expr.Parameter(new Expr.Type(type), variable));
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
                    function.Add(new Expr.Type(_return), name, parameters, GetBlock(definitionType.type));
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
                    expr = new Expr.Return(Logical());
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
                var variable = new Expr.Get(previous());
                 expr = new Expr.Is(expr, TypeMatch("DOT")? 
                     new Expr.Type(GetGetter(variable))
                     : new Expr.Type(variable)
                     );
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
                    Expr expr;
                    Expr.Get variable = new Expr.Get(previous());

                    if (TypeMatch("DOT"))
                    {
                        variable = GetGetter(variable);
                    }
                    if (TypeMatch("EQUALS"))
                    {
                        expr = new Expr.Assign(new Expr.Member(variable), NoSemicolon());
                    }
                    else if (TypeMatch(new string[] { "PLUS", "MINUS", "MULTIPLY", "DIVIDE", "MODULO" }, new string[] { "EQUALS" }))
                    {
                        var sign = tokens[index-2];
                        expr = new Expr.Assign(new Expr.Member(variable), sign, NoSemicolon());
                    }
                    else if (TypeMatch("PLUSPLUS", "MINUSMINUS"))
                    {
                        if (variable.name.type == "this")
                        {
                            throw new Errors.AnalyzerError("Invalid 'This' Keyword", "The 'this' keyword may only be used in a member to reference the enclosing class");
                        }
                        expr = new Expr.Unary(previous(), variable);
                    }
                    else if (TypeMatch("IDENTIFIER"))
                    {
                        if (variable.name.type == "this")
                        {
                            throw new Errors.AnalyzerError("Invalid 'This' Keyword", "The 'this' keyword may only be used in a member to reference the enclosing class");
                        }
                        var name = previous();
                        Expect("EQUALS", "'=' when declaring variable");
                        Expr value = NoSemicolon();
                        expr = new Expr.Declare(new Expr.Type(variable), name, value);
                    }
                    else if (TypeMatch("LPAREN"))
                    {
                        if (variable.name.type == "this")
                        {
                            throw new Errors.AnalyzerError("Invalid 'This' Keyword", "The 'this' keyword may only be used in a member to reference the enclosing class");
                        }
                        expr = Call(variable);
                    }
                    else
                    {
                        expr = new Expr.Member(variable);
                    }
                    return expr;
                }

                if (TypeMatch("null", "true", "false"))
                {
                    return new Expr.Keyword(previous().lexeme);
                }

                if (TypeMatch("new"))
                {
                    Expect("IDENTIFIER", "class name after 'new' keyword");
                    Expr.Get variable = new Expr.Get(previous());

                    if (TypeMatch("DOT"))
                    {
                        variable = GetGetter(variable);
                    }
                    Expect("LPAREN", "'(' starting constructor parameters");

                    return new Expr.New(new Expr.Call(variable, GetArgs()));
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

        private Expr.Get GetGetter(Expr.Get getter)
        {
            Expect("IDENTIFIER", "variable name after '.'");
            
            Expr.Get get = new Expr.Get(previous());
            if (TypeMatch("DOT"))
            {
                get = GetGetter(get);
            }

            return new Expr.Get(getter, get);
        }

        private Exception End()
        {
            return new Errors.ParseError("Expression Reached Unexpected End", $"Expression '{((previous() != null)? previous().lexeme : "")}' reached an unexpected end");
        }

        private Expr.Call Call(Expr.Get name)
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
                        var operand1 = previous();

                        if (TypeMatch("COMMA"))
                        {
                            // Binary
                            
                            if (TypeMatch("IDENTIFIER"))
                            {
                                instructions.Add(new Instruction.Binary(op.lexeme, new Instruction.Register(InstructionInfo.Registers[operand1.lexeme].Item1, InstructionInfo.Registers[operand1.lexeme].Item2), new Instruction.Register(InstructionInfo.Registers[previous().lexeme].Item1, InstructionInfo.Registers[previous().lexeme].Item2)));
                            }
                            else if (TypeMatch("INTEGER", "FLOATING", "STRING", "HEX", "BINARY"))
                            {
                                instructions.Add(new Instruction.Binary(op.lexeme, new Instruction.Register(InstructionInfo.Registers[operand1.lexeme].Item1, InstructionInfo.Registers[operand1.lexeme].Item2), new Instruction.Literal(previous().lexeme, previous().type)));
                            }
                            else if (TypeMatch("DOLLAR"))
                            {
                                Expect("IDENTIFIER", "after escape '$'");
                                var ptr = new Instruction.Pointer(0, 0);
                                variables[new Expr.Variable(previous())] = ptr;
                                instructions.Add(new Instruction.Binary(op.lexeme, new Instruction.Register(InstructionInfo.Registers[operand1.lexeme].Item1, InstructionInfo.Registers[operand1.lexeme].Item2), ptr));
                            }
                            else
                            {
                                Expected("IDENTIFIER, INTEGER, FLOAT, STRING, HEX, BINARY", "operand after comma ','");
                            }
                        }
                        else
                        {
                            // Unary
                            if (operand1.type == "DOLLAR")
                            {
                                Expect("IDENTIFIER", "after escape '$'");
                                var ptr = new Instruction.Pointer(0, 0);
                                variables[new Expr.Variable(previous())] = ptr;
                                instructions.Add(new Instruction.Unary(op.lexeme, ptr));
                            }
                            else
                            {
                                instructions.Add(new Instruction.Unary(op.lexeme, new Instruction.Register(InstructionInfo.Registers[operand1.lexeme].Item1, InstructionInfo.Registers[operand1.lexeme].Item2)));
                            }
                            
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

        private Token? previous()
        {
            if (!isAtEnd(index - 1))
            {
                return tokens[index - 1];
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
