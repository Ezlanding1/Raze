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
                R = 1 | A,
                M = 2 | MOFFS,
                IMM = 4,
                A = 8,
                MOFFS = 16,
                XMM = 32
            }
            internal enum OperandSize
            {
                _128Bits = 16,
                _64Bits = 8,
                _32Bits = 4,
                _16Bits = 2,
                _8Bits = 1
            }

            internal static Operand RegisterOperandType(AssemblyExpr.Register reg)
            {
                ThrowTMP(reg);

                OperandType type = reg.Name switch
                {
                    AssemblyExpr.Register.RegisterName.RAX => OperandType.A,
                    _ when reg.Name >= AssemblyExpr.Register.RegisterName.XMM0 && reg.Name <= AssemblyExpr.Register.RegisterName.XMM15 => OperandType.XMM,
                    _ => OperandType.R
                };
                OperandSize size = (OperandSize)Math.Max(1, (int)reg.Size);

                return new Operand(type, size);
            }

            internal static void ThrowTMP(AssemblyExpr.Register reg)
            {
                if (reg.Name == AssemblyExpr.Register.RegisterName.TMP)
                {
                    Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("TMP Register Emitted"));
                }
            }
        }
    }
}
