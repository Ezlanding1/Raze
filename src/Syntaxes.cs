using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze
{
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
                        { new Instruction.Unary("CALL", new Instruction.ProcedureRef(main.QualifiedName)) },
                        { new Instruction.Binary("MOV", new Instruction.Register("RDI", Instruction.Register.RegisterSize._64Bits), (main._returnType.ToString() == "number") ? new Instruction.Register("RAX", Instruction.Register.RegisterSize._64Bits) : new Instruction.Literal("0", Parser.Literals[0])) },
                        { new Instruction.Binary("MOV", new Instruction.Register("RAX", Instruction.Register.RegisterSize._64Bits), new Instruction.Literal("60", Parser.Literals[0])) },
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
                            return $"{instruction.instruction}\t{instruction.operand1.Accept(this)}, { AlignTo16((Instruction.Literal)instruction.operand2) }";
                        else
                            //
                            throw new Exception();
                    if (instruction.operand2 is Instruction.Pointer)
                            return $"{instruction.instruction}\t{instruction.operand1.Accept(this)}, { PointerToString( (Instruction.Pointer)instruction.operand2 ) }";

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

                public string visitGlobal(Instruction.Global instruction)
                {
                    return $"global {instruction.name}\n";
                }

                public string visitPointer(Instruction.Pointer instruction)
                {
                    return $"{InstructionInfo.wordSize[instruction.size]} [{InstructionInfo.Registers[(instruction.name, instruction.size)]} {instruction._operator} {instruction.offset}]";
                }

                public string visitProcedureRef(Instruction.ProcedureRef instruction)
                {
                    return $"{instruction.name}";
                }

                public string visitRegister(Instruction.Register instruction)
                {
                    return $"{InstructionInfo.Registers[(instruction.name, instruction.size)]}";
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
                    return $"[{InstructionInfo.Registers[(instruction.name, instruction.size)]} {instruction._operator} {instruction.offset}]";
                }
                
                public string visitLiteral(Instruction.Literal instruction)
                {
                    return $"{instruction.name}";
                }

                private string AlignTo16(Instruction.Literal instruction)
                {
                    if (int.TryParse(instruction.name, out var value)) 
                    {
                        string name = (((int)Math.Ceiling(value / 16f)) * 16).ToString();
                        return new Instruction.Literal(name, instruction.type).Accept(this);
                    }
                    else
                    {
                        throw new Errors.ImpossibleError("Size of aligned literal is not numeric");
                    }
                }

                
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
                        { new Instruction.Section("text") },
                        { new Instruction.Global("main") },
                        { new Instruction.Procedure("main") },
                        { new Instruction.Unary("CALL", main.QualifiedName) },
                        { new Instruction.Binary("MOV", "RDI", (main._returnType.ToString() == "number")? "RAX" : "0") },
                        { new Instruction.Binary("MOV", "RAX", "60") },
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

                public string visitLiteral(Instruction.Literal instruction)
                {
                    throw new NotImplementedException();
                }
            }
        }
    }
}
