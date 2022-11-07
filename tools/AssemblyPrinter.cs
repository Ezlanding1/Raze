#define Intel_x86_64_NASM

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Espionage.tools
{
    internal class AssemblyPrinter
    {
        List<List<Instruction>> instructions;
        List<Instruction> data;
        public AssemblyPrinter(List<List<Instruction>> instructions, List<Instruction> data)
        {
            this.instructions = instructions;
            this.data = data;
        }

        public static void PrintAssembly(List<List<Instruction>> instructions, List<Instruction> data)
        {
            AssemblyPrinter printer = new(instructions, data);
            printer.PrintAssembly();
        }

        public void PrintAssembly()
        {
            Syntaxes.SyntaxFactory.ISyntaxFactory Syntax;
            #if Intel_x86_64_NASM
            Syntax = Syntaxes.SyntaxFactory.SyntaxTypeCreator.FactoryMethod("Intel_x86_64_NASM");
            #endif

            if (Syntax == null)
            {
                throw new Exception("Espionage Error: No Syntax Type Defined");
            }
            
            Syntax.Run(Syntax.header);
            Syntax.Run(Syntax.headerInstructions);
            Syntax.Run(instructions);
            Syntax.Run(data);
            Console.WriteLine(Syntax.Output);
        }
    }
}
