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
            internal enum EncodingTypes
            {
                None = 0,
                RexPrefix = 1,
                RexWPrefix = 2,
                SizePrefix = 4,
                NoModRegRM = 8,
                SignExtends = 16,
                ZeroExtends = 32,
                AddRegisterToOpCode = 64,
                RelativeJump = 128,
                NoUpper8BitEncoding = 256
            }
        }
    }
}
