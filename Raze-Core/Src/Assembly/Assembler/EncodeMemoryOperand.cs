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
        internal static partial class EncodingUtils
        {
            // Supports: [Base], [Base + Displacement], [Displacement]
            public static IList<IInstruction> EncodeMemoryOperand(AssemblyExpr.Pointer ptr1, byte operand2, Assembler assembler)
            {
                byte[] offset = ptr1.offset.Accept(assembler).Instructions[0].GetBytes();

                if (ptr1.value != null)
                {
                    if (offset.All(x => x == 0)) // [Base]
                    {
                        if (BaseAddressingModeMustHaveDisplacement(ptr1.value))
                        {
                            return [
                                new Instruction.ModRegRm(
                                    Instruction.ModRegRm.Mod.OneByteDisplacement,
                                    operand2,
                                    ExprRegisterToModRegRmRegister(ptr1.value)
                                ),
                                GetDispInstruction(offset)
                            ];
                        }
                        else if (BaseAddressingModeMustHaveSIB(ptr1.value))
                        {
                            return [
                                new Instruction.ModRegRm(
                                    Instruction.ModRegRm.Mod.SibNoDisplacement,
                                    operand2,
                                    ExprRegisterToModRegRmRegister(ptr1.value)
                                ),
                                new Instruction.SIB(Instruction.SIB.Scale.TimesOne, Instruction.SIB.Index.Illegal, Instruction.SIB.Base.ESP)
                            ];
                        }
                        return [
                            new Instruction.ModRegRm(
                                Instruction.ModRegRm.Mod.ZeroByteDisplacement,
                                operand2,
                                ExprRegisterToModRegRmRegister(ptr1.value)
                            )
                        ];
                    }
                    return [ // [Base + Displacement]
                        new Instruction.ModRegRm(
                            GetDispSize(offset),
                            operand2,
                            ExprRegisterToModRegRmRegister(ptr1.value)
                        ),
                        GetDispInstruction(offset)
                    ];
                }
                else // [Displacement]
                {
                    return new IInstruction[]
                    {
                        new Instruction.ModRegRm(
                            Instruction.ModRegRm.Mod.ZeroByteDisplacement,
                            (Instruction.ModRegRm.RegisterCode)operand2,
                            5
                        ),
                        new Instruction.Immediate(offset)
                    };
                }
            }
        }
    }
}