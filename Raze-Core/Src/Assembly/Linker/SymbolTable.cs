using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Linker
{
    internal class SymbolTable
    {
        internal List<RefOrDefSymbolTableInfo> unresolvedReferences = new();
        internal int sTableUnresRefIdx = -1;

        internal Dictionary<string, int> definitions = new();

        internal List<AssemblyExpr.Include> includes = [];
    }

    internal abstract class RefOrDefSymbolTableInfo
    {
        // true = reference, false = definition
        public readonly bool reference; 

        public RefOrDefSymbolTableInfo(bool reference)
        {
            this.reference = reference;
        }
    }

    internal class DefinitionInfo : RefOrDefSymbolTableInfo
    {
        public string refName;
        public bool textSection;

        public DefinitionInfo(string refName, bool textSection) : base(false)
        {
            this.refName = refName;
            this.textSection = textSection;

        }
    }

    internal class ReferenceInfo : RefOrDefSymbolTableInfo
    {
        public AssemblyExpr instruction;
        public int location;
        public int size;
        public bool textSection;
        public bool absoluteAddress = true;
        public int dataSize;

        public ReferenceInfo(AssemblyExpr instruction, int location, int size, bool textSection, int dataSize) : base(true)
        {
            this.instruction = instruction;
            this.location = location;
            this.size = size;
            this.textSection = textSection;
            this.dataSize = dataSize;
        }
        public ReferenceInfo(AssemblyExpr instruction, int location, int size, bool textSection) : this(instruction, location, size, textSection, (int)InstructionUtils.SYS_SIZE)
        {
        }
    }
}
