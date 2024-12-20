﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Parser
{
    List<Token> tokens;
    List<Expr> expressions;
    Token? current;
    int index;

    List<Expr.Import> imports = [];

    internal const LiteralTokenType VoidTokenType = (LiteralTokenType)(-1);
    internal enum LiteralTokenType
    {
        Integer = Token.TokenType.INTEGER,
        UnsignedInteger = Token.TokenType.UNSIGNED_INTEGER,
        Floating = Token.TokenType.FLOATING,
        String = Token.TokenType.STRING,
        RefString = Token.TokenType.REF_STRING,
        Boolean = Token.TokenType.BOOLEAN
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

    public static void Parse(List<Token> tokens)
    {
        SymbolTableSingleton.SymbolTable.AddMainImport(tokens);
    }

    internal (List<Expr> exprs, List<Expr.Import> imports) ParseImport()
    {
        while (!IsAtEnd())
        {
            expressions.Add(Start());
        }

        var runtimeAutoImports = Expr.Import.GenerateAutoImports()
            .Where(x => x.fileInfo != SymbolTableSingleton.SymbolTable.currentFileInfo);
        imports.AddRange(runtimeAutoImports);
        expressions.AddRange(runtimeAutoImports);

        return (expressions, imports);
    }

    private Expr Start() => Import();

    private Expr Import()
    {
        if (ReservedValueMatch("import"))
        {
            int idx = index;

            while (TypeMatch(Token.TokenType.IDENTIFIER, Token.TokenType.DOT, Token.TokenType.DIVIDE, Token.TokenType.MULTIPLY))
            {
                if (IsAtEnd())
                {
                    End();
                    return new Expr.InvalidExpr();
                }
            }
            bool fromStmt = current.lexeme == "from";

            index = idx-1;
            Advance();

            Expr.TypeReference? importRef = new(null);
            string fileName = "";
            bool importAll = false;

            if (fromStmt)
            {
                importRef.typeName = new();
                if (TypeMatch(Token.TokenType.MULTIPLY))
                {
                    importAll = true;
                }
                else if (Expect(Token.TokenType.IDENTIFIER, "type reference name"))
                {
                    importRef.typeName.Enqueue(Previous());

                    while (TypeMatch(Token.TokenType.DOT) && current.type != Token.TokenType.MULTIPLY)
                    {
                        Expect(Token.TokenType.IDENTIFIER, "variable name after '.'");
                        importRef.typeName.Enqueue(Previous());
                    }
                    importAll = TypeMatch(Token.TokenType.MULTIPLY);
                }
                Advance();

            }

            bool customPath = false;
            while (TypeMatch(Token.TokenType.IDENTIFIER, Token.TokenType.DOT, Token.TokenType.DIVIDE))
            {
                if (Previous().type == Token.TokenType.DIVIDE) customPath = true;
                fileName += Previous().lexeme;
            }

            if (!fileName.EndsWith(".rz"))
            {
                fileName += ".rz";
            }

            Expect(Token.TokenType.SEMICOLON, "';' after expression");

            var import = new Expr.Import(new Expr.Import.FileInfo(fileName), customPath, new Expr.Import.ImportType(importRef, importAll));
            imports.Add(import);
            return import;
        }
        Expr expr = Definition();
        SymbolTableSingleton.SymbolTable.AddGlobal(expr as Expr.Definition);
        return expr;
    }

    private Expr Definition()
    {
        if (ReservedValueMatch("function", "class", "primitive", "trait"))
        {
            Token definitionType = Previous();
            if (definitionType.lexeme == "function")
            {
                ExprUtils.Modifiers modifiers = ExprUtils.Modifiers.FunctionModifierTemplate();

                while (modifiers.ContainsModifier(current?.lexeme))
                {
                    modifiers[current.lexeme] = true;
                    Advance();
                }
                if (IsAtEnd()) return new Expr.InvalidExpr();

                Expr.TypeReference _return = new(null);

                bool refReturn = ReservedValueMatch("ref");

                Expect(Token.TokenType.IDENTIFIER, definitionType.lexeme + " name");

                if (IsAtEnd()) return new Expr.InvalidExpr();

                if (current.type == Token.TokenType.DOT || current.type == Token.TokenType.IDENTIFIER)
                {
                    _return.typeName = GetTypeReference();

                    if (!Expect(Token.TokenType.IDENTIFIER, definitionType.lexeme + " name"))
                        return new Expr.InvalidExpr();
                }

                Token name = Previous();
                Expect(Token.TokenType.LPAREN, "'(' after function name");

                List<Expr.Parameter> parameters = new();

                while (true)
                {
                    if (IsAtEnd())
                    {
                        Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.UnexpectedEndInFunctionParameters, name.location, name.lexeme));
                        return new Expr.Function(modifiers, refReturn, _return, name, parameters, new(new()), null);
                    }

                    if (TypeMatch(SynchronizationTokens) || TypeMatch(Token.TokenType.LBRACE))
                    {
                        MovePrevious();
                        break;
                    }

                    ExprUtils.Modifiers paramModifers = ExprUtils.Modifiers.ParameterModifierTemplate();

                    Advance();
                    (paramModifers["ref"], paramModifers["readonly"]) = ParseRefReadonlyModifiers();

                    if (Previous().type == Token.TokenType.IDENTIFIER)
                    {
                        var typeName = GetTypeReference();
                        if (Expect(Token.TokenType.IDENTIFIER, "identifier as function parameter"))
                            parameters.Add(new Expr.Parameter(typeName, Previous(), paramModifers));
                    }
                    else
                    {
                        PanicUntil(SynchronizationTokens.Append(Token.TokenType.LBRACE).ToArray(), Token.TokenType.IDENTIFIER, "identifier as function parameter name");
                    }

                    if (TypeMatch(Token.TokenType.IDENTIFIER))
                    {
                        Expected(Token.TokenType.COMMA.ToString(), "',' between parameters");
                    }
                    else if (!TypeMatch(Token.TokenType.COMMA))
                    {
                        break;
                    }
                }

                Expect(Token.TokenType.RPAREN, "')' after function name");

                string? externFileName = null;
                if (ReservedValueMatch("from"))
                {
                    Expect(Token.TokenType.REF_STRING, "extern file name after 'from'");
                    externFileName = Previous().lexeme;
                }

                if (TypeMatch(Token.TokenType.SEMICOLON))
                {
                    modifiers["virtual"] = externFileName == null;
                    return new Expr.Function(modifiers, refReturn, _return, name, parameters, null, externFileName);
                }
                return new Expr.Function(modifiers, refReturn, _return, name, parameters, GetBlock(definitionType.lexeme), externFileName);
            }
            else if (definitionType.lexeme == "class" || definitionType.lexeme == "trait")
            {
                bool trait = definitionType.lexeme == "trait";

                Expect(Token.TokenType.IDENTIFIER, definitionType.lexeme + " name");
                Token name = Previous();

                if (Enum.GetNames<LiteralTokenType>().Contains(name.type.ToString()))
                {
                    Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.InvalidClassName, name.location, name.lexeme));
                }

                Expr.TypeReference superclass = new(null);
                if (ReservedValueMatch("extends"))
                {
                    Expect(Token.TokenType.IDENTIFIER, "superclass of type");
                    superclass = new(GetTypeReference());
                }

                List<Expr.Declare> declarations = new();
                List<Expr.Definition> definitions = new();

                Expect(Token.TokenType.LBRACE, "'{' before" + definitionType.lexeme + "body");
                while (!TypeMatch(Token.TokenType.RBRACE))
                {
                    Expr.Declare? declExpr = FullDeclare();
                    if (declExpr != null)
                    {
                        declarations.Add(declExpr);
                    }
                    else if (!IsAtEnd())
                    {
                        if (Definition() is Expr.Definition definitionExpr)
                        {
                            definitions.Add(definitionExpr);
                        }
                        else
                        {
                            Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.InvalidClassDefinition, current?.location ?? Location.NoLocation, current?.lexeme));
                            Advance();
                        }
                    }
                    else
                    {
                        Expect(Token.TokenType.RBRACE, "'}' after" + definitionType.lexeme + "body");
                        break;
                    }
                }

                return new Expr.Class(name, declarations, definitions, superclass, trait);
            }
            else if (definitionType.lexeme == "primitive")
            {
                ExpectValue(Token.TokenType.RESERVED, "class", "'class' keyword");
                Expect(Token.TokenType.IDENTIFIER, definitionType.lexeme + " name");
                var name = Previous();

                if (Enum.GetNames<LiteralTokenType>().Contains(name.type.ToString()))
                {
                    Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.InvalidPrimitiveName, name.location, name.lexeme));
                }

                ExpectValue(Token.TokenType.IDENTIFIER, "sizeof", "'sizeof' keyword");

                int size = -1;

                if (TypeMatch(Token.TokenType.INTEGER))
                {
                    if (!new List<string>() { "8", "4", "2", "1" }.Contains(Previous().lexeme))
                    {
                        Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.InvalidPrimitiveSize, Previous().location, Previous().lexeme));
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

                string? superclassName = null;

                if (ReservedValueMatch("extends"))
                {
                    Expect(Token.TokenType.IDENTIFIER, "superclass of primitive type");

                    superclassName = Previous().lexeme;

                    if (!Enum.GetNames<LiteralTokenType>().Contains(superclassName))
                    {
                        Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.InvalidPrimitiveSuperclass, Previous().location, Previous().lexeme));
                        superclassName = null;
                    }
                }

                List<Expr.Definition> definitions = new();
                Expect(Token.TokenType.LBRACE, "'{' before primitive class body");
                while (!TypeMatch(Token.TokenType.RBRACE))
                {
                    Expr.Definition? bodyExpr = Definition() as Expr.Definition;
                    if (bodyExpr != null)
                    {
                        definitions.Add(bodyExpr);
                    }
                    else if (!IsAtEnd())
                    {
                        Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.InvalidClassDefinition, current?.location ?? Location.NoLocation, current?.lexeme));
                        Advance();
                    }
                    else
                    {
                        Expect(Token.TokenType.RBRACE, "'}' after primitive body");
                        break;
                    }
                }

                return new Expr.Primitive(name, definitions, size, superclassName);
            }
        }
        return Entity(true);
    }

    private Expr Entity(bool topLevel)
    {
        if (ReservedValueMatch("asm"))
        {
            return new AssemblyParser(this).ParseInlineAssemblyBlock();
        }
        return topLevel ? InvalidTopLevelCode() : Conditional();
    }

    private Expr InvalidTopLevelCode()
    {
        Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.TopLevelCode));
        if (TypeMatch(Token.TokenType.LBRACE))
        {
            MovePrevious();
            return GetBlock("disconnected block");
        }
        return Conditional();
    }

    private Expr NonTopLevelStart() => Entity(false);

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
                            Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.NoMatchingIf, conditionalType.location, "else if"));
                            return new Expr.If(new(GetCondition(), GetBlock("else if")));
                        }
                        else
                        {
                            Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.NoMatchingIf, conditionalType.location, "else"));
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
                expr = new Expr.Return(new Expr.Keyword("void"));
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
        Expr expr = IsOrAs();
        while (TypeMatch(Token.TokenType.PLUSPLUS, Token.TokenType.MINUSMINUS))
        {
            Token op = Previous();
            expr = new Expr.Unary(op, expr);
        }
        return expr;
    }

    private Expr IsOrAs()
    {
        Expr expr = Primary();
        while (ReservedValueMatch("is", "as"))
        {
            string value = Previous().lexeme;
            Expect(Token.TokenType.IDENTIFIER, $"type after '{value}' operator");
            expr = (value == "is") ?
                new Expr.Is(expr, new Expr.TypeReference(GetTypeReference())) :
                new Expr.As(expr, new Expr.TypeReference(GetTypeReference()));

        }
        return expr;
    }

    private Expr Primary()
    {
        if (!IsAtEnd())
        {
            Expr? getter = null;

            if (TypeMatch(Array.ConvertAll<LiteralTokenType, Token.TokenType>(Enum.GetValues<LiteralTokenType>(), new(x => (Token.TokenType)x))))
            {
                getter = new Expr.Literal(new LiteralToken((LiteralTokenType)Previous().type, Previous().lexeme, Previous().location));
            }
            else if (TypeMatch(Token.TokenType.LPAREN))
            {
                Expr logical = Logical();
                Expect(Token.TokenType.RPAREN, "')' after expression.");
                getter = new Expr.Grouping(logical);
            }
            else if (ReservedValueMatch("new"))
            {
                Expect(Token.TokenType.IDENTIFIER, "identifier after new expression");
                var newCallExpr = GetNewGetter();
                if (newCallExpr == null)
                {
                    return new Expr.InvalidExpr();
                }
                getter = new Expr.New(newCallExpr);
            }
            else if (ReservedValueMatch("heapalloc"))
            {
                Expect(Token.TokenType.LPAREN, "before heapalloc size expression");
                var heapAlloc = new Expr.HeapAlloc(Logical());
                Expect(Token.TokenType.RPAREN, "after heapalloc size expression");
                getter = heapAlloc;
            }

            if ((getter != null && current.type == Token.TokenType.DOT) || (getter == null && (TypeMatch(Token.TokenType.IDENTIFIER) || ReservedValueMatch("this", "ref", "readonly"))))
            {
                (bool _ref, bool _readonly) = ParseRefReadonlyModifiers();

                var variable = GetReference((getter is Expr.Getter || getter == null) ? (Expr.Getter)getter : new Expr.Grouping(getter), _ref);

                if (TypeMatch(Token.TokenType.EQUALS))
                {
                    if (variable.IsMethodCall())
                    {
                        Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.InvalidAssignStatement, Previous().location, "method"));
                    }
                    if (_readonly)
                    {
                        Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.InvalidReadonlyModifier, Previous().location));
                    }

                    return new Expr.Assign(variable, NoSemicolon());
                }
                else if (TypeMatch([Token.TokenType.PLUS, Token.TokenType.MINUS, Token.TokenType.MULTIPLY, Token.TokenType.DIVIDE, Token.TokenType.MODULO], [Token.TokenType.EQUALS]))
                {
                    if (variable.IsMethodCall())
                    {
                        Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.InvalidAssignStatement, Previous().location, "method"));
                    }
                    if (_readonly)
                    {
                        Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.InvalidReadonlyModifier, Previous().location));
                    }

                    var op = tokens[index - 2];
                    return new Expr.Assign(variable, new Expr.Binary(variable, op, NoSemicolon()));
                }
                else if (TypeMatch(Token.TokenType.LBRACKET))
                {
                    if (_readonly)
                    {
                        Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.InvalidReadonlyModifier, Previous().location));
                    }

                    var binary = new Expr.Binary(variable, Previous(), NoSemicolon());
                    Expect(Token.TokenType.RBRACKET, "']' after indexer");
                    return binary;
                }
                else if (!variable.IsMethodCall() && (TypeMatch(Token.TokenType.IDENTIFIER) || ReservedValueMatch("this")))
                {
                    return (Expr)Declare(variable, _readonly) ?? new Expr.InvalidExpr();
                }
                else
                {
                    if (_readonly)
                    {
                        Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.InvalidReadonlyModifier, Previous().location));
                    }

                    return variable;
                }
            }

            if (getter != null)
            {
                return getter;
            }

            if (ReservedValueMatch("null", "true", "false"))
            {
                return new Expr.Keyword(Previous().lexeme);
            }
        }
        End();
        Advance();
        return new Expr.InvalidExpr();
    }

    private Expr.Declare? FullDeclare()
    {
        if (TypeMatch(Token.TokenType.IDENTIFIER) || ReservedValueMatch("ref", "readonly"))
        {
            Expr.Declare? declare;

            (bool _ref, bool _readonly) = ParseRefReadonlyModifiers();
            
            var variable = GetReference(null, _ref);

            if (TypeMatch(Token.TokenType.IDENTIFIER))
            {
                declare = Declare(variable, _readonly);
            }
            else
            {
                Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.InvalidClassDefinition, Previous().location, Previous().lexeme));
                return null;
            }
            Expect(Token.TokenType.SEMICOLON, "';' after expression");

            if (declare != null && declare.stack._ref)
            {
                Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.InvalidRefModifier_Location, "class definition"));
            }

            return declare;
        }
        return null;
    }

    private Expr.Declare? Declare(Expr.GetReference variable, bool _readonly)
    {
        if (variable is not Expr.AmbiguousGetReference getRef)
        {
            Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.InvalidDeclareStatement, variable.GetLastName().location, []));
            return null;
        }

        var name = Previous();
        if (name.lexeme == "this")
        {
            Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.InvalidThisKeyword, name.location, []));
            name.type = Token.TokenType.IDENTIFIER;
        }

        if (TypeMatch(Token.TokenType.SEMICOLON))
        {
            MovePrevious();
            return new Expr.Declare(getRef.typeName, name, getRef._ref, _readonly, null);
        }
        Expect(Token.TokenType.EQUALS, "'=' when declaring variable");

        return new Expr.Declare(getRef.typeName, name, getRef._ref, _readonly, NoSemicolon());
    }

    private Expr.GetReference GetReference(Expr.Getter? expr, bool _ref)
    {
        List<Expr.Getter> getters = new();

        if (expr == null)
        {
            Expr.AmbiguousGetReference callee = new(GetTypeReference(), _ref);

            if (TypeMatch(Token.TokenType.LPAREN))
            {
                getters.Add(new Expr.Call(callee.typeName.StackPop(), GetArgs(), (callee.typeName.Count == 0) ? null : callee));
            }
            else if (callee.typeName.Count != 0 && TypeMatch(Token.TokenType.LBRACKET))
            {
                getters.Add(new Expr.Binary(callee, Previous(), Logical()));
                Expect(Token.TokenType.RBRACKET, "']' after indexer");
            }
            else
            {
                callee.instanceCall = true;
                return callee;
            }
        }
        else
        {
            getters.Add(expr);
        }

        while (true)
        {
            if (TypeMatch(Token.TokenType.DOT))
            {
                Expect(Token.TokenType.IDENTIFIER, "variable name after '.'");
                if (TypeMatch(Token.TokenType.LPAREN))
                {
                    var call = new Expr.Call(Previous(2), GetArgs(), new Expr.InstanceGetReference(getters, _ref));
                    getters = [call];
                }
                else
                {
                    getters.Add(new Expr.Get(Previous()));
                }
            }
            else if (TypeMatch(Token.TokenType.LBRACKET))
            {
                var call = new Expr.Binary(new Expr.InstanceGetReference(getters, _ref), Previous(), Logical());
                getters = [call];
                Expect(Token.TokenType.RBRACKET, "']' after indexer");
            }
            else break;
        }

        return new Expr.InstanceGetReference(getters, _ref);
    }

    private Expr.Call? GetNewGetter()
    {
        Expr.AmbiguousGetReference callee = new(GetTypeReference(), false);
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
        Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.ExpressionReachedUnexpectedEnd, current?.location ?? Location.NoLocation, (current == null)? Previous().lexeme : current.lexeme));
    }

    private (bool _ref, bool _readonly) ParseRefReadonlyModifiers()
    {
        bool _readonly, _ref;
        var lastTwoLexemes = new List<string> { Previous().lexeme, current.lexeme };

        if (_ref = lastTwoLexemes.Contains("ref"))
            Advance();
        if (_readonly = lastTwoLexemes.Contains("readonly"))
            Advance();

        return (_ref, _readonly);
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
                    Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.UnexpectedTokenInFunctionArguments, Previous().location, "comma ','"));
                    while (TypeMatch(Token.TokenType.COMMA))
                    {
                        Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.UnexpectedTokenInFunctionArguments, Previous().location, "comma ','"));
                    }
                }
                if (TypeMatch(Token.TokenType.RPAREN))
                {
                    Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.UnexpectedTokenInFunctionArguments, Previous().location, "comma ','"));
                    break;
                }
            }
        }
        return arguments;
    }

    private Expr GetCondition()
    {
        Expect(Token.TokenType.LPAREN, "'(' before conditional");
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
            Expect(Token.TokenType.RBRACE, "'}' after " + bodytype + " body");
            return bodyExprs;
        }
        while (!TypeMatch(Token.TokenType.RBRACE))
        {
            if (IsAtEnd())
            {
                Expect(Token.TokenType.RBRACE, "'}' after " + bodytype + " body");
                break;
            }
            bodyExprs.Add(NonTopLevelStart());
        }
        return bodyExprs;
    }

    private void PanicUntilSynchronized(Token.TokenType expectedToken, string errorMessage) => PanicUntil(SynchronizationTokens, expectedToken, errorMessage);
    private void PanicUntil(Token.TokenType[] types, Token.TokenType expectedToken, string errorMessage)
    {
        Location startingLocation = current.location;
        string panicInvalidTokens = current.lexeme;
        Advance();
        while (!(IsAtEnd() || TypeMatch(types)))
        {
            panicInvalidTokens += " " + current.lexeme;
            Advance();
        }
        Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.TokenExpected, startingLocation, expectedToken, errorMessage, panicInvalidTokens));
        MovePrevious();
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
            MovePrevious();
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

        return false;
    }

    private void ExpectValue(Token.TokenType type, string value, string errorMessage)
    {
        if (current != null && current.type == type && current.lexeme == value)
        {
            Advance();
            return;
        }
        Expected($"{{ {type} : {value} }}", errorMessage);
    }

    private void Expected(string type, string errorMessage)
    {
        Diagnostics.Report(new Diagnostic.ParseDiagnostic(Diagnostic.DiagnosticName.TokenExpected, current?.location ?? Location.NoLocation, type, errorMessage, current?.lexeme));
    }

    private Token Previous(int sub = 1)
    {
        return tokens[index - sub];
    }

    private void Advance()
    {
        index++;
        if (!(index >= tokens.Count || index < 0))
        {
            current = tokens[index];
            return;
        }
        current = null;
    }
    private void MovePrevious() => current = tokens[--index];

    private bool IsAtEnd() => current == null;
}
