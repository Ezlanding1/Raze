using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Raze;

namespace Raze_Driver;

internal partial class Shell
{
    static class InitTemplates
    {
        private const string TemplatesPath = "Raze_Driver.Src.Resources.Templates.";

        public static Stream DefaultTemplate => GetTemplate("DefaultTemplate.rz");

        private static Stream GetTemplate(string filePath)
        {
            return Assembly
                .GetExecutingAssembly()
                .GetManifestResourceStream(TemplatesPath + filePath)
                    ?? throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("Template not found!"));
        }
    }
}
