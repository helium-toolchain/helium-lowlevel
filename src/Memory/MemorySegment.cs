namespace Helium.LowLevel.Memory;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>
/// Represents a segment of memory, with a reference to the next segment in the chain.
/// Obtained from a memory allocator.
/// </summary>
internal unsafe ref struct MemorySegment
{
    // a pointer to the memory region this represents. must not be null.
    // this could just as well be represented as a span, but that makes MemorySegment* quite worrying
    private readonly void* ptr;
    private readonly UIntPtr length;

    public MemorySegment* NextSegment { get; set; }

    public Boolean HasNextSegment()
    {
        return this.NextSegment == null;
    }

    // exposes the memory as span
    public Span<Byte> Span
    {
        get => MemoryMarshal.CreateSpan
        (
            ref Unsafe.AsRef<Byte>(ptr),
            (Int32)length
        );
    }
}
