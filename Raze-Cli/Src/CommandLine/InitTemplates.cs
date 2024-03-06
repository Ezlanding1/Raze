using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Raze_Cli;

internal partial class Shell
{
    static class InitTemplates
    {
        public static Stream DefaultTemplate => GetTemplate("DefaultTemplate.rz");

        private static Stream GetTemplate(string filePath)
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceStream("Raze_Cli.Src.Resources.Templates." + filePath);
        }
    }
}
