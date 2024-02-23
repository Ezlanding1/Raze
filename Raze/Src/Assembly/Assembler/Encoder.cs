using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Raze;

public partial class Assembler
{
    public partial class Encoder
    {
        Dictionary<string, List<Encoding>> instructionEncodings;

        internal Encoder() 
        {
            string path = Path.Join(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), "EncodingSchema.json");

            if (File.Exists(path)) 
            {
                this.instructionEncodings = JsonSerializer.Deserialize<Dictionary<string, List<Encoding>>>(File.ReadAllText(path));
            }
            else
            {
                Diagnostics.errors.Push(new Error.ImpossibleError($"Could not locate Encoding Schema file. Path: '{path}'"));
            }
        }

        internal Encoding GetEncoding(AssemblyExpr.OperandInstruction instruction, Assembler assembler, out bool refResolve)
        {
            Operand[] operands = instruction.Operands.Select(x => x.ToAssemblerOperand()).ToArray();

            if (instructionEncodings.TryGetValue(instruction.instruction.ToString(), out var encodings))
            {
                if (operands[^1].type == Operand.OperandType.IMM && EncodingUtils.IsReferenceLiteralType(((AssemblyExpr.Literal)instruction.Operands[^1]).type))
                {
                    refResolve = true;
                    (Operand.OperandSize absoluteJump, Operand.OperandSize relativeJump) = 
                        EncodingUtils.HandleUnresolvedRef(instruction, (AssemblyExpr.LabelLiteral)instruction.Operands[^1], assembler);

                    foreach (Encoding encoding in encodings)
                    {
                        operands[^1].size = encoding.encodingType.HasFlag(Encoding.EncodingTypes.RelativeJump) ? relativeJump : absoluteJump;

                        if (encoding.Matches(operands) && encoding.SpecialMatch(instruction, operands))
                        {
                            return encoding;
                        }
                    }
                }
                else
                {
                    refResolve = false;
                    foreach (Encoding encoding in encodings)
                    {
                        if (encoding.Matches(operands) && encoding.SpecialMatch(instruction, operands))
                        {
                            return encoding;
                        }
                    }
                }
            }
            Diagnostics.errors.Push(new Error.ImpossibleError("Invalid/Unsupported Instruction"));
            refResolve = false;
            return new();
        }
        internal Encoding GetEncoding(AssemblyExpr.Zero zero)
        {
            if (instructionEncodings.TryGetValue(zero.instruction.ToString(), out var encodings))
            {
                if (encodings.Count != 0)
                {
                    return encodings[0];
                }
            }
            Diagnostics.errors.Push(new Error.ImpossibleError("Invalid/Unsupported Instruction"));
            return new();
        }
    }
}
