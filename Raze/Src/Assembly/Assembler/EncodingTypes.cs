using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Assembler
{
    public partial class Encoder
    {
        internal partial class Encoding
        {
            [Flags]
            internal enum EncodingTypes : byte
            {
                None = 0,
                RexPrefix = 1,
                RexWPrefix = 2,
                ExpansionPrefix = 4,
                SizePrefix = 8,
                NoModRegRM = 16,
                SignExtends = 32,
                AddRegisterToOpCode = 64
            }
        }
    }
}
