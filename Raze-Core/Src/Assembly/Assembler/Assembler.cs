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

    internal int TextLocation => text.Count;
    internal int DataLocation => data.Count;

    internal Linker.SymbolTable symbolTable = new Linker.SymbolTable();

    Encoder encoder = new Encoder();
    Encoder.Encoding encoding;

    internal bool nonResolvingPass = true;
    internal string enclosingLbl = string.Empty;

    public void Assemble(CodeGen.Assembly assembly)
    {
        AssembleTextSection(CodeGen.ISection.Text.GenerateDriverInstructions(SymbolTableSingleton.SymbolTable.main));

        AssembleTextSection(assembly.text);

        AssembleDataSection(assembly.data);
    }

    private void AssembleTextSection(CodeGen.ISection.Text section)
    {
        foreach (var assemblyExpr in section)
        {
            foreach (byte[] bytes in assemblyExpr.Accept(this).ToBytes())
            {
                text.AddRange(bytes);
            }
        }
    }
    private void AssembleDataSection(CodeGen.ISection.Data section)
    {
        foreach (var assemblyExpr in section)
        {
            foreach (byte[] bytes in assemblyExpr.Accept(this).ToBytes())
            {
                data.AddRange(bytes);
            }
        }
    }

    public Instruction VisitBinary(AssemblyExpr.Binary instruction)
    {
        encoding = encoder.GetEncoding(instruction, this, out bool refResolve);

        List<IInstruction> instructions = new();

        if (Encoder.EncodingUtils.SetAddressSizeOverridePrefix(instruction.operand1) || 
            Encoder.EncodingUtils.SetAddressSizeOverridePrefix(instruction.operand2))
        {
            instructions.Add(new Prefix(Encoder.Encoding.Prefix.AddressSizeOverridePrefix));
        }
        if (Encoder.EncodingUtils.SetSizePrefix(encoding.encodingType))
        {
            instructions.Add(new Prefix(Encoder.Encoding.Prefix.OperandSizeOverridePrefix));
        }
        if (Encoder.EncodingUtils.SetRexPrefix(instruction, encoding, out RexPrefix rexPrefix))
        {
            instructions.Add(rexPrefix);
        }

        IEnumerable<IInstruction> operandInstructions = instruction.operand2.Accept(this, instruction.operand1).Instructions;

        instructions.Add(new InstructionOpCode(encoding.opCode));

        // Assumes ModRegRm byte is always first byte emitted
        if (encoding.encodingType.HasFlag(Encoder.Encoding.EncodingTypes.NoModRegRM))
        {
            operandInstructions = operandInstructions.Skip(1);
        }

        instructions.AddRange(operandInstructions);

        if (nonResolvingPass && refResolve)
        {
            ((Linker.ReferenceInfo)symbolTable.unresolvedReferences[^1]).size = instructions.Sum(x => x.GetBytes().Length);
        }

        return new Instruction(instructions.ToArray());
    }

    public Instruction VisitComment(AssemblyExpr.Comment instruction)
    {
        return new Instruction();
    }

    public Instruction VisitData(AssemblyExpr.Data instruction)
    {
        if (!string.IsNullOrEmpty(instruction.name))
        {
            instruction.name = "data." + instruction.name;
            symbolTable.definitions[instruction.name] = DataLocation;
        }
        IInstruction[] data = [new RawInstruction(instruction.literal.value.SelectMany(x => x).ToArray())];
        return new Instruction(data);
    }

    public Instruction VisitGlobal(AssemblyExpr.Global instruction)
    {
        return new Instruction();
    }

    public Instruction VisitLocalProcedure(AssemblyExpr.LocalProcedure instruction)
    {
        instruction.name = enclosingLbl + '.' + instruction.name;
        symbolTable.definitions[instruction.name] = TextLocation;
        symbolTable.unresolvedReferences.Add(new Linker.DefinitionInfo(instruction.name));
        return new Instruction();
    }

    public Instruction VisitProcedure(AssemblyExpr.Procedure instruction)
    {
        instruction.name = "text." + instruction.name;
        enclosingLbl = instruction.name;
        symbolTable.definitions[instruction.name] = TextLocation;
        symbolTable.unresolvedReferences.Add(new Linker.DefinitionInfo(instruction.name));
        return new Instruction();
    }

    public Instruction VisitSection(AssemblyExpr.Section instruction)
    {
        throw new NotImplementedException();
    }

    public Instruction VisitUnary(AssemblyExpr.Unary instruction)
    {
        encoding = encoder.GetEncoding(instruction, this, out bool refResolve);

        List<IInstruction> instructions = new();

        if (Encoder.EncodingUtils.SetAddressSizeOverridePrefix(instruction.operand))
        {
            instructions.Add(new Prefix(Encoder.Encoding.Prefix.AddressSizeOverridePrefix));
        }
        if (Encoder.EncodingUtils.SetSizePrefix(encoding.encodingType))
        {
            instructions.Add(new Prefix(Encoder.Encoding.Prefix.OperandSizeOverridePrefix));
        }
        if (Encoder.EncodingUtils.SetRexPrefix(instruction, encoding, out RexPrefix rexPrefix))
        {
            instructions.Add(rexPrefix);
        }

        IEnumerable<IInstruction> operandInstructions = instruction.operand.Accept(this).Instructions;

        instructions.Add(new InstructionOpCode(encoding.opCode));

        // Assumes ModRegRm byte is always first byte emitted
        if (encoding.encodingType.HasFlag(Encoder.Encoding.EncodingTypes.NoModRegRM))
        {
            operandInstructions = operandInstructions.Skip(1);
        }

        instructions.AddRange(operandInstructions);

        if (nonResolvingPass && refResolve)
        {
            ((Linker.ReferenceInfo)symbolTable.unresolvedReferences[^1]).size = instructions.Sum(x => x.GetBytes().Length);
        }

        return new Instruction(instructions.ToArray());
    }

    public Instruction VisitZero(AssemblyExpr.Nullary instruction)
    {
        encoding = encoder.GetEncoding(instruction);

        List<IInstruction> instructions = new();

        instructions.Add(new InstructionOpCode(encoding.opCode));

        return new Instruction(instructions.ToArray());
    }

    public Instruction VisitRegisterRegister(AssemblyExpr.Register reg1, AssemblyExpr.Register reg2)
    {
        if (encoding.encodingType.HasFlag(Encoder.Encoding.EncodingTypes.AddRegisterToOpCode))
        {
            encoding.AddRegisterCode(Encoder.EncodingUtils.ExprRegisterToModRegRmRegister(reg1));
        }

        if (Encoder.EncodingUtils.SwapOperands(encoding))
        {
            (reg1, reg2) = (reg2, reg1);
        }

        return new Instruction {
            Instructions = new IInstruction[] { 
                new ModRegRm(
                    ModRegRm.Mod.RegisterAdressingMode,
                    Encoder.EncodingUtils.ExprRegisterToModRegRmRegister(reg1),
                    Encoder.EncodingUtils.ExprRegisterToModRegRmRegister(reg2)
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

        return new(
            Encoder.EncodingUtils.EncodeMemoryOperand(ptr2, (byte)Encoder.EncodingUtils.ExprRegisterToModRegRmRegister(reg1), this, 1)
            .ToArray()
        );
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
            Encoder.EncodingUtils.GetImmInstruction(encoding.operands[1].size, imm2, this, encoding.encodingType)
        } };
    }

    public Instruction VisitMemoryRegister(AssemblyExpr.Pointer ptr1, AssemblyExpr.Register reg2)
    {
        if (encoding.encodingType.HasFlag(Encoder.Encoding.EncodingTypes.AddRegisterToOpCode))
        {
            encoding.AddRegisterCode(Encoder.EncodingUtils.ExprRegisterToModRegRmRegister(reg2));
        }

        return new(
            Encoder.EncodingUtils.EncodeMemoryOperand(ptr1, (byte)Encoder.EncodingUtils.ExprRegisterToModRegRmRegister(reg2), this, 0).ToArray()
        );
    }

    public Instruction VisitMemoryMemory(AssemblyExpr.Pointer ptr1, AssemblyExpr.Pointer ptr2)
    {
        throw Encoder.EncodingUtils.ThrowIvalidEncodingType("Memory", "Memory");
    }

    public Instruction VisitMemoryImmediate(AssemblyExpr.Pointer ptr1, AssemblyExpr.Literal imm2)
    {
        var iinstructions = Encoder.EncodingUtils.EncodeMemoryOperand(ptr1, encoding.OpCodeExtension, this, 0).ToList();
        iinstructions.Add(Encoder.EncodingUtils.GetImmInstruction(encoding.operands[1].size, imm2, this, encoding.encodingType));

        return new Instruction(iinstructions.ToArray());
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
        return new Instruction(
            Encoder.EncodingUtils.EncodeMemoryOperand(ptr, encoding.OpCodeExtension, this, 0)
            .ToArray()
        );
    }

    public Instruction VisitImmediate(AssemblyExpr.Literal imm)
    {
        // No ModRegRm byte is emitted for a unary literal operand
        return new Instruction(new IInstruction[] { Encoder.EncodingUtils.GetImmInstruction(encoding.operands[0].size, imm, this, encoding.encodingType) });
    }
}
