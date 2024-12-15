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
    const string DiagnosticPadding = "\n\n";
    private static bool errorEncountered = false;
    public static bool debugErrors;
    public static FileInfo mainFile;
    public static List<DirectoryInfo> importDirs;
    public static string runtimeName;

    public static void ThrowCompilerErrors()
    {
        if (errorEncountered)
        {
            Environment.Exit(65);
        }
    }

    public static void Report(Diagnostic diagnostic)
    {
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
            case Diagnostic.Severity.Exception:
                Panic(diagnostic);
                break;
        }
    }

    [DoesNotReturn]
    public static Exception Panic(Diagnostic diagnostic)
    {
        Console.WriteLine(InternalErrorHeading);
        PrintDiagnostic(diagnostic, ConsoleColor.DarkRed);

        Environment.Exit(70);
        return new();
    }

    public static void Assert(bool condition, string? message = null)
    {
        if (!condition)
        {
            Panic(new Diagnostic.ImpossibleDiagnostic(message ?? "Assertion Failed"));
        }
    }

    private static void PrintDiagnostic(Diagnostic diagnostic, ConsoleColor consoleColor)
    {
        Console.ForegroundColor = consoleColor;
        Console.WriteLine(diagnostic.ComposeDiagnosticMessage());
        Console.WriteLine(DiagnosticPadding);
        Console.ResetColor();
    }
}
