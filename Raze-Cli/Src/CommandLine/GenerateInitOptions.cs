using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.CommandLine;

namespace Raze_Cli;

internal partial class Shell
{
    static InitOptions GenerateInitOptions()
    {
        var fileArgument = new Argument<string>(
            name: "file",
            description: "Output file",
            parse: result =>
            {
                string? filePath = result.Tokens.Single().Value;
                filePath += filePath.EndsWith(fileExtension)? "" : fileExtension;
                return filePath;
            }
        );
        fileArgument.SetDefaultValue("Program.rz");

        return new() {
            FileArgument = fileArgument
        };
    }
}
