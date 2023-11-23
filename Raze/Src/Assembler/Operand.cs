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
        internal struct Operand
        {
            internal OperandType operandType { get; private set; }
            internal OperandSize size { get; private set; }

            public Operand(OperandType operandType, OperandSize size)
            {
                this.operandType = operandType;
                this.size = size;
            }
            public Operand(OperandType operandType, int size) : this(operandType, (OperandSize)size)
            {
            }

            public bool Matches(Operand operand) => operand.operandType.HasFlag(this.operandType) && (operandType == OperandType.IMM ? (int)operand.size >= (int)this.size : operand.size == this.size);
            public bool Matches(OperandType operandType) => operandType.HasFlag(this.operandType);

            [Flags]
            internal enum OperandType
            {
                R = 1 | A,
                M = 2,
                IMM = 4,
                A = 8,
            }
            internal enum OperandSize
            {
                _64Bits = 8,
                _32Bits = 4,
                _16Bits = 2,
                _8Bits = 1,
                _8BitsUpper = 0
            }
        }
    }
}
