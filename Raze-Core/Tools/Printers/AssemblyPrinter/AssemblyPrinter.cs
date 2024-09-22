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

    public static void PrintAssembly(CodeGen.Assembly assembly, string syntax)
    {
        AssemblyPrinter printer = new(assembly);
        printer.PrintAssembly(syntax);
    }

    public void PrintAssembly(string syntax)
    {
        // Note: Only Intel (NASM) assembly syntax is currently supported
        Syntaxes.SyntaxFactory.ISyntaxFactory Syntax = syntax.ToLower() switch
        {
            "intel" or "nasm" => Syntaxes.SyntaxFactory.SyntaxTypeCreator.FactoryMethod(Syntaxes.SyntaxFactory.SyntaxTypeCreator.AssemblySyntax.Intel_x86_64_NASM),
            _ => throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic($"Assembly flavor '{syntax}' not supported"))
        };

        Syntax.Run(Syntax.header);
        Syntax.Run(CodeGen.ISection.Text.GenerateHeaderInstructions());
        Syntax.Run(CodeGen.ISection.Text.GenerateDriverInstructions(SymbolTableSingleton.SymbolTable.main));
        Syntax.Run(assembly.text);
        Syntax.Run(CodeGen.ISection.Data.GenerateHeaderInstructions());
        Syntax.Run(assembly.data);
        Console.WriteLine(Syntax.Output);
    }
}
