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

            // When an OperandType with a static size is combined with an OperandType with a variable size (e.g. XMMRM), the static-sized operand must be defined here
            internal static readonly Dictionary<OperandType, OperandSize> constantSizeOperandTypeRM = new()
            {
                { OperandType.XMM, OperandSize._128Bits },
                { OperandType.MMX, OperandSize._64Bits }
            };

            internal bool Matches(Operand operand)
            {
                bool sizeMatches =
                    OperandType.IMM.HasFlag(operand.type) ?
                        (int)operand.size >= (int)this.size :
                    constantSizeOperandTypeRM.TryGetValue(this.type, out OperandSize value) ?
                        value == this.size :
                        operand.size == this.size;

                return operand.type.HasFlag(this.type) && sizeMatches;
            }

            [Flags]
            internal enum OperandType
            {
                R = RNA | A,                // General Purpose Register
                RNA = 1 | C | D,            // GRP Non-Accumulator Register
                A = 2,                      // Accumulator Register (AL/AX/EAX/RAX)
                C = 4,                      // Count Register (CL/CX/ECX/RCX)
                D = 8,                      // Data Register (DL/DX/EDX/RDX)
                M = 16,                     // Memory
                MOFFS = 32,                 // 64-Bit Memory Offset
                IMM = 64 | One,             // Immediate
                One = 128,                  // Immediate Operand '1'
                XMM = 256,                  // SSE Register
                MMX = 512,                  // MMX Register
                CR = 1024,                  // Control Register
                DR = 2048,                  // Debug Register
                TR = 4096,                  // Test Register
                SEG = 8192 | CS | FS | GS,  // Segment Register
                CS = 16384,                 // Code Segment Register
                FS = 32768,                 // FS Segment Register
                GS = 65536,                 // GS Segment Register
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

                OperandType type = reg.name switch
                {
                    AssemblyExpr.Register.RegisterName.RAX => OperandType.A,
                    AssemblyExpr.Register.RegisterName.RCX => OperandType.C,
                    _ when reg.name >= AssemblyExpr.Register.RegisterName.XMM0 && reg.name <= AssemblyExpr.Register.RegisterName.XMM15 => OperandType.XMM,
                    _ => OperandType.R
                };
                OperandSize size = (OperandSize)Math.Max(1, (int)reg.Size);

                return new Operand(type, size);
            }

            internal static void ThrowTMP(AssemblyExpr.Register reg)
            {
                Diagnostics.Assert(
                    reg.name != AssemblyExpr.Register.RegisterName.TMP,
                    "TMP Register Cannot Be Emitted"
                );
            }
        }
    }
}
