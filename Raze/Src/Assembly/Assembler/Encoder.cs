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

        public Encoder() 
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

        public Instruction Encode(AssemblyExpr.Binary binary, Assembler assembler)
        {
            var encoding1 = Encoding.ToEncodingType(binary.operand1);
            var encoding2 = Encoding.ToEncodingType(binary.operand2);

            if (instructionEncodings.TryGetValue(binary.instruction.ToString(), out var encodings))
            {
                foreach (Encoding encoding in encodings)
                {
                    if (encoding.Matches(encoding1, encoding2) && encoding.SpecialMatch(new Operand[] { encoding1, encoding2 },  binary))
                    {
                        return encoding.GenerateInstruction(encoding1, encoding2, binary.operand1, binary.operand2, assembler);
                    }
                }
            }

            Diagnostics.errors.Push(new Error.ImpossibleError("Invalid/Unsupported Instruction"));
            return new Instruction();
        }

        public Instruction EncodeData(AssemblyExpr.Data data, Assembler assembler)
        {
            assembler.symbolTable.data[data.name] = assembler.location;
            return new Instruction(new IInstruction[] { data.value.Item1 switch
            {
                Parser.LiteralTokenType.BINARY or
                Parser.LiteralTokenType.HEX or
                Parser.LiteralTokenType.FLOATING or
                Parser.LiteralTokenType.BOOLEAN or
                Parser.LiteralTokenType.INTEGER => EncodingUtils.GetImmInstruction((Operand.OperandSize)(byte)data.size, new(data.value.Item1, data.value.Item2)),
                Parser.LiteralTokenType.REF_STRING or Parser.LiteralTokenType.STRING => new Instruction.RawInstruction(System.Text.Encoding.ASCII.GetBytes(data.value.Item2)),
                _ => EncodingUtils.EncodingError().Instructions[0]
            }});
        }
    }
}
