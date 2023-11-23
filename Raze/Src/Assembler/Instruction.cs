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
            int len = Instructions.Length;
            for (int i = 0; i < len; ++i)
            {
                int size = Marshal.SizeOf(Instructions[i]);
                byte[] bytes = new byte[size];

                IntPtr ptr = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.StructureToPtr(Instructions[i], ptr, true);
                    Marshal.Copy(ptr, bytes, 0, size);
                }
                catch (Exception e)
                {
                    throw e;
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
                yield return bytes;
            }
        }
    }
}
