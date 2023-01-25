namespace Helium.LowLevel.Memory;

using System;
using System.Buffers;

/// <summary>
/// This place consists of evil unsafe hacks and of evil unsafe hacks only.
/// Proceed at your own responsibility.
/// <br/> <br/>
/// This is a wrapper around multiple memory pages and presents them to the caller as if they were contiguous memory.
/// This allows "in-place" resizing without actually incurring memory copies.
/// </summary>
/// <remarks>
/// This type is highly volatile in its usage. It is imperative to never break the safety rules lined out here:
/// <br/> <br/>
/// - New instances must be created through <seealso cref="Allocate"/>. <br/>
/// - Instances no longer used must be freed through <seealso cref="Free"/>. <br/>
/// - Resizes must be performed through <seealso cref="Resize"/>. <br/>
/// - It is <b>not</b> allowed to return this type, ever. All instances should be created by the caller and passed
/// to the callee via the argument list, in much the same way as regular spans are supposed to be used.
/// </remarks>
internal unsafe ref struct PagedResizableSpan<T>
    where T : unmanaged
{
    private IntPtr[] underlyingMemory;
    private Int32 currentlyAllocatedPages;
    private Int32 currentlyAllocatedLength;
    private Int32 currentlyUsedLength;

    // ensures that the rented array can contain references to enough memory pages after the next increase in size.
    // arrays returned by ArrayPool<T>.Shared.Rent can exceed the size specified, so we should not be keeping track
    // of ArrayPool rent size and instead just use the Length property on the returned array, as performed here.
    private void ensureUnderlyingMemoryPointerArraySize
    (
        Int32 additionalPages = 0
    )
    {
        Int32 neededPages = currentlyAllocatedPages + additionalPages;

        if(underlyingMemory.Length >= neededPages)
        {
            return;
        }

        IntPtr[] newArray = ArrayPool<IntPtr>.Shared.Rent(neededPages);

        // make sure to not lose a reference here
        underlyingMemory.CopyTo(newArray, 0);
        ArrayPool<IntPtr>.Shared.Return(underlyingMemory);
    }
}
