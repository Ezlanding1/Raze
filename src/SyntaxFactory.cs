﻿using System;
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
                    return type switch
                    {
                        "Intel_x86_64_NASM" => new IntelSyntax(),
                        "Intel_x86_64_GAS" => new GasSyntax(),
                        _ => throw new ArgumentException("Espionage Error: Invalid Type Given", type),
                    };
                }
            }
        }
    }
}