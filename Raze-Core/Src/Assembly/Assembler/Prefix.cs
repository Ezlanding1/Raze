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
            internal enum Prefix : byte
            {
                // Group 1 Prefixes:

                LockPrefix = 0xF0,

                RepnePrefix = 0xF2,
                RepnzPrefix = 0xF2,

                RepPrefix = 0xF3,
                RepePrefix = 0xF3,
                RepzPrefix = 0xF3,

                BndPrefix = 0xF2,


                // Group 2 Prefixes:

                CsSegmentOverridePrefix = 0x2E,
                SsSegmentOverridePrefix = 0x36,
                DsSegmentOverridePrefix = 0x3E,
                EsSegmentOverridePrefix = 0x26,
                FsSegmentOverridePrefix = 0x64,
                GsSegmentOverridePrefix = 0x65,

                BranchHintNotTakenPrefix = 0x2E,
                BranchHintTakenPrefix = 0x3E,


                // Group 3 Prefixes:

                OperandSizeOverridePrefix = 0x66,


                // Group 4 Prefixes:

                AddressSizeOverridePrefix = 0x67
            }
        }
    }
}
