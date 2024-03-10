using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.CommandLine;

namespace Raze_Driver;

internal partial class Shell
{
    internal class InitOptions : ICommandOptions
    {
        public required Argument<string> FileArgument { get; set; }

        public List<Argument> GetArguments()
        {
            return new List<Argument>() { FileArgument };
        }

        public List<Option> GetOptions()
        {
            return new List<Option>() { };
        }
    }
}
