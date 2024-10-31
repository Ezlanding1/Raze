using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Linker
{
    internal partial class Elf64
    {
        // Code adapted from Elf-64 specification outlined here: https://uclibc.org/docs/elf-64-gen.pdf

        public static ReadOnlySpan<byte> ToReadOnlySpan<T>(ref readonly T t) where T : unmanaged
        {
            return MemoryMarshal.AsBytes(new ReadOnlySpan<T>(in t));
        }

        public static ReadOnlySpan<byte> ToReadOnlySpan<T>(T[] t) where T : unmanaged
        {
            return MemoryMarshal.AsBytes((ReadOnlySpan<T>)t);
        }
    }
}