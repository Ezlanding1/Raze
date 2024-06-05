using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Raze.Src.Assembly.Assembler.Resources.EncodingSchema.json");

            if (stream is null)
            {
                Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic($"Could not locate Encoding Schema file"));
            }

            instructionEncodings = JsonSerializer.Deserialize<Dictionary<string, List<Encoding>>>(stream);
        }

        internal Encoding GetEncoding(AssemblyExpr.OperandInstruction instruction, Assembler assembler, out bool refResolve)
        {
            Operand[] operands = instruction.Operands.Select(x => x.ToAssemblerOperand()).ToArray();
            
            if (instructionEncodings.TryGetValue(instruction.instruction.ToString(), out var encodings))
            {
                if (EncodingUtils.IsReferenceLiteralOperand(operands[^1], instruction.Operands[^1], out var labelLiteral))
                {
                    refResolve = true;
                    (Operand.OperandSize absoluteJump, Operand.OperandSize relativeJump) = 
                        EncodingUtils.HandleUnresolvedRef(instruction, labelLiteral, operands[^1].size, assembler);

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
            throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("Invalid/Unsupported Instruction"));
        }
        internal Encoding GetEncoding(AssemblyExpr.Nullary zero)
        {
            if (instructionEncodings.TryGetValue(zero.instruction.ToString(), out var encodings))
            {
                if (encodings.Count != 0)
                {
                    return encodings[0];
                }
            }
            throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("Invalid/Unsupported Instruction"));
        }
    }
}
