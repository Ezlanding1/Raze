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
        internal List<LabelRefOrDefSymbolTableInfo> unresolvedLabels = new();
        internal Stack<DataSymbolTableInfo> unresolvedData = new();

        internal Dictionary<string, int> labels = new();
        internal Dictionary<string, int> data = new();
    }

    internal class DataSymbolTableInfo
    {
        public readonly string dataRef;
        public int location;

        public DataSymbolTableInfo(string dataRef, int location)
        {
            this.dataRef = dataRef;
            this.location = location;
        }
    }

    internal abstract class LabelRefOrDefSymbolTableInfo
    {
        public readonly bool lblRef;

        public LabelRefOrDefSymbolTableInfo(bool lblRef)
        {
            this.lblRef = lblRef;
        }
    }

    internal class LabelDefInfo : LabelRefOrDefSymbolTableInfo
    {
        public string refName;

        public LabelDefInfo(string refName) : base(false)
        {
            this.refName = refName;
        }
    }

    internal class LabelRefInfo : LabelRefOrDefSymbolTableInfo
    {
        public AssemblyExpr instruction;
        public int location;
        public int size;

        public LabelRefInfo(AssemblyExpr instruction, int location, int size) : base(true)
        {
            this.instruction = instruction;
            this.location = location;
            this.size = size;
        }
    }
}
