using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace Raze;

public abstract partial class Diagnostic
{
    private const string DiagnosticCodePrefix = "RZ";

    private string GetDiagnosticHeader(Diagnostic diagnostic) =>
        $"{GetDiagnosticCode(diagnostic.name)}:{GetDiagnosticName(diagnostic)}";

    private string GetDiagnosticName(Diagnostic diagnostic)
    {
        var unescapedName = diagnostic.name.ToString();

        int hideIdx = unescapedName.IndexOf('_');
        if (hideIdx != -1)
        {
            unescapedName = unescapedName[0..hideIdx];
        }
        return Regex.Replace(unescapedName, "[A-Z]", " $0");
    }

    private string GetDiagnosticCode(DiagnosticName diagnosticName)
    {
        if (diagnosticName < 0)
            return DiagnosticCodePrefix + ((int)diagnosticName).ToString();

		var digits = Enum.GetNames(typeof(DiagnosticName)).Length.ToString().Length;
		var formatting = new string('0', digits);
		
		return DiagnosticCodePrefix + ((int)diagnosticName).ToString(formatting);
	}
}