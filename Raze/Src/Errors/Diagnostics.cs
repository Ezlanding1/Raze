using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public static class Diagnostics
{
    const string InternalErrorHeading = "Internal Error:";
    const string ErrorPadding = "\n\n";
    private static bool errorEncountered = false;
    public static bool debugErrors;
    
    public static void ThrowCompilerErrors()
    {
        if (errorEncountered)
        {
            Environment.Exit(65);
        }
    }

    public static void ReportError(Error error)
    {
        errorEncountered = true;
        if (error is Error.ImpossibleError impossibleError)
        {
            Panic(impossibleError);
        }
        PrintError(error);
    }

    [DoesNotReturn]
    public static Exception Panic(Error.ImpossibleError error)
    {
        Console.WriteLine(InternalErrorHeading);
        PrintError(error);

        Environment.Exit(70);
        return new();
    }

    private static void PrintError(Error error)
    {
        Console.ForegroundColor = ConsoleColor.DarkRed;
        Console.WriteLine(error.ComposeErrorMessage());
        Console.WriteLine(ErrorPadding);
        Console.ResetColor();
    }
}
