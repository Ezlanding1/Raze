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
            expressions.Add(Start());
            return expressions;
        }

        private Expr Start()
        {
            return Additive();
        }
        private Expr Additive()
        {
            Expr expr = Multiplicative();
            if (!isAtEnd() && TypeMatch("PLUS", "MINUS"))
            {
                Token op = previous();
                if (op == null)
                {
                    //IMPORTANT NOTE: ERROR HERE
                    throw new NotImplementedException();
                }
                Expr right = Additive();
                expr = new Expr.Binary(expr, op, right);
            }
            return expr;
        }

        private Expr Multiplicative()
        {
            Expr expr = Primary();
            if (!isAtEnd() && TypeMatch("MULTIPLY", "DIVIDE", "MODULO"))
            {
                Token op = previous();
                if (op == null)
                {
                    //IMPORTANT NOTE: ERROR HERE
                    throw new NotImplementedException();
                }
                Expr right = Additive();
                expr = new Expr.Binary(expr, op, right);
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
            }
            throw End();
        }

        private Exception End()
        {
            //IMPORTANT NOTE: add error here
            return new NotImplementedException();
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
        private void Expect(string type, string errorMessage)
        {
            if (current.type == type)
            {
                advance();
                return;
            }

            //IMPORTANT NOTE: add error here
            throw new NotImplementedException();
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
