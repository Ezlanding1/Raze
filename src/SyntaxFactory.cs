using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Espionage
{
    partial class Syntaxes
    {
        public abstract partial class SyntaxFactory
        {
            public abstract class  ISyntaxFactory
            {
                public abstract void Run(List<List<Instruction>> instructions);
                public abstract void Run(List<Instruction> instructions);
                public abstract void Run(Instruction instruction);

                public StringBuilder Output = new();

                public Instruction.Comment header;
                public List<List<Instruction>> headerInstructions;
            }

            public class SyntaxTypeCreator
            {
                public static ISyntaxFactory FactoryMethod(string type)
                {
                    switch (type)
                    {
                        case "Intel_x86_64": return new IntelSyntax();
                        case "Gas": return new GasSyntax();
                        default: throw new ArgumentException("Espionage Error: Invalid Type Given", type);
                    }
                }
            }
        }
    }
}
