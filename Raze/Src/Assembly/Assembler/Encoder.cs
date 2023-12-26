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

        internal Encoding GetEncoding(AssemblyExpr.Binary binary)
        {
            var encoding1 = binary.operand1.ToAssemblerOperand();
            var encoding2 = binary.operand2.ToAssemblerOperand();

            if (instructionEncodings.TryGetValue(binary.instruction.ToString(), out var encodings))
            {
                foreach (Encoding encoding in encodings)
                {
                    if (encoding.Matches(encoding1, encoding2) && encoding.SpecialMatch(new Operand[] { encoding1, encoding2 },  binary))
                    {
                        return encoding;
                    }
                }
            }
            Diagnostics.errors.Push(new Error.ImpossibleError("Invalid/Unsupported Instruction"));
            return new();
        }

        public Instruction EncodeData(AssemblyExpr.Data data)
        {
            return new Instruction(new IInstruction[] { data.value.Item1 switch
            {
                AssemblyExpr.Literal.LiteralType.BINARY or
                AssemblyExpr.Literal.LiteralType.HEX or
                AssemblyExpr.Literal.LiteralType.FLOATING or
                AssemblyExpr.Literal.LiteralType.BOOLEAN or
                AssemblyExpr.Literal.LiteralType.INTEGER => EncodingUtils.GetImmInstruction((Operand.OperandSize)(byte)data.size, new(data.value.Item1, data.value.Item2), null),
                AssemblyExpr.Literal.LiteralType.STRING => new Instruction.RawInstruction(System.Text.Encoding.ASCII.GetBytes(data.value.Item2)),
                _ => EncodingUtils.EncodingError().Instructions[0]
            }});
        }
    }
}
