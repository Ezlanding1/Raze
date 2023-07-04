using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

partial class Syntaxes
{
    partial class SyntaxFactory 
    {

        class IntelSyntax : ISyntaxFactory, Instruction.IVisitor
        {
            public IntelSyntax()
            {
                header = new Instruction.Comment("Raze Compiler Version ALPHA 0.0.0 Intel_x86-64 NASM");
            }
            public override List<Instruction> GenerateHeaderInstructions(Expr.Function main)
            {
                return new List<Instruction>()
                {
                    { new Instruction.Global("_start") },
                    { new Instruction.Section("text") },
                    { new Instruction.Procedure("_start") },
                    { new Instruction.Unary("CALL", new Instruction.ProcedureRef(Assembler.ToMangedName(main))) },
                    { new Instruction.Binary("MOV", new Instruction.Register(Instruction.Register.RegisterName.RDI, Instruction.Register.RegisterSize._64Bits), (Analyzer.TypeCheckPass.literalTypes[Parser.Literals[0]].Matches(main._returnType.type)) ? new Instruction.Register(Instruction.Register.RegisterName.RAX, Instruction.Register.RegisterSize._64Bits) : new Instruction.Literal(Parser.Literals[0], "0")) },
                    { new Instruction.Binary("MOV", new Instruction.Register(Instruction.Register.RegisterName.RAX, Instruction.Register.RegisterSize._64Bits), new Instruction.Literal(Parser.Literals[0], "60")) },
                    { new Instruction.Zero("SYSCALL") }
                };
            }

            public override void Run(List<Instruction> instructions)
            {
                foreach (var instruction in instructions)
                {
                    Run(instruction);
                }
            }

            public override void Run(Instruction instruction)
            {
                Output.AppendLine(instruction.Accept(this));
            }

            public string visitBinary(Instruction.Binary instruction)
            {
                if (instruction is Instruction.StackAlloc)
                    if (instruction.operand2 is Instruction.Literal)
                        return $"{instruction.instruction}\t{instruction.operand1.Accept(this)}, {AlignTo16((Instruction.Literal)instruction.operand2)}";
                    else
                        //
                        throw new Exception();
                if (instruction.operand2 is Instruction.Pointer)
                    return $"{instruction.instruction}\t{instruction.operand1.Accept(this)}, {PointerToString((Instruction.Pointer)instruction.operand2)}";

                return $"{instruction.instruction}\t{instruction.operand1.Accept(this)}, {instruction.operand2.Accept(this)}";
            }

            public string visitComment(Instruction.Comment instruction)
            {
                return $"; {instruction.comment}";
            }

            public string visitData(Instruction.Data instruction)
            {
                return $"{instruction.name}: {instruction.size} {instruction.value}";
            }

            public string visitDataRef(Instruction.DataRef instruction)
            {
                return $"{instruction.dataName}";
            }

            public string visitProcedure(Instruction.Procedure instruction)
            {
                return $"{instruction.name}:";
            }

            public string visitLocalProcedure(Instruction.LocalProcedure instruction)
            {
                return $".{instruction.name}:";
            }

            public string visitGlobal(Instruction.Global instruction)
            {
                return $"global {instruction.name}\n";
            }

            public string visitPointer(Instruction.Pointer instruction)
            {
                if (instruction.register.name == Instruction.Register.RegisterName.TMP)
                {
                    throw new Errors.ImpossibleError("TMP Register Cannot Be Emitted");
                }
                return $"{InstructionUtils.wordSize[instruction.size]} [{RegisterToString[(instruction.register.name, InstructionUtils.SYS_SIZE)]} {instruction._operator} {instruction.offset}]";
            }

            public string visitProcedureRef(Instruction.ProcedureRef instruction)
            {
                return $"{instruction.name}";
            }

            public string visitLocalProcedureRef(Instruction.LocalProcedureRef instruction)
            {
                return $".{instruction.name}";
            }

            public string visitRegister(Instruction.Register instruction)
            {
                if (instruction.name == Instruction.Register.RegisterName.TMP)
                {
                    throw new Errors.ImpossibleError("TMP Register Cannot Be Emitted");
                }

                return $"{RegisterToString[(instruction.name, instruction.size)]}";
            }

            public string visitSection(Instruction.Section instruction)
            {
                return $"section .{instruction.name}\n";
            }

            public string visitUnary(Instruction.Unary instruction)
            {
                return $"{instruction.instruction}\t{instruction.operand.Accept(this)}";
            }

            public string visitZero(Instruction.Zero instruction)
            {
                return $"{instruction.instruction}";
            }

            private string PointerToString(Instruction.Pointer instruction)
            {
                if (instruction.register.name == Instruction.Register.RegisterName.TMP)
                {
                    throw new Errors.ImpossibleError("TMP Register Cannot Be Emitted");
                }
                return $"[{RegisterToString[(instruction.register.name, InstructionUtils.SYS_SIZE)]} {instruction._operator} {instruction.offset}]";
            }

            public string visitLiteral(Instruction.Literal instruction)
            {
                return $"{instruction.value}";
            }

            private string AlignTo16(Instruction.Literal instruction)
            {
                if (int.TryParse(instruction.value, out var value))
                {
                    string name = (((int)Math.Ceiling(value / 16f)) * 16).ToString();
                    return new Instruction.Literal(instruction.type, name).Accept(this);
                }
                else
                {
                    throw new Errors.ImpossibleError("Size of aligned literal is not numeric");
                }
            }

            private static Dictionary<(Instruction.Register.RegisterName, Instruction.Register.RegisterSize?), string> RegisterToString = new()
            {
                { (Instruction.Register.RegisterName.RAX, Instruction.Register.RegisterSize._64Bits), "RAX" }, // 64-Bits 
                { (Instruction.Register.RegisterName.RAX, Instruction.Register.RegisterSize._32Bits), "EAX" }, // Lower 32-Bits
                { (Instruction.Register.RegisterName.RAX, Instruction.Register.RegisterSize._16Bits), "AX" }, // Lower 16-Bits
                { (Instruction.Register.RegisterName.RAX, Instruction.Register.RegisterSize._8BitsUpper), "AH" }, // Upper 16-Bits
                { (Instruction.Register.RegisterName.RAX, Instruction.Register.RegisterSize._8Bits), "AL" }, // Lower 8-Bits

                { (Instruction.Register.RegisterName.RCX, Instruction.Register.RegisterSize._64Bits), "RCX" },
                { (Instruction.Register.RegisterName.RCX, Instruction.Register.RegisterSize._32Bits), "ECX" },
                { (Instruction.Register.RegisterName.RCX, Instruction.Register.RegisterSize._16Bits), "CX" },
                { (Instruction.Register.RegisterName.RCX, Instruction.Register.RegisterSize._8BitsUpper), "CH" },
                { (Instruction.Register.RegisterName.RCX, Instruction.Register.RegisterSize._8Bits), "CL" },

                { (Instruction.Register.RegisterName.RDX, Instruction.Register.RegisterSize._64Bits), "RDX" },
                { (Instruction.Register.RegisterName.RDX, Instruction.Register.RegisterSize._32Bits), "EDX" },
                { (Instruction.Register.RegisterName.RDX, Instruction.Register.RegisterSize._16Bits), "DX" },
                { (Instruction.Register.RegisterName.RDX, Instruction.Register.RegisterSize._8BitsUpper), "DH" },
                { (Instruction.Register.RegisterName.RDX, Instruction.Register.RegisterSize._8Bits), "DL" },

                { (Instruction.Register.RegisterName.RBX, Instruction.Register.RegisterSize._64Bits), "RBX" },
                { (Instruction.Register.RegisterName.RBX, Instruction.Register.RegisterSize._32Bits), "EBX" },
                { (Instruction.Register.RegisterName.RBX, Instruction.Register.RegisterSize._16Bits), "BX" },
                { (Instruction.Register.RegisterName.RBX, Instruction.Register.RegisterSize._8BitsUpper), "BH" },
                { (Instruction.Register.RegisterName.RBX, Instruction.Register.RegisterSize._8Bits), "BL" },

                { (Instruction.Register.RegisterName.RSI, Instruction.Register.RegisterSize._64Bits), "RSI" },
                { (Instruction.Register.RegisterName.RSI, Instruction.Register.RegisterSize._32Bits), "ESI" },
                { (Instruction.Register.RegisterName.RSI, Instruction.Register.RegisterSize._16Bits), "SI" },
                { (Instruction.Register.RegisterName.RSI, Instruction.Register.RegisterSize._8Bits), "SIL" },

                { (Instruction.Register.RegisterName.RDI, Instruction.Register.RegisterSize._64Bits), "RDI" },
                { (Instruction.Register.RegisterName.RDI, Instruction.Register.RegisterSize._32Bits), "EDI" },
                { (Instruction.Register.RegisterName.RDI, Instruction.Register.RegisterSize._16Bits), "DI" },
                { (Instruction.Register.RegisterName.RDI, Instruction.Register.RegisterSize._8Bits), "DIL" },

                { (Instruction.Register.RegisterName.RSP, Instruction.Register.RegisterSize._64Bits), "RSP" },
                { (Instruction.Register.RegisterName.RSP, Instruction.Register.RegisterSize._32Bits), "ESP" },
                { (Instruction.Register.RegisterName.RSP, Instruction.Register.RegisterSize._16Bits), "SP" },
                { (Instruction.Register.RegisterName.RSP, Instruction.Register.RegisterSize._8Bits), "SPL" },

                { (Instruction.Register.RegisterName.RBP, Instruction.Register.RegisterSize._64Bits), "RBP" },
                { (Instruction.Register.RegisterName.RBP, Instruction.Register.RegisterSize._32Bits), "EBP" },
                { (Instruction.Register.RegisterName.RBP, Instruction.Register.RegisterSize._16Bits), "BP" },
                { (Instruction.Register.RegisterName.RBP, Instruction.Register.RegisterSize._8Bits), "BPL" },

                { (Instruction.Register.RegisterName.R8, Instruction.Register.RegisterSize._64Bits), "R8" },
                { (Instruction.Register.RegisterName.R8, Instruction.Register.RegisterSize._32Bits), "R8D" },
                { (Instruction.Register.RegisterName.R8, Instruction.Register.RegisterSize._16Bits), "R8W" },
                { (Instruction.Register.RegisterName.R8, Instruction.Register.RegisterSize._8Bits), "R8B" },

                { (Instruction.Register.RegisterName.R9, Instruction.Register.RegisterSize._64Bits), "R9" },
                { (Instruction.Register.RegisterName.R9, Instruction.Register.RegisterSize._32Bits), "R9D" },
                { (Instruction.Register.RegisterName.R9, Instruction.Register.RegisterSize._16Bits), "R9W" },
                { (Instruction.Register.RegisterName.R9, Instruction.Register.RegisterSize._8Bits), "R9B" },

                { (Instruction.Register.RegisterName.R10, Instruction.Register.RegisterSize._64Bits), "R10" },
                { (Instruction.Register.RegisterName.R10, Instruction.Register.RegisterSize._32Bits), "R10D" },
                { (Instruction.Register.RegisterName.R10, Instruction.Register.RegisterSize._16Bits), "R10W" },
                { (Instruction.Register.RegisterName.R10, Instruction.Register.RegisterSize._8Bits), "R10B" },

                { (Instruction.Register.RegisterName.R11, Instruction.Register.RegisterSize._64Bits), "R11" },
                { (Instruction.Register.RegisterName.R11, Instruction.Register.RegisterSize._32Bits), "R11D" },
                { (Instruction.Register.RegisterName.R11, Instruction.Register.RegisterSize._16Bits), "R11W" },
                { (Instruction.Register.RegisterName.R11, Instruction.Register.RegisterSize._8Bits), "R11B" },

                { (Instruction.Register.RegisterName.R12, Instruction.Register.RegisterSize._64Bits), "R12" },
                { (Instruction.Register.RegisterName.R12, Instruction.Register.RegisterSize._32Bits), "R12D" },
                { (Instruction.Register.RegisterName.R12, Instruction.Register.RegisterSize._16Bits), "R12W" },
                { (Instruction.Register.RegisterName.R12, Instruction.Register.RegisterSize._8Bits), "R12B" },

                { (Instruction.Register.RegisterName.R13, Instruction.Register.RegisterSize._64Bits), "R13" },
                { (Instruction.Register.RegisterName.R13, Instruction.Register.RegisterSize._32Bits), "R13D" },
                { (Instruction.Register.RegisterName.R13, Instruction.Register.RegisterSize._16Bits), "R13W" },
                { (Instruction.Register.RegisterName.R13, Instruction.Register.RegisterSize._8Bits), "R13B" },

                { (Instruction.Register.RegisterName.R14, Instruction.Register.RegisterSize._64Bits), "R14" },
                { (Instruction.Register.RegisterName.R14, Instruction.Register.RegisterSize._32Bits), "R14D" },
                { (Instruction.Register.RegisterName.R14, Instruction.Register.RegisterSize._16Bits), "R14W" },
                { (Instruction.Register.RegisterName.R14, Instruction.Register.RegisterSize._8Bits), "R14B" },

                { (Instruction.Register.RegisterName.R15, Instruction.Register.RegisterSize._64Bits), "R15" },
                { (Instruction.Register.RegisterName.R15, Instruction.Register.RegisterSize._32Bits), "R15D" },
                { (Instruction.Register.RegisterName.R15, Instruction.Register.RegisterSize._16Bits), "R15W" },
                { (Instruction.Register.RegisterName.R15, Instruction.Register.RegisterSize._8Bits), "R15B" },
            };
        }

        partial class GasSyntax : ISyntaxFactory, Instruction.IVisitor
        {
            public GasSyntax()
            {
                header = new Instruction.Comment("Raze Compiler Version ALPHA 0.0.0 Intel_x86-64 GAS");
            }
            public override List<Instruction> GenerateHeaderInstructions(Expr.Function main){
                return new List<Instruction>()
                {
                    { new Instruction.Global("_start") },
                    { new Instruction.Section("text") },
                    { new Instruction.Procedure("_start") },
                    { new Instruction.Unary("CALL", new Instruction.ProcedureRef(Assembler.ToMangedName(main))) },
                    { new Instruction.Binary("MOV", new Instruction.Register(Instruction.Register.RegisterName.RDI, Instruction.Register.RegisterSize._64Bits), (Analyzer.TypeCheckPass.literalTypes[Parser.Literals[0]].Matches(main._returnType.type)) ? new Instruction.Register(Instruction.Register.RegisterName.RAX, Instruction.Register.RegisterSize._64Bits) : new Instruction.Literal(Parser.Literals[0], "0")) },
                    { new Instruction.Binary("MOV", new Instruction.Register(Instruction.Register.RegisterName.RAX, Instruction.Register.RegisterSize._64Bits), new Instruction.Literal(Parser.Literals[0], "60")) },
                    { new Instruction.Zero("SYSCALL") }
                };
            }

            public override void Run(List<Instruction> instructions)
            {
                foreach (var instruction in instructions)
                {
                    instruction.Accept(this);
                    Console.WriteLine();
                }
            }

            public override void Run(Instruction instruction)
            {
                instruction.Accept(this);
            }

            public string visitBinary(Instruction.Binary instruction)
            {
                throw new NotImplementedException();
            }

            public string visitProcedure(Instruction.Procedure instruction)
            {
                throw new NotImplementedException();
            }

            public string visitLocalProcedure(Instruction.LocalProcedure instruction)
            {
                throw new NotImplementedException();
            }

            public string visitDataRef(Instruction.DataRef instruction)
            {
                throw new NotImplementedException();
            }

            public string visitComment(Instruction.Comment instruction)
            {
                throw new NotImplementedException();
            }

            public string visitData(Instruction.Data instruction)
            {
                throw new NotImplementedException();
            }

            public string visitGlobal(Instruction.Global instruction)
            {
                throw new NotImplementedException();
            }

            public string visitPointer(Instruction.Pointer instruction)
            {
                throw new NotImplementedException();
            }

            public string visitRegister(Instruction.Register instruction)
            {
                throw new NotImplementedException();
            }

            public string visitSection(Instruction.Section instruction)
            {
                throw new NotImplementedException();
            }

            public string visitUnary(Instruction.Unary instruction)
            {
                throw new NotImplementedException();
            }

            public string visitZero(Instruction.Zero instruction)
            {
                throw new NotImplementedException();
            }

            public string visitProcedureRef(Instruction.ProcedureRef instruction)
            {
                throw new NotImplementedException();
            }

            public string visitLocalProcedureRef(Instruction.LocalProcedureRef instruction)
            {
                throw new NotImplementedException();
            }

            public string visitLiteral(Instruction.Literal instruction)
            {
                throw new NotImplementedException();
            }
        }
    }
}
