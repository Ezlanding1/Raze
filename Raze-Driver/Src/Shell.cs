using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.CommandLine;
using System.Diagnostics;
using Raze;

namespace Raze_Driver;

internal partial class Shell
{
    const string fileExtension = ".rz";

    static void Main(string[] args)
    {
        var rootCommand = new RootCommand("The Raze programming language compiler")
        {
            TreatUnmatchedTokensAsErrors = true
        };

        var compileOptions = GenerateCompileOptions();
        var initOptions = GenerateInitOptions();

        var compileCommand = new Command("compile", "Compile the given file");
        rootCommand.AddCommand(compileCommand);

        var runCommand = new Command("run", "Compile and run the given file");
        rootCommand.AddCommand(runCommand);

        var initCommand = new Command("init", "Initialize a new Raze program");
        rootCommand.AddCommand(initCommand);

        ((ICommandOptions)compileOptions).AddToCommand(compileCommand);
        ((ICommandOptions)compileOptions).AddToCommand(runCommand);
        ((ICommandOptions)initOptions).AddToCommand(initCommand);

        compileCommand.SetHandler(
            CompileProgram,
            compileOptions
        );

        runCommand.SetHandler((compileOptions) =>
            {
                if (compileOptions.SystemInfoModified)
                {
                    Diagnostics.Panic(new Diagnostic.DriverDiagnostic(Diagnostic.DiagnosticName.TargetSystemModified));
                }

                CompileProgram(compileOptions);

                if (!compileOptions.DryRunOption)
                    Process.Start(compileOptions.OutputOption);
            },
            compileOptions
        );

        initCommand.SetHandler((fileArgument) =>
            {
                using var fs = File.Create(fileArgument);
                InitTemplates.DefaultTemplate.CopyTo(fs);
            },
            initOptions.FileArgument
        );

        rootCommand.Invoke(args);
    }

    static void CompileProgram(CompileOptions compileOptions)
    {
        Diagnostics.mainFile = compileOptions.FileArgument;
        Diagnostics.debugErrors = compileOptions.DebugErrorsOption;
        Diagnostics.importDirs.AddRange(compileOptions.libraries);
        Diagnostics.runtimeName = compileOptions.SystemInfo.GetRuntimeName();
        try
        {
            Run(compileOptions);
        }
        catch (Exception exception)
        {
            Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic(exception));
        }
        
        MakeExecutable(compileOptions.OutputOption, compileOptions.SystemInfo, compileOptions.DryRunOption);
    }
}
