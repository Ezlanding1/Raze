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

        internal Encoding GetEncoding(AssemblyExpr.Binary binary, Assembler assembler)
        {
            var encoding1 = binary.operand1.ToAssemblerOperand();
            var encoding2 = binary.operand2.ToAssemblerOperand();

            if (instructionEncodings.TryGetValue(binary.instruction.ToString(), out var encodings))
            {
                foreach (Encoding encoding in encodings)
                {
                    if (encoding.Matches(encoding1, encoding2) && encoding.SpecialMatch(binary, encoding1, encoding2))
                    {
                        return encoding;
                    }
                }
            }
            Diagnostics.errors.Push(new Error.ImpossibleError("Invalid/Unsupported Instruction"));
            return new();
        }
        internal Encoding GetEncoding(AssemblyExpr.Unary unary, Assembler assembler, out bool refResolveType)
        {
            var encoding1 = unary.operand.ToAssemblerOperand();

            if (instructionEncodings.TryGetValue(unary.instruction.ToString(), out var encodings))
            {
                if (encoding1.type == Operand.OperandType.IMM && EncodingUtils.IsReferenceLiteralType(((AssemblyExpr.LabelLiteral)unary.operand).type))
                {
                    refResolveType = true;
                    (Operand.OperandSize absoluteJump, Operand.OperandSize relativeJump) = EncodingUtils.HandleUnresolvedRef(unary, unary.operand, assembler);

                    foreach (Encoding encoding in encodings)
                    {
                        encoding1.size = encoding.encodingType.HasFlag(Encoding.EncodingTypes.RelativeJump) ? relativeJump : absoluteJump;

                        if (encoding.Matches(encoding1) && encoding.SpecialMatch(unary, encoding1))
                        {
                            return encoding;
                        }
                    }
                }
                else
                {
                    refResolveType = false;
                    foreach (Encoding encoding in encodings)
                    {
                        if (encoding.Matches(encoding1) && encoding.SpecialMatch(unary, encoding1))
                        {
                            return encoding;
                        }
                    }
                }
            }
            Diagnostics.errors.Push(new Error.ImpossibleError("Invalid/Unsupported Instruction"));
            refResolveType = false;
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
