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
    // .ELF code adapted from Elf-64 specification here: https://uclibc.org/docs/elf-64-gen.pdf
    // .EXE code adapted from PE32+ specification here: https://learn.microsoft.com/en-us/windows/win32/debug/pe-format

    public static ReadOnlySpan<byte> ToReadOnlySpan<T>(ref readonly T t) where T : unmanaged
    {
        return MemoryMarshal.AsBytes(new ReadOnlySpan<T>(in t));
    }

    public static ReadOnlySpan<byte> ToReadOnlySpan<T>(T[] t) where T : unmanaged
    {
        return MemoryMarshal.AsBytes((ReadOnlySpan<T>)t);
    }
}
