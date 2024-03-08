﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public static class Diagnostics
{
    const string InternalErrorHeading = "Internal Error:";
    const string DiagnosticPadding = "\n\n";
    private static bool errorEncountered = false;
    public static bool debugErrors;
    
    public static void ThrowCompilerErrors()
    {
        if (errorEncountered)
        {
            Environment.Exit(65);
        }
    }

    public static void Report(Diagnostic diagnostic)
    {
        if (diagnostic is Diagnostic.ImpossibleDiagnostic impossibleDiagnostic)
        {
            Panic(impossibleDiagnostic);
        }

        switch (diagnostic.SeverityLevel)
        {
            case Diagnostic.Severity.Info:
                PrintDiagnostic(diagnostic, ConsoleColor.Gray);
                break;
            case Diagnostic.Severity.Warning:
                PrintDiagnostic(diagnostic, ConsoleColor.DarkYellow);
                break;
            case Diagnostic.Severity.Error:
                PrintDiagnostic(diagnostic, ConsoleColor.DarkRed);
                errorEncountered = true;
                break;
        }
    }

    [DoesNotReturn]
    public static Exception Panic(Diagnostic.ImpossibleDiagnostic impossibleDiagnostic)
    {
        Console.WriteLine(InternalErrorHeading);
        PrintDiagnostic(impossibleDiagnostic, ConsoleColor.DarkRed);

        Environment.Exit(70);
        return new();
    }

    private static void PrintDiagnostic(Diagnostic diagnostic, ConsoleColor consoleColor)
    {
        Console.ForegroundColor = consoleColor;
        Console.WriteLine(diagnostic.ComposeDiagnosticMessage());
        Console.WriteLine(DiagnosticPadding);
        Console.ResetColor();
    }
}
