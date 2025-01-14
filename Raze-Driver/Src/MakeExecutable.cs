using System;

using Raze;

namespace Raze_Driver;

internal partial class Shell
{
    public static void MakeExecutable(string path, SystemInfo systemInfo)
    {
        if (!File.Exists(path))
        {
            Diagnostics.Report(new Diagnostic.ImpossibleDiagnostic("Output executable file not found!"));
            return;
        }

        if (systemInfo.IsUnix())
        {
            var mode = File.GetUnixFileMode(path);
            mode |= UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            File.SetUnixFileMode(path, mode);
        }
    }
}
