using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.CommandLine;

namespace Raze_Driver;

internal partial class Shell
{
    static CompileOptionsBinder GenerateCompileOptions()
    {
        var fileArgument = new Argument<FileInfo>(
            name: "file",
            description: "Input file to be compiled",
            parse: result =>
            {
                string? filePath = result.Tokens.Single().Value;
                if (!File.Exists(filePath))
                {
                    result.ErrorMessage = $"File {filePath} does not exist";
                    return null;
                }
                else if (!filePath.EndsWith(fileExtension))
                {
                    result.ErrorMessage = $"File {filePath} must end with '{fileExtension}'";
                    return null;
                }
                else
                {
                    return new FileInfo(filePath);
                }
            }
        );

        var debugOption = GenerateSimpleOption("--debug", "Print all compiler debugging info to Console (same as '--debug-input --debug-tokens --debug-ast --debug-assembly --debug-errors')");
        debugOption.AddAlias("--debug-all");

        var dryRunOption = GenerateSimpleOption("--dry-run", "Supress creation of output binary");
        debugOption.AddAlias("-d");

        var outputOption = new Option<string>(
            name: "--output",
            description: "Name of output binary"
        );
        outputOption.SetDefaultValue("output.elf");
        outputOption.AddAlias("-o");

        return new() {
            FileArgument = fileArgument,
            OutputOption = outputOption,
            DebugOption = debugOption,
            DebugInputOption = GenerateSimpleOption("--debug-input", "Print compiler input to Console"),
            DebugTokensOption = GenerateSimpleOption("--debug-tokens", "Print lexer tokens to Console"),
            DebugAstOption = GenerateSimpleOption("--debug-ast", "Print AST graph to Console"),
            DebugAssemblyOption = GenerateSimpleOption("--debug-assembly", "Print output assembly to Console"),
            DebugErrorsOption = GenerateSimpleOption("--debug-errors", "Print full stacktrace of internal exceptions to Console"),
            DryRunOption = dryRunOption
        };
    }

    private static Option<bool> GenerateSimpleOption(string name, string description)
    {
        var simpleOption = new Option<bool>(name, x => true)
        {
            Arity = ArgumentArity.Zero,
            Description = description
        };
        return simpleOption;
    }
}
