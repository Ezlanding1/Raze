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
            public static IList<IInstruction> EncodeMemoryOperand(AssemblyExpr.Pointer ptr1, byte operand2, Func<IInstruction[]> displacementCallback)
            {
                if (ptr1.value.IsRegister(out var reg1))
                {
                    if (ptr1.offset == 0) // [Base]
                    {
                        if (BaseAddressingModeMustHaveDisplacement(reg1))
                        {
                            return [
                                new Instruction.ModRegRm(
                                    Instruction.ModRegRm.Mod.OneByteDisplacement,
                                    operand2,
                                    ExprRegisterToModRegRmRegister(reg1)
                                ),
                                GetDispInstruction(ptr1.offset)
                            ];
                        }
                        else if (BaseAddressingModeMustHaveSIB(reg1))
                        {
                            return [
                                new Instruction.ModRegRm(
                                    Instruction.ModRegRm.Mod.SibNoDisplacement,
                                    operand2,
                                    ExprRegisterToModRegRmRegister(reg1)
                                ),
                                new Instruction.SIB(Instruction.SIB.Scale.TimesOne, Instruction.SIB.Index.Illegal, Instruction.SIB.Base.ESP)
                            ];
                        }
                        return [
                            new Instruction.ModRegRm(
                                Instruction.ModRegRm.Mod.ZeroByteDisplacement,
                                operand2,
                                ExprRegisterToModRegRmRegister(reg1)
                            )
                        ];
                    }
                    return [ // [Base + Displacement]
                        new Instruction.ModRegRm(
                            GetDispSize(ptr1.offset),
                            operand2,
                            ExprRegisterToModRegRmRegister(reg1)
                        ),
                        GetDispInstruction(ptr1.offset)
                    ];
                }
                else // [Displacement]
                {
                    return displacementCallback();
                }
            }
        }
    }
}