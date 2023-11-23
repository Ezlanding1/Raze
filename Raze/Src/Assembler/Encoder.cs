using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Raze;

public partial class Assembler
{
    internal partial class Encoder
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

        public Instruction Encode(AssemblyExpr.Binary binary)
        {
            var encoding1 = Encoding.ToEncodingType(binary.operand1);
            var encoding2 = Encoding.ToEncodingType(binary.operand2);

            if (instructionEncodings.TryGetValue(binary.instruction, out var encodings))
            {
                foreach (Encoding encoding in encodings)
                {
                    if (encoding.Matches(encoding1, encoding2))
                    {
                        return encoding.GenerateInstruction(encoding1, encoding2, binary.operand1, binary.operand2);
                    }
                }
            }

            Diagnostics.errors.Push(new Error.ImpossibleError("Invalid/Unsupported Instruction"));
            return new Instruction();
        }
    }
}
