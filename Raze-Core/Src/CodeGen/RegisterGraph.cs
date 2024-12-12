using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class CodeGen : Expr.IVisitor<AssemblyExpr.IValue?>
{
    static class StateUtils
    {
        public static AssemblyExpr.Register.RegisterName[] registers = InstructionUtils.storageRegisters[..InstructionUtils.SseRegisterOffset];
        public static AssemblyExpr.Register.RegisterName[] sseRegisters = InstructionUtils.storageRegisters[InstructionUtils.SseRegisterOffset..];
        public static AssemblyExpr.Register.RegisterName[] NonVolatileRegisters(CallingConvention cconv) => cconv.nonVolatileRegisters;
    }

    class Node
    {
        public enum NodePriority : byte
        {
            High = 0,
            Moderate = 1,
            Low = byte.MaxValue
        }
        // Lower value = higher priority
        public NodePriority Priority { get; private set; } = NodePriority.Low;
        public bool Alive { get; private set; } = true;
        public int Locked { get; private set; } = 0;
        public bool CanSpillToStack { get; private set; } = true;
        public AssemblyExpr.Register.RegisterName[] State { get; private set; } = StateUtils.registers;
        
        public readonly AssemblyExpr.Register register = new(AssemblyExpr.Register.RegisterName.TMP, 0);
        public readonly List<Node> connections = new();

        public void Free() =>
            Alive = !(Locked == 0);

        public void Lock() =>
            Locked++;

        public void Unlock() =>
            Locked = Math.Max(0, Locked - 1);

        public void SetState(AssemblyExpr.Register.RegisterName[] state) => 
            this.State = state;

        public void SetState(AssemblyExpr.Register.RegisterName[] state, NodePriority priority)
        {
            SetState(state);
            Priority = priority;
        }

        public void SetState(AssemblyExpr.Register.RegisterName[] state, NodePriority priority, bool canSpillToStack)
        {
            SetState(state);
            Priority = priority;
            CanSpillToStack = canSpillToStack;
        }

        // If when setting a suggested register, the register allocation algorithm will try to match the suggested register first before defualting to state
        // Registers with suggested values also have a higher priority than normal registers, so they can be resolved first
        public void SetSuggestedRegister(AssemblyExpr.Register.RegisterName suggested)
        {
            SetState([suggested, .. State]);
            Priority = NodePriority.Moderate;
        }
    }

    partial class RegisterGraph
    {
        public int Count => adj.Count;
        private List<Node> adj = new();

        public Node AllocateNode()
        {
            Node newNode = new();

            foreach (Node node in GetAliveNodes())
            {
                AddEdge(node, newNode);
            }
            adj.Add(newNode);

            return newNode;
        }

        private void AddEdge(Node v, Node w)
        {
            v.connections.Add(w);
            w.connections.Add(v);
        }

        public IEnumerable<Node> GetAliveNodes()
        {
            foreach (Node node in adj)
            {
                if (node.Alive)
                    yield return node;
            }
        }

        public void Print()
        {
            foreach (Node node in adj)
            {
                Console.WriteLine($"NODE {node.GetHashCode()}: " + string.Join(", ", node.connections.Select(x => x.GetHashCode())));
            }
        }

        public Node? GetNodeForRegister(AssemblyExpr.Register reg)
        {
            return GetAliveNodes().Where(x => x.register == reg).FirstOrDefault();
        }
    }
}
