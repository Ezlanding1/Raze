using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.CommandLine;
using System.CommandLine.Binding;
using Raze;

namespace Raze_Driver;

internal partial class Shell
{
    class CompileOptions
    {
        public required FileInfo FileArgument { get; init; }
        public required string OutputOption { get; set; }

        public bool DebugOption
        {
            set
            {
                if (value)
                {
                    DebugInputOption = true;
                    DebugTokensOption = true;
                    DebugAstOption = true;
                    DebugAssemblyOption = true;
                    DebugErrorsOption = true;
                }
            }
        }
        public required bool DebugInputOption { get; set; }
        public required bool DebugTokensOption { get; set; }
        public required bool DebugAstOption { get; set; }
        public required bool DebugAssemblyOption { get; set; }
        public required bool DebugErrorsOption { get; set; }
        public required string AssemblyFlavorOption { get; set; }

        public required bool DryRunOption { get; init; }
        public SystemInfo SystemInfo { get; init; }
        public readonly List<DirectoryInfo> libraries = new();
        public required string[]? LibraryArgument 
        {
            init
            {
                if (value == null)
                {
                    return;
                }
                foreach (var library in value)
                {
                    if (Directory.Exists(library))
                    {
                        libraries.Add(new DirectoryInfo(library));
                    }
                    else
                    {
                        Diagnostics.Report(new Diagnostic.DriverDiagnostic(Diagnostic.DiagnosticName.LibraryDirectoryPathNotFound, library));
                    }
                }
            }
        }

        public bool SystemInfoModified =>
            this.SystemInfo.architecture != SystemInfoGenerator.GetArchitecture ||
            this.SystemInfo.osabi != SystemInfoGenerator.GetOsabi ||
            this.SystemInfo.bitFormat != SystemInfoGenerator.GetBitFormat;

        public CompileOptions(SystemInfo.CPU_Architecture? architecture, SystemInfo.OsAbi? osAbi, SystemInfo.BitFormat? bitFormat)
        {
            SystemInfo = new SystemInfo
            (
                architecture ?? SystemInfoGenerator.GetArchitecture,
                osAbi ?? SystemInfoGenerator.GetOsabi,
                bitFormat ?? SystemInfoGenerator.GetBitFormat
            );
        }
    }

    class CompileOptionsBinder : BinderBase<CompileOptions>, ICommandOptions
    {
        public required Argument<FileInfo> FileArgument { get; init; }
        public required Option<string> OutputOption { get; init; }
        public required Option<bool> DebugOption { get; init; }
        public required Option<bool> DebugInputOption { get; init; }
        public required Option<bool> DebugTokensOption { get; init; }
        public required Option<bool> DebugAstOption { get; init; }
        public required Option<bool> DebugAssemblyOption { get; init; }
        public required Option<bool> DebugErrorsOption { get; init; }
        public required Option<bool> DryRunOption { get; init; }
        public required Option<string?> Architecture { get; init; }
        public required Option<string?> Osabi { get; init; }
        public required Option<string?> BitFormat { get; init; }
        public required Option<string[]?> LibraryOption { get; init; }
        public required Option<string> AssemblyFlavorOption { get; init; }

        public List<Option> GetOptions()
        {
            return new List<Option>() { OutputOption, DebugOption, DebugInputOption, DebugTokensOption, DebugAstOption, DebugAssemblyOption, DebugErrorsOption, DryRunOption, Architecture, Osabi, BitFormat, LibraryOption, AssemblyFlavorOption };
        }
        public List<Argument> GetArguments()
        {
            return new List<Argument>() { FileArgument };
        }

        protected override CompileOptions GetBoundValue(BindingContext bindingContext)
        {
            const string defaultFileName = "output";

            var compileOptions = new CompileOptions
            (
                ParseSystemInfoOption<SystemInfo.CPU_Architecture>(bindingContext.ParseResult.GetValueForOption(Architecture), Diagnostic.DiagnosticName.UnsupportedSystem_CPU_Architecture),
                ParseSystemInfoOption<SystemInfo.OsAbi>(bindingContext.ParseResult.GetValueForOption(Osabi), Diagnostic.DiagnosticName.UnsupportedSystem_OsAbi),
                ParseSystemInfoOption<SystemInfo.BitFormat>(bindingContext.ParseResult.GetValueForOption(BitFormat), Diagnostic.DiagnosticName.UnsupportedSystem_BitFormat)
            )
            {
                FileArgument = bindingContext.ParseResult.GetValueForArgument(FileArgument),
                OutputOption = bindingContext.ParseResult.GetValueForOption(OutputOption),
                DebugInputOption = bindingContext.ParseResult.GetValueForOption(DebugInputOption),
                DebugTokensOption = bindingContext.ParseResult.GetValueForOption(DebugTokensOption),
                DebugAstOption = bindingContext.ParseResult.GetValueForOption(DebugAstOption),
                DebugAssemblyOption = bindingContext.ParseResult.GetValueForOption(DebugAssemblyOption),
                DebugErrorsOption = bindingContext.ParseResult.GetValueForOption(DebugErrorsOption),
                DryRunOption = bindingContext.ParseResult.GetValueForOption(DryRunOption),
                LibraryArgument = bindingContext.ParseResult.GetValueForOption(LibraryOption),
                AssemblyFlavorOption = bindingContext.ParseResult.GetValueForOption(AssemblyFlavorOption)
            };
            compileOptions.DebugOption = bindingContext.ParseResult.GetValueForOption(DebugOption);
            compileOptions.OutputOption ??= defaultFileName + compileOptions.SystemInfo.GetDefualtFileExtension();

            return compileOptions;
        }

        private T? ParseSystemInfoOption<T>(string? str, Diagnostic.DiagnosticName diagnosticName) 
            where T : struct
        {
            if (str is null) 
                return default;

            if (Enum.TryParse(str, true, out T result))
            {
                return result;
            }
            throw Diagnostics.Panic(new Diagnostic.DriverDiagnostic(diagnosticName, str));
        }
    }
}
