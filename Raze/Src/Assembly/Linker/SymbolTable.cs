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

        internal Dictionary<string, int> definitions = new();
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

        public DefinitionInfo(string refName) : base(false)
        {
            this.refName = refName;
        }
    }

    internal class ReferenceInfo : RefOrDefSymbolTableInfo
    {
        public AssemblyExpr instruction;
        public int location;
        public int size;

        public ReferenceInfo(AssemblyExpr instruction, int location, int size) : base(true)
        {
            this.instruction = instruction;
            this.location = location;
            this.size = size;
        }
    }
}
