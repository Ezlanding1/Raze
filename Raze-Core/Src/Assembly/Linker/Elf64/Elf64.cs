using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Linker
{
    internal partial class Elf64
    {
        // Code adapted from Elf-64 specification outlined here: https://uclibc.org/docs/elf-64-gen.pdf

        public unsafe static ReadOnlySpan<byte> ToReadOnlySpan<T>(T t) where T : struct
        {
            return new ReadOnlySpan<byte>(Unsafe.AsPointer(ref t), sizeof(T));
        }

        public unsafe static ReadOnlySpan<byte> ToReadOnlySpan<T>(T[] t) where T : struct
        {
            return new ReadOnlySpan<byte>(Unsafe.AsPointer(ref t[0]), sizeof(T) * t.Length);
        }
    }
}