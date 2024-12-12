using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class CodeGen : Expr.IVisitor<AssemblyExpr.IValue?>
{
    partial class RegisterGraph
    {
        public Dictionary<AssemblyExpr.IValue, AssemblyExpr.Pointer?> ColorGraph()
        {
            Dictionary<Node, int> color = new();

            for (int i = 0; i < Count; i++)
            {
                color[adj[i]] = -1;
            }

            int numberOfRegisters = InstructionUtils.storageRegisters.Length;

            foreach (Node node in adj.OrderBy(x => x.Priority))
            {
                bool[] used = new bool[numberOfRegisters];

                foreach (Node connection in node.connections)
                {
                    if (color[connection] != -1)
                    {
                        used[color[connection]] = true;
                    }
                }


                foreach (var reg in node.State)
                {
                    int idx = InstructionUtils.NameToIdx(reg);
                    if (!used[idx])
                    {
                        color[node] = idx;
                        break;
                    }
                }

                if (color[node] == -1 && node.CanSpillToStack)
                {
                    color[node] = Array.IndexOf(used, false, InstructionUtils.storageRegisters.Length);
                    if (color[node] == -1)
                    {
                        color[node] = numberOfRegisters++;
                    }
                }
            }

            if (color.Any(x => x.Value == -1))
            {
                Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("Impossible program, cannot allocate registers"));
            }

            Dictionary<AssemblyExpr.IValue, AssemblyExpr.Pointer?> stackSpill = new();

            foreach (Node node in color.Keys)
            {
                if (color[node] < InstructionUtils.storageRegisters.Length)
                {
                    node.register.name = InstructionUtils.storageRegisters[color[node]];
                }
                else
                {
                    stackSpill[node.register] = null;
                }
            }

            return stackSpill;
        }
    }
}
