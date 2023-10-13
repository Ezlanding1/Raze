﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

internal partial class Analyzer
{
    internal static class TypeCheckUtils
    {
        public static Expr.Class anyType = new Analyzer.SpecialObjects.Any(new(Token.TokenType.IDENTIFIER, "any"));

        public static Expr.Type _voidType = new Expr.Class(new(Token.TokenType.RESERVED, "void"), new(), new(), new(null));

        public static Dictionary<Token.TokenType, Expr.Type> literalTypes = new Dictionary<Token.TokenType, Expr.Type>()
        {
            { Parser.Literals[0], new(new Token(Parser.Literals[0])) },
            { Parser.Literals[1], new(new Token(Parser.Literals[1])) },
            { Parser.Literals[2], new(new Token(Parser.Literals[2])) },
            { Parser.Literals[3], new(new Token(Parser.Literals[3])) },
            { Parser.Literals[4], new(new Token(Parser.Literals[4])) },
            { Parser.Literals[5], new(new Token(Parser.Literals[5])) },
        };

        public static Dictionary<string, Expr.Type> keywordTypes = new Dictionary<string, Expr.Type>()
        {
            { "true", literalTypes[Token.TokenType.BOOLEAN] },
            { "false", literalTypes[Token.TokenType.BOOLEAN] },
            { "null", null },
        };

        public static void MustMatchType(Expr.Type type1, Expr.Type type2, string error= "You cannot assign type '{0}' to type '{1}'")
        {
            if (!type2.Matches(type1))
            {
                Diagnostics.errors.Push(new Error.AnalyzerError("Type Mismatch", string.Format(error, type2, type1)));
            }
        }

        public static bool CannotBeRef(Expr expr) => expr is not Expr.GetReference || (expr is Expr.GetReference getRef && getRef.IsMethod());

        public static void TypeCheckConditional(Expr.IVisitor<Expr.Type> visitor, List<(Expr.Type?, bool, Expr.Return?)> _return, Expr? condition, Expr.Block block)
        {
            int _returnCount = _return.Count;
            if (condition != null)
            {
                if (!literalTypes[Token.TokenType.BOOLEAN].Matches(condition.Accept(visitor)))
                {
                    Diagnostics.errors.Push(new Error.AnalyzerError("Type Mismatch", $"'if' expects condition to return 'BOOLEAN'. Got '{condition.Accept(visitor)}'"));
                }
            }
            block.Accept(visitor);

            if (condition != null)
            {
                for (int i = _returnCount; i < _return.Count; i++)
                {
                    _return[i] = (_return[i].Item1, true, _return[i].Item3);
                }
            }
        }

        public static void HandleFunctionReturns(Expr.Function expr, List<(Expr.Type?, bool, Expr.Return?)> _return)
        {
            int _returnCount = 0;
            foreach (var ret in _return)
            {
                if (!ret.Item2)
                {
                    _returnCount++;
                }

                if (ret.Item3 == null)
                    continue;

                MustMatchType(expr._returnType.type, ret.Item1, "You cannot return type '{0}' from type '{1}'");

                ret.Item3.size = expr._returnSize;
            }
            if (_returnCount == 0 && !Primitives.IsVoidType(expr._returnType.type))
            {
                if (_return.Count == 0)
                {
                    Diagnostics.errors.Push(new Error.AnalyzerError("No Return", "A Function must have a 'return' expression"));
                }
                else
                {
                    Diagnostics.errors.Push(new Error.AnalyzerError("No Return", "A Function must have a 'return' expression from all code paths"));
                }
            }
            _return.Clear();
        }

        public static void ValidateCall(Expr.Call expr, Expr.Function callee)
        {
            if (!expr.constructor && callee.constructor)
            {
                Diagnostics.errors.Push(new Error.AnalyzerError("Constructor Called As Method", "A Constructor may not be called as a method of its class"));
            }
            else if (expr.constructor && !callee.constructor)
            {
                Diagnostics.errors.Push(new Error.AnalyzerError("Method Called As Constructor", "A Method may not be called as a constructor of its class"));
            }

            if (expr.callee != null)
            {
                if (expr.instanceCall && callee.modifiers["static"])
                {
                    Diagnostics.errors.Push(new Error.AnalyzerError("Static Method Called From Instance", "You cannot call a static method from an instance"));
                }
                if (!expr.instanceCall && !callee.modifiers["static"] && !expr.constructor)
                {
                    Diagnostics.errors.Push(new Error.AnalyzerError("Instance Method Called From Static Context", "You cannot call an instance method from a static context"));
                }
            }
        }
    }
}