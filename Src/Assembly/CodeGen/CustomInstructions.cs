using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

internal abstract class CustomInstructions
{
    internal class StackAlloc : Instruction.CustomInstruction
    {
        Literal literal;

        public StackAlloc(int allocSize)
        {
            literal = new(Token.TokenType.INTEGER, AlignTo16(allocSize).ToString());
        }

        public override string GetInstruction(IVisitor visitor)
        {
            return new Binary("SUB", new Register(Register.RegisterName.RSP, Register.RegisterSize._64Bits), literal).Accept(visitor);
        }

        private string AlignTo16(int i)
        {
            return (((int)Math.Ceiling(i / 16f)) * 16).ToString();
        }
    }

    internal class FunctionPushPreserved : Instruction.CustomInstruction
    {
        public bool leaf = true;
        public int size;
        bool pushed = false;
        bool[] registers = new bool[5];

        public FunctionPushPreserved(int size)
        {
            this.size = size;
        }

        public void IncludeRegister(int idx) => registers[idx-1] = true;

        public override string GetInstruction(IVisitor visitor)
        {
            var sb = new StringBuilder();

            if (!pushed)
            {
                GenerateHeader(visitor, sb);

                for (int i = 0; i < registers.Length; i++)
                {
                    if (registers[i]) 
                        sb.AppendLine(new Unary("PUSH", new Register(InstructionUtils.storageRegisters[i + 1], Register.RegisterSize._64Bits)).Accept(visitor));
                }

                pushed = true;
            }
            else
            {
                for (int i = registers.Length - 1; i >= 0; i--)
                {
                    if (registers[i])
                        sb.AppendLine(new Unary("POP", new Register(InstructionUtils.storageRegisters[i + 1], Register.RegisterSize._64Bits)).Accept(visitor));
                }

                GenerateFooter(visitor, sb);
            }

            return sb.Remove(sb.Length - 1, 1).ToString();
        }

        private void GenerateHeader(IVisitor visitor, StringBuilder sb)
        {
            sb.AppendLine(new Unary("PUSH", Register.RegisterName.RBP).Accept(visitor));
            sb.AppendLine(new Binary("MOV", Register.RegisterName.RBP, Register.RegisterName.RSP).Accept(visitor));

            if (!((leaf || size == 0) && size <= 128))
            {
                sb.AppendLine(new StackAlloc((size > 128) ? size - 128 : size).Accept(visitor));
            }
        }
        
        private void GenerateFooter(IVisitor visitor, StringBuilder sb)
        {
            sb.AppendLine(
                leaf ?
                new Unary("POP", Register.RegisterName.RBP).Accept(visitor) : 
                new Zero("LEAVE").Accept(visitor)
            );

            sb.AppendLine(new Zero("RET").Accept(visitor));
        }
    }
}
