using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public class ErrorStack : Stack<Error>
{
    private const string ErrorPadding = "\n\n";

    internal new void Push(Error error)
    {
        if (error is Error.ImpossibleError)
        {
            Diagnostics.Panic(error);
        }
        base.Push(error);
    }

    public void ComposeErrorReport()
    {
        var color = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.DarkRed;

        while (Count > 1)
        {
            Console.WriteLine(this.Pop().ComposeErrorMessage() + ErrorPadding);
        }
        Console.WriteLine(this.Pop().ComposeErrorMessage());
        Console.ForegroundColor = color;
    }
}
