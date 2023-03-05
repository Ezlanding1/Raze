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
                        { new Instruction.Function("_start") },
                        { new Instruction.Unary("CALL", main.QualifiedName) },
                        { new Instruction.Binary("MOV", "RDI", (main._returnType == "number") ? "RAX" : "0") },
                        { new Instruction.Binary("MOV", "RAX", "60") },
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
                        if (instruction.operand2 is Instruction.Register)
                            return $"{instruction.instruction}\t{instruction.operand1.Accept(this)}, { AlignTo16((Instruction.Register)instruction.operand2) }";
                        else
                            throw new Exception();
                    if (instruction.operand2 is Instruction.Pointer)
                        if (instruction.operand1 is Instruction.Pointer)
                            return new Instruction.Binary("MOV", new Instruction.Register(RegisterFor(((Instruction.Pointer)instruction.operand2), "RAX")), instruction.operand2).Accept(this) + "\n" + new Instruction.Binary("MOV", instruction.operand1, new Instruction.Register(RegisterFor(((Instruction.Pointer)instruction.operand1), "RAX"))).Accept(this);
                        else
                            return $"{instruction.instruction}\t{instruction.operand1.Accept(this)}, { PointerToString( (Instruction.Pointer)instruction.operand2 ) }";

                    return $"{instruction.instruction}\t{instruction.operand1.Accept(this)}, {instruction.operand2.Accept(this)}";
                }

                public string visitClass(Instruction.Class instruction)
                {
                    return $"{instruction.name}:";
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

                public string visitFunction(Instruction.Function instruction)
                {
                    return $"{instruction.name}:";
                }

                public string visitGlobal(Instruction.Global instruction)
                {
                    return $"global {instruction.name}\n";
                }

                public string visitPointer(Instruction.Pointer instruction)
                {
                    return $"{InstructionInfo.wordSize[(int)instruction.size]} [{instruction.name}{( (instruction.offset != 0)? $"-{(instruction.offset)}" : "" )}]";
                }

                public string visitFunctionRef(Instruction.FunctionRef instruction)
                {
                    return $"{instruction.name}";
                }

                public string visitRegister(Instruction.Register instruction)
                {
                    return $"{instruction.name}";
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
                    return $"[{instruction.name}{( (instruction.offset != 0)? $"-{(instruction.offset)}" : "" )}]";
                }

                private string AlignTo16(Instruction.Register instruction)
                {
                    if (int.TryParse(instruction.name, out var value)) 
                    {
                        string name = (((int)Math.Ceiling(value / 16f)) * 16).ToString();
                        return new Instruction.Register(name).Accept(this);
                    }
                    else
                    {
                        throw new Errors.ImpossibleError("Size of aligned literal is not numeric");
                    }
                }

                private string RegisterFor(Instruction.Pointer pointer, string register) => InstructionInfo.Registers[(register, pointer.size)];
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
                        { new Instruction.Function("main") },
                        { new Instruction.Unary("CALL", main.QualifiedName) },
                        { new Instruction.Binary("MOV", "RDI", (main._returnType == "number")? "RAX" : "0") },
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

                public string visitClass(Instruction.Class instruction)
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

                public string visitFunction(Instruction.Function instruction)
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

                public string visitFunctionRef(Instruction.FunctionRef instruction)
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
            }
        }
    }
}
