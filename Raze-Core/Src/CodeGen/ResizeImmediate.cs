﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class AssemblyExpr
{
    internal partial class ImmediateGenerator
    {
        public static byte[] MinimizeImmediate(Literal.LiteralType literalType, byte[] value)
        {
            switch (literalType)
            {
                case Literal.LiteralType.Integer:
                    {
                        bool isNegative = value[^1] >= 128;
                        int dCount;
                        if (isNegative)
                        {
                            dCount = value.Reverse().TakeWhile(x => x == 255).Count();
                            if (value[Math.Max(value.Length - dCount - 1, 0)] < 128)
                                dCount--;
                        }
                        else
                        {
                            dCount = value.Reverse().TakeWhile(x => x == 0).Count();
                            if (value[Math.Max(value.Length - dCount - 1, 0)] >= 128)
                                dCount--;
                        }

                        int sCount = value.Length - dCount;

                        if (sCount <= 1)
                            Array.Resize(ref value, 1);
                        else if (sCount <= 2)
                            Array.Resize(ref value, 2);
                        else if (sCount <= 4)
                            Array.Resize(ref value, 4);

                        break;
                    }
                case Literal.LiteralType.UnsignedInteger:
                case Literal.LiteralType.Binary:
                case Literal.LiteralType.Hex:
                    {
                        int dCount = value.Reverse().TakeWhile(x => x == 0).Count();

                        if (dCount >= 7)
                            Array.Resize(ref value, 1);
                        else if (dCount >= 6)
                            Array.Resize(ref value, 2);
                        else if (dCount >= 4)
                            Array.Resize(ref value, 4);

                        break;
                    }
            };
            return value;
        }

        public static bool IsOne(Literal literal)
        {
            return literal.type switch
            {
                Literal.LiteralType.UnsignedInteger or
                Literal.LiteralType.Integer => (literal.value[0] == 1 && literal.value[1..].All(x => x == 0)),
                _ => false
            };
        }

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