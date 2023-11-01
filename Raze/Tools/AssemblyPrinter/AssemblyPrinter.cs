#define Intel_x86_64_NASM

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze.Tools;

public class AssemblyPrinter
{
    List<AssemblyExpr> instructions;
    List<AssemblyExpr> data;
    public AssemblyPrinter(List<AssemblyExpr> instructions, List<AssemblyExpr> data)
    {
        this.instructions = instructions;
        this.data = data;
    }

    public static void PrintAssembly(List<AssemblyExpr> instructions, List<AssemblyExpr> data)
    {
        AssemblyPrinter printer = new(instructions, data);
        printer.PrintAssembly(SymbolTableSingleton.SymbolTable.main);
    }

    public void PrintAssembly(Expr.Function main)
    {
        Syntaxes.SyntaxFactory.ISyntaxFactory Syntax;
        #if Intel_x86_64_NASM
        Syntax = Syntaxes.SyntaxFactory.SyntaxTypeCreator.FactoryMethod("Intel_x86_64_NASM");
        #endif

        if (Syntax == null)
        {
            Diagnostics.errors.Push(new Error.ImpossibleError("No Syntax Type Defined"));
        }
        
        Syntax.Run(Syntax.header);
        Syntax.Run(Syntax.GenerateHeaderInstructions(main));
        Syntax.Run(instructions);
        Syntax.Run(data);
        Console.WriteLine(Syntax.Output);
    }
}
