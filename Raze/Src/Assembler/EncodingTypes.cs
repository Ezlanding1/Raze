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
                RexPrefix = 1,
                RexWPrefix = 2,
                ExpansionPrefix = 4,
                SizePrefix = 8
            }
        }
    }
}
