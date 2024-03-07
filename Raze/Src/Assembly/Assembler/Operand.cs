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
        public struct Operand
        {
            internal OperandType type { get; private set; }
            internal OperandSize size { get; set; }

            internal Operand(OperandType operandType, OperandSize size)
            {
                this.type = operandType;
                this.size = size;
            }
            internal Operand(OperandType operandType, int size) : this(operandType, (OperandSize)size)
            {
            }

            internal bool Matches(Operand operand) => operand.type.HasFlag(this.type) && (type == OperandType.IMM ? (int)operand.size >= (int)this.size : operand.size == this.size);
            internal bool Matches(OperandType operandType) => operandType.HasFlag(this.type);

            [Flags]
            internal enum OperandType
            {
                R = 1 | A | P,
                M = 2,
                IMM = 4,
                A = 8,
                P = 16
            }
            internal enum OperandSize
            {
                _64Bits = 8,
                _32Bits = 4,
                _16Bits = 2,
                _8Bits = 1,
                _8BitsUpper = 0
            }

            internal static OperandType RegisterOperandType(AssemblyExpr.Register reg)
            {
                ThrowTMP(reg);

                return reg.Name switch
                {
                    AssemblyExpr.Register.RegisterName.RAX => OperandType.A,
                    AssemblyExpr.Register.RegisterName.RSP => OperandType.P,
                    AssemblyExpr.Register.RegisterName.RBP => OperandType.P,
                    AssemblyExpr.Register.RegisterName.RSI => OperandType.P,
                    AssemblyExpr.Register.RegisterName.RDI => OperandType.P,
                    _ => OperandType.R
                };
            }

            internal static void ThrowTMP(AssemblyExpr.Register reg)
            {
                if (reg.Name == AssemblyExpr.Register.RegisterName.TMP)
                {
                    Diagnostics.Panic(new Error.ImpossibleError("TMP Register Emitted"));
                }
            }
        }
    }
}
