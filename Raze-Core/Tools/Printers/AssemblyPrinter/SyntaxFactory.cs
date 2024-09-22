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
            public static ISyntaxFactory FactoryMethod(AssemblySyntax syntax)
            {
                return syntax switch
                {
                    AssemblySyntax.Intel_x86_64_NASM => new IntelSyntax(),
                    AssemblySyntax.Intel_x86_64_ATT => new AttSyntax()
                };
            }

            public enum AssemblySyntax
            {
                Intel_x86_64_NASM,
                Intel_x86_64_ATT
            }
        }
    }
}
