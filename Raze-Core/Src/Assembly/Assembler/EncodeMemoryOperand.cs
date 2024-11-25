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
            // Supports: [Base], [Base + Displacement], [Rip + Displacement], [Displacement]
            public static IList<IInstruction> EncodeMemoryOperand(AssemblyExpr.Pointer ptr1, byte operand2, Assembler assembler, int ptrIdx)
            {
                byte[] offset = GetImmInstruction(assembler.encoding.operands[ptrIdx].size, ptr1.offset, assembler, assembler.encoding.encodingType).GetBytes();

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
                else if (assembler.encoding.operands[ptrIdx].type != Operand.OperandType.MOFFS) // [Rip + Displacement]
                {
                    var instructions = new Instruction { Instructions = new IInstruction[2] };
                    instructions.Instructions[0] =
                        new Instruction.ModRegRm(
                            Instruction.ModRegRm.Mod.ZeroByteDisplacement,
                            (Instruction.ModRegRm.RegisterCode)operand2,
                            5
                        );

                    int location = 0;
                    if (!assembler.nonResolvingPass)
                    {
                        var refInfo = (Linker.ReferenceInfo)assembler.symbolTable.unresolvedReferences[assembler.symbolTable.sTableUnresRefIdx];
                        refInfo.absoluteAddress = false;
                        location = refInfo.location + refInfo.size;
                    }

                    var newOffset = new byte[8];
                    Array.Copy(offset, newOffset, offset.Length);
                    offset = newOffset;

                    offset = BitConverter.GetBytes(checked((int)((ulong)BitConverter.ToInt64(offset) - (ulong)location - assembler.textVirtualAddress)));
                    ShrinkSignedDisplacement(ref offset, 4);

                    instructions.Instructions[1] = new Instruction.Immediate(offset);

                    return instructions.Instructions;
                }
                else // [Displacement]
                {
                    ShrinkSignedDisplacement(ref offset, 8);

                    return new IInstruction[]
                    {
                         new Instruction.Immediate(offset)
                    };
                }
            }
        }
    }
}