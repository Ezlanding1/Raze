using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

internal static class Diagnostics
{
    public static ErrorStack errors = new();

    public static void ThrowCompilerErrors()
    {
        if (errors.Count == 0) return;

        errors.ComposeErrorReport();
        Environment.Exit(65);
    }

    public static void Panic(Error error)
    {
        errors.ComposeErrorReport();
        
        Console.WriteLine("INTERNAL ERROR:");
        Console.WriteLine(error.ComposeErrorMessage());

        Environment.Exit(70);
    }
}
