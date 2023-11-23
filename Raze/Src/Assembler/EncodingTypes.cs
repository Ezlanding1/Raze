using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Assembler
{
    internal partial class Encoder
    {
        private partial class Encoding
        {
            [Flags]
            internal enum EncodingTypes : byte
            {
                None = 0,
                RexWPrefix = 1,
                ExpansionPrefix = 2,
                SizePrefix = 4
            }
        }
    }
}
