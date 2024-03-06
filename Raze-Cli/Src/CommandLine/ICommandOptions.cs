using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze_Cli;

internal partial class Shell
{
    interface ICommandOptions
    {
        public List<Option> GetOptions();
        public List<Argument> GetArguments();

        public void AddToCommand(Command command)
        {
            GetOptions().ForEach(command.AddOption);
            GetArguments().ForEach(command.AddArgument);
        }
    }
}