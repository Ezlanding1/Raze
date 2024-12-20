﻿using System;
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

    internal ulong textVirtualAddress;
    internal ulong dataVirtualAddress;

    public void Assemble(CodeGen.Assembly assembly)
    {
        AssembleTextSection(CodeGen.ISection.Text.GenerateDriverInstructions());

        AssembleTextSection(assembly.text);

        AssembleDataSection(assembly.data);

        AssembleIDataSection(assembly.idata);
    }

    private void AssembleSection(CodeGen.ISection section, List<byte>? asmSection)
    {
        foreach (var assemblyExpr in section)
        {
            foreach (byte[] bytes in assemblyExpr.Accept(this).ToBytes())
            {
                asmSection?.AddRange(bytes);
            }
        }
    }
    private void AssembleTextSection(CodeGen.ISection.Text section) => AssembleSection(section, text);
    private void AssembleDataSection(CodeGen.ISection.Data section) => AssembleSection(section, data);
    private void AssembleIDataSection(CodeGen.ISection.IData section) => AssembleSection(section, null);

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
        if (!string.IsNullOrEmpty(instruction.name) && nonResolvingPass)
        {
            instruction.name = "data." + instruction.name;
            symbolTable.definitions[instruction.name] = DataLocation;
            symbolTable.unresolvedReferences.Add(new Linker.DefinitionInfo(instruction.name, false));
        }

        List<IInstruction> data = [];

        foreach (var bytes in instruction.literal.value)
        {
            if (Encoder.EncodingUtils.IsReferenceLiteralType(instruction.literal.type))
            {
                var literal = new AssemblyExpr.LabelLiteral(instruction.literal.type, bytes); 

                literal.Name = literal.type switch
                {
                    AssemblyExpr.Literal.LiteralType.RefData => "data.",
                    AssemblyExpr.Literal.LiteralType.RefProcedure => "text.",
                    AssemblyExpr.Literal.LiteralType.RefLocalProcedure => enclosingLbl + '.' + literal.Name
                } + literal.Name;


                data.Add(Encoder.EncodingUtils.GetImmInstruction(Encoder.Operand.OperandSize._64Bits, literal, this, Encoder.Encoding.EncodingTypes.None));

                if (nonResolvingPass)
                {
                    int size = data[^1].GetBytes().Length;
                    symbolTable.unresolvedReferences.Add(new Linker.ReferenceInfo(instruction, DataLocation, size, false, size));
                }
            }
            else
            {
                data.Add(Encoder.EncodingUtils.GetImmInstruction((Encoder.Operand.OperandSize)bytes.Length, new AssemblyExpr.Literal(instruction.literal.type, bytes), this, Encoder.Encoding.EncodingTypes.None));
            }
        }
        return new Instruction(data.ToArray());
    }

    public Instruction VisitGlobal(AssemblyExpr.Global instruction)
    {
        return new Instruction();
    }

    public Instruction VisitLocalProcedure(AssemblyExpr.LocalProcedure instruction)
    {
        instruction.name = enclosingLbl + '.' + instruction.name;
        symbolTable.definitions[instruction.name] = TextLocation;
        symbolTable.unresolvedReferences.Add(new Linker.DefinitionInfo(instruction.name, true));
        return new Instruction();
    }

    public Instruction VisitProcedure(AssemblyExpr.Procedure instruction)
    {
        instruction.name = "text." + instruction.name;
        enclosingLbl = instruction.name;
        symbolTable.definitions[instruction.name] = TextLocation;
        symbolTable.unresolvedReferences.Add(new Linker.DefinitionInfo(instruction.name, true));
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

    public Instruction VisitInclude(AssemblyExpr.Include include)
    {
        symbolTable.includes.Add(include);
        return new Instruction();
    }
}
