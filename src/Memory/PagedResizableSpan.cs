namespace Helium.LowLevel.Memory;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
[SkipLocalsInit]
internal unsafe ref struct PagedResizableSpan<T>
    where T : unmanaged
{
    private UIntPtr[] underlyingMemory;
    private Int32 currentlyAllocatedPages;
    private Int32 currentlyAllocatedLength;
    private Int32 currentlyUsedLength;

    // ensures that sufficient memory for the next operation(s) is available and allocated, potentially allocating
    // more if need be. this will always allocate in multiples of the system page size, and continuity is not
    // guaranteed for anything beyond individual pages.
    private void ensureMemoryAvailability
    (
        Int32 additionalMemory = 0
    )
    {
        // all of this is in a checked context because we need to make sure all numbers stay within the Int32 limit.
        // if we exceed this limit, anything might happen, so it's best to throw an exception before any memory is
        // corrupted or a segmentation fault occurs.
        checked
        {
            Int32 totalMemory = currentlyUsedLength + additionalMemory;

            if(currentlyAllocatedLength >= totalMemory)
            {
                return;
            }

            // calculate the amount of additional pages needed and validate whether we have space for them, 
            // if not, resize the backing page reference array.
            Int32 additionalPages = (totalMemory - currentlyAllocatedLength) / Environment.SystemPageSize;
            this.ensureUnderlyingMemoryPointerArraySize(additionalPages);

            // allocate the necessary pages and update the struct values
            for(Int32 i = 0; i < additionalPages; i++)
            {
                // here, we switch back to unchecked because nothing in here needs it, and we might as well pick up
                // the handful of nanoseconds that would otherwise be used on checking overflows.
                unchecked
                {
                    // this doesn't strictly make a guarantee that it will be a perfect memory page, but practically,
                    // unless we're running out of system memory, it will be a memory page on any supported OS.
                    void* page = NativeMemory.Alloc
                    (
                        // unchecked because we know this will always fit UIntPtr, so there's no need to take
                        // the additional check hitting performance.
                        (UIntPtr)Environment.SystemPageSize
                    );

                    underlyingMemory[currentlyAllocatedPages + i] = (UIntPtr)page;
                }
            }

            // do these operations back in a checked context, and outside the loop - imul;add is in the general
            // case preferable to several add instructions
            currentlyAllocatedPages += additionalPages;
            currentlyAllocatedLength += additionalPages * Environment.SystemPageSize;
        }
    }

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

        UIntPtr[] newArray = ArrayPool<UIntPtr>.Shared.Rent(neededPages);

        // make sure to not lose a reference here
        underlyingMemory.CopyTo(newArray, 0);
        ArrayPool<UIntPtr>.Shared.Return(underlyingMemory);

        underlyingMemory = newArray;
    }
}
