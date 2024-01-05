using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Raze.Assembler.Instruction;

namespace Raze;

public partial class Assembler : 
    AssemblyExpr.IVisitor<Assembler.Instruction>, 
    AssemblyExpr.IUnaryOperandVisitor<Assembler.Instruction>, 
    AssemblyExpr.IBinaryOperandVisitor<Assembler.Instruction>
{
    internal List<byte> text = new List<byte>();
    internal List<byte> data = new List<byte>();

    internal int location { get; private set; } = 0;

    internal Linker.SymbolTable symbolTable = new Linker.SymbolTable();

    Encoder encoder = new Encoder();
    Encoder.Encoding encoding;

    public void Assemble(CodeGen.Assembly assembly)
    {
        foreach (var assemblyExpr in assembly.text)
        {
            foreach (byte[] bytes in assemblyExpr.Accept(this).ToBytes())
            {
                text.AddRange(bytes);
                location += bytes.Length;
            }
        }
        Linker.ResolveLocalProcedureRefs(text, symbolTable);
        Linker.ResolveProcedureRefs(text, symbolTable);

        location = 0;

        foreach (var assemblyExpr in assembly.data)
        {
            foreach (byte[] bytes in assemblyExpr.Accept(this).ToBytes())
            {
                data.AddRange(bytes);
                location += bytes.Length;
            }
        }
    }

    public Instruction VisitBinary(AssemblyExpr.Binary instruction)
    {
        encoding = encoder.GetEncoding(instruction);

        int localLocation = location;
        List<IInstruction> instructions = new();

        if (Encoder.EncodingUtils.SetAddressSizeOverridePrefix(instruction.operand1) || 
            Encoder.EncodingUtils.SetAddressSizeOverridePrefix(instruction.operand2))
        {
            instructions.Add(new AddressSizeOverridePrefix());
        }
        if (Encoder.EncodingUtils.SetSizePrefix(encoding.encodingType))
        {
            instructions.Add(new InstructionOpCodeSizePrefix());
        }
        if (Encoder.EncodingUtils.SetRexPrefix(instruction, encoding, out RexPrefix rexPrefix))
        {
            instructions.Add(rexPrefix);
        }

        instructions.Add(new InstructionOpCode(encoding.OpCode));
        location += instructions.Count;

        IEnumerable<IInstruction> operandInstructions = instruction.operand2.Accept(this, instruction.operand1).Instructions;

        // Assumes ModRegRm byte is always first byte emitted
        if (encoding.encodingType.HasFlag(Encoder.Encoding.EncodingTypes.NoModRegRM))
        {
            location--;
            operandInstructions = operandInstructions.Skip(1);
        }

        instructions.AddRange(operandInstructions);

        location = localLocation;
        return new Instruction(instructions.ToArray());
    }

    public Instruction VisitComment(AssemblyExpr.Comment instruction)
    {
        return new Instruction();
    }

    public Instruction VisitData(AssemblyExpr.Data instruction)
    {
        symbolTable.data[instruction.name] = location; 
        return encoder.EncodeData(instruction);
    }

    public Instruction VisitGlobal(AssemblyExpr.Global instruction)
    {
        return new Instruction();
    }

    public Instruction VisitLocalProcedure(AssemblyExpr.LocalProcedure instruction)
    {
        symbolTable.localLabels[instruction.name] = location;
        return new Instruction();
    }

    public Instruction VisitProcedure(AssemblyExpr.Procedure instruction)
    {
        Linker.ResolveLocalProcedureRefs(text, symbolTable);
        symbolTable.labels[instruction.name] = location;
        return new Instruction();
    }

    public Instruction VisitSection(AssemblyExpr.Section instruction)
    {
        throw new NotImplementedException();
    }

    public Instruction VisitUnary(AssemblyExpr.Unary instruction)
    {
        encoding = encoder.GetEncoding(instruction);

        int localLocation = location;
        List<IInstruction> instructions = new();

        if (Encoder.EncodingUtils.SetAddressSizeOverridePrefix(instruction.operand))
        {
            instructions.Add(new AddressSizeOverridePrefix());
        }
        if (Encoder.EncodingUtils.SetSizePrefix(encoding.encodingType))
        {
            instructions.Add(new InstructionOpCodeSizePrefix());
        }
        if (Encoder.EncodingUtils.SetRexPrefix(instruction, encoding, out RexPrefix rexPrefix))
        {
            instructions.Add(rexPrefix);
        }

        instructions.Add(new InstructionOpCode(encoding.OpCode));
        location += instructions.Count;

        IEnumerable<IInstruction> operandInstructions = instruction.operand.Accept(this).Instructions;

        // Assumes ModRegRm byte is always first byte emitted
        if (encoding.encodingType.HasFlag(Encoder.Encoding.EncodingTypes.NoModRegRM))
        {
            location--;
            operandInstructions = operandInstructions.Skip(1);
        }

        instructions.AddRange(operandInstructions);

        location = localLocation;
        return new Instruction(instructions.ToArray());
    }

    public Instruction VisitZero(AssemblyExpr.Zero instruction)
    {
        return new Instruction(new IInstruction[] { new InstructionOpCode(encoding.OpCode) });
    }

    public Instruction VisitRegisterRegister(AssemblyExpr.Register reg1, AssemblyExpr.Register reg2)
    {
        if (encoding.encodingType.HasFlag(Encoder.Encoding.EncodingTypes.AddRegisterToOpCode))
        {
            encoding.AddRegisterCode(Encoder.EncodingUtils.ExprRegisterToModRegRmRegister(reg1));
        }

        return new Instruction {
            Instructions = new IInstruction[] { 
                new ModRegRm(
                    ModRegRm.Mod.RegisterAdressingMode,
                    Encoder.EncodingUtils.ExprRegisterToModRegRmRegister(reg2),
                    Encoder.EncodingUtils.ExprRegisterToModRegRmRegister(reg1)
                ) 
            }
        };
    }

    public Instruction VisitRegisterMemory(AssemblyExpr.Register reg1, AssemblyExpr.Pointer ptr2)
    {
        if (encoding.encodingType.HasFlag(Encoder.Encoding.EncodingTypes.AddRegisterToOpCode))
        {
            encoding.AddRegisterCode(Encoder.EncodingUtils.ExprRegisterToModRegRmRegister(reg1));
        }

        if (ptr2.offset == 0 && Encoder.EncodingUtils.CanHaveZeroByteDisplacement(ptr2.register))
        {
            return new Instruction {
                Instructions = new IInstruction[] {
                    new ModRegRm(
                        ModRegRm.Mod.ZeroByteDisplacement,
                        Encoder.EncodingUtils.ExprRegisterToModRegRmRegister(reg1),
                        Encoder.EncodingUtils.ExprRegisterToModRegRmRegister(ptr2.register)
                    )
                }
            };
        }
        return new Instruction {
            Instructions = new IInstruction[] {
                new ModRegRm(
                    Encoder.EncodingUtils.GetDispSize(ptr2.offset),
                    Encoder.EncodingUtils.ExprRegisterToModRegRmRegister(reg1),
                    Encoder.EncodingUtils.ExprRegisterToModRegRmRegister(ptr2.register)
                ),
                Encoder.EncodingUtils.GetDispInstruction(ptr2.offset)
            }
        };
    }

    public Instruction VisitRegisterImmediate(AssemblyExpr.Register reg1, AssemblyExpr.Literal imm2)
    {
        if (encoding.encodingType.HasFlag(Encoder.Encoding.EncodingTypes.AddRegisterToOpCode))
        {
            encoding.AddRegisterCode(Encoder.EncodingUtils.ExprRegisterToModRegRmRegister(reg1));
        }

        return new Instruction { Instructions = new IInstruction[] {
            new ModRegRm(
                ModRegRm.Mod.RegisterAdressingMode,
                (ModRegRm.OpCodeExtension)encoding.OpCodeExtension,
                Encoder.EncodingUtils.ExprRegisterToModRegRmRegister(reg1)
            ),
            Encoder.EncodingUtils.GetImmInstruction(encoding.operands[1].size, imm2, this)
        } };
    }

    public Instruction VisitMemoryRegister(AssemblyExpr.Pointer ptr1, AssemblyExpr.Register reg2)
    {
        if (ptr1.offset == 0 && Encoder.EncodingUtils.CanHaveZeroByteDisplacement(ptr1.register))
        {
            return new Instruction
            {
                Instructions = new IInstruction[] {
                    new ModRegRm(
                        ModRegRm.Mod.ZeroByteDisplacement,
                        Encoder.EncodingUtils.ExprRegisterToModRegRmRegister(reg2),
                        Encoder.EncodingUtils.ExprRegisterToModRegRmRegister(ptr1.register)
                    )
                }
            };
        }
        return new Instruction
        {
            Instructions = new IInstruction[] {
                new ModRegRm(
                    Encoder.EncodingUtils.GetDispSize(ptr1.offset),
                    Encoder.EncodingUtils.ExprRegisterToModRegRmRegister(reg2),
                    Encoder.EncodingUtils.ExprRegisterToModRegRmRegister(ptr1.register)
                ),
                Encoder.EncodingUtils.GetDispInstruction(ptr1.offset)
            }
        };
    }

    public Instruction VisitMemoryMemory(AssemblyExpr.Pointer ptr1, AssemblyExpr.Pointer ptr2)
    {
        Encoder.EncodingUtils.ThrowIvalidEncodingType("Memory", "Memory");
        return new Instruction();
    }

    public Instruction VisitMemoryImmediate(AssemblyExpr.Pointer ptr1, AssemblyExpr.Literal imm2)
    {
        if (ptr1.offset == 0 && Encoder.EncodingUtils.CanHaveZeroByteDisplacement(ptr1.register))
        {
            return new Instruction
            {
                Instructions = new IInstruction[] {
                    new ModRegRm(
                        ModRegRm.Mod.ZeroByteDisplacement,
                        (ModRegRm.OpCodeExtension)encoding.OpCodeExtension,
                        Encoder.EncodingUtils.ExprRegisterToModRegRmRegister(ptr1.register)
                    ),
                    Encoder.EncodingUtils.GetImmInstruction(encoding.operands[1].size, imm2, this)
                }
            };
        }
        location += Encoder.EncodingUtils.Disp8Bit(ptr1.offset)? 1 : 4;
        return new Instruction
        {
            Instructions = new IInstruction[] {
                new ModRegRm(
                    Encoder.EncodingUtils.GetDispSize(ptr1.offset),
                    (ModRegRm.OpCodeExtension)encoding.OpCodeExtension,
                    Encoder.EncodingUtils.ExprRegisterToModRegRmRegister(ptr1.register)
                ),
                Encoder.EncodingUtils.GetDispInstruction(ptr1.offset),
                Encoder.EncodingUtils.GetImmInstruction(encoding.operands[1].size, imm2, this)
            }
        };
    }

    public Instruction VisitRegister(AssemblyExpr.Register reg)
    {
        if (encoding.encodingType.HasFlag(Encoder.Encoding.EncodingTypes.AddRegisterToOpCode))
        {
            encoding.AddRegisterCode(Encoder.EncodingUtils.ExprRegisterToModRegRmRegister(reg));
        }

        return new Instruction
        {
            Instructions = new IInstruction[] {
                new ModRegRm(
                    ModRegRm.Mod.RegisterAdressingMode, 
                    (ModRegRm.OpCodeExtension)encoding.OpCodeExtension,
                    Encoder.EncodingUtils.ExprRegisterToModRegRmRegister(reg)
                )
            }
        };
    }

    public Instruction VisitMemory(AssemblyExpr.Pointer ptr)
    {
        if (ptr.offset == 0 && Encoder.EncodingUtils.CanHaveZeroByteDisplacement(ptr.register))
        {
            return new Instruction
            {
                Instructions = new IInstruction[] {
                    new ModRegRm(
                        ModRegRm.Mod.ZeroByteDisplacement,
                        (ModRegRm.OpCodeExtension)encoding.OpCodeExtension,
                        Encoder.EncodingUtils.ExprRegisterToModRegRmRegister(ptr.register)
                    )
                }
            };
        }
        return new Instruction
        {
            Instructions = new IInstruction[] {
                new ModRegRm(
                    Encoder.EncodingUtils.GetDispSize(ptr.offset),
                    (ModRegRm.OpCodeExtension)encoding.OpCodeExtension,
                    Encoder.EncodingUtils.ExprRegisterToModRegRmRegister(ptr.register)
                ),
                Encoder.EncodingUtils.GetDispInstruction(ptr.offset)
            }
        };
    }

    public Instruction VisitImmediate(AssemblyExpr.Literal imm)
    {
        // No ModRegRm byte is emitted for a unary literal operand
        return new Instruction(new IInstruction[] { Encoder.EncodingUtils.GetImmInstruction(encoding.operands[0].size, imm, this) });
    }
}
