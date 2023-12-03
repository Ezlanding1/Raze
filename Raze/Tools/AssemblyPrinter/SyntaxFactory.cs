using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze.Tools;

partial class Syntaxes
{
    public abstract partial class SyntaxFactory
    {
        public abstract class  ISyntaxFactory
        {
            public abstract void Run(CodeGen.ISection instructions);
            public abstract void Run(AssemblyExpr instruction);

            public StringBuilder Output = new();

            public AssemblyExpr.Comment header;
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
