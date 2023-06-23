using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze
{
    internal partial class Analyzer
    {
        internal class Primitives
        {
            // Binary Operation
            public static Expr.Type Operation(Token op, int t1, Expr a, int t2, Expr b)
            {
                string pName = TypeCheckPass.SymbolToPrimitiveName(op);
                throw new NotImplementedException();
            }
            public static void InvalidOperation(Token op, string t1, string t2)
            {
                throw new Errors.AnalyzerError("Invalid Operator", $"Types '{t1}' and '{t2}' don't have a definition for '{TypeCheckPass.SymbolToPrimitiveName(op)}' ( '{op.lexeme}' )");
            }

            // Unary Operation
            public static Expr.Type Operation(Token op, int t1, Expr a)
            {
                string pName = TypeCheckPass.SymbolToPrimitiveName(op);
                throw new NotImplementedException();
            }
            public static void InvalidOperation(Token op, string t1)
            {
                throw new Errors.AnalyzerError("Invalid Operator", $"Type '{t1}' doesn't not have a definition for '{TypeCheckPass.SymbolToPrimitiveName(op)}' ( '{op.lexeme}' )");
            }
        }
    }
}
