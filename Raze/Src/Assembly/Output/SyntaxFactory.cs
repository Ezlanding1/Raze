using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

partial class Syntaxes
{
    public abstract partial class SyntaxFactory
    {
        public abstract class  ISyntaxFactory
        {
            public abstract void Run(List<Instruction> instructions);
            public abstract void Run(Instruction instruction);

            public StringBuilder Output = new();

            public Instruction.Comment header;
            public abstract List<Instruction> GenerateHeaderInstructions(Expr.Function main);
        }

        public class SyntaxTypeCreator
        {
            public static ISyntaxFactory? FactoryMethod(string type)
            {
                return type switch
                {
                    "Intel_x86_64_NASM" => new IntelSyntax(),
                    "Intel_x86_64_AT&T" => new AttSyntax(),
                    _ => null
                };
            }
        }
    }
}
