using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class AssemblyExpr
{
    internal partial class ImmediateGenerator
    {
        public static bool ResizeImmediate(Literal literal, Assembler.Encoder.Operand.OperandSize size)
        {
            if ((int)literal.Size > (int)size || size == Assembler.Encoder.Operand.OperandSize._128Bits)
            {
                return false;
            }

            switch (literal.type)
            {
                case Literal.LiteralType.Integer:
                    var prevSize = (int)literal.Size;
                    bool isNegative = literal.value[^1] >= 128;
                    Array.Resize(ref literal.value, (int)size);
                    if (isNegative)
                    {
                        for (int i = prevSize; i < literal.value.Length; i++)
                        {
                            literal.value[i] = 0xFF;
                        }
                    }
                    return true;
                case Literal.LiteralType.UnsignedInteger:
                case Literal.LiteralType.Binary:
                case Literal.LiteralType.Hex:
                    Array.Resize(ref literal.value, (int)size);
                    return true;
            };
            return false;
        }

    }
}