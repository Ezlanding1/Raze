using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Assembler
{
    internal interface IInstruction
    {
        public byte[] GetBytes();
    }

    // Instruction (1-15 bytes)
    [StructLayout(LayoutKind.Sequential, Pack = 1)]

    public partial struct Instruction
    {
        internal IInstruction[] Instructions { get; set; }

        internal Instruction(IInstruction[] instructions)
        {
            this.Instructions = instructions;
        }

        public IEnumerable<byte[]> ToBytes()
        {
            for (int i = 0; i < Instructions.Length; i++)
            {
                yield return Instructions[i].GetBytes();
            }
        }

        internal static byte[] ToByteArrByMarshal(IInstruction instruction)
        {
            int size = Marshal.SizeOf(instruction);
            byte[] bytes = new byte[size];

            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(instruction, ptr, true);
                Marshal.Copy(ptr, bytes, 0, size);
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            return bytes;
        }
    }
}
