#define Intel_x86_64_NASM

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze.Tools;

public class AssemblyPrinter
{
    CodeGen.Assembly assembly;

    public AssemblyPrinter(CodeGen.Assembly assembly)
    {
        this.assembly = assembly;
    }

    public static void PrintAssembly(CodeGen.Assembly assembly)
    {
        AssemblyPrinter printer = new(assembly);
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
            Diagnostics.errors.Push(new Error.ImpossibleError("No Syntax Type Defined"));
            return;
        }
        
        Syntax.Run(Syntax.header);
        Syntax.Run(CodeGen.ISection.Text.GenerateHeaderInstructions());
        Syntax.Run(CodeGen.ISection.Text.GenerateDriverInstructions(SymbolTableSingleton.SymbolTable.main));
        Syntax.Run(assembly.text);
        Syntax.Run(CodeGen.ISection.Data.GenerateHeaderInstructions());
        Syntax.Run(assembly.data);
        Console.WriteLine(Syntax.Output);
    }
}
