using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

internal class ErrorStack : Stack<Error>
{
    internal new void Push(Error error)
    {
        if (error is Error.ImpossibleError)
        {
            Diagnostics.Panic(error);
        }
        base.Push(error);
    }

    internal void ComposeErrorReport()
    {
        while (Count > 0)
        {
            Console.WriteLine(this.Pop().ComposeErrorMessage());
        }
    }
}
