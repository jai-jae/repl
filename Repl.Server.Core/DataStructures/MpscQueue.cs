/// <summary>
/// Fixed-size Thread-Safe Multiple Producer Single Consumer Queue
/// referenced implementations from System.Collections.Concurrent.ConcurrentQueue without ConcurrentQueueSegments and spinning on Consummer.
/// </summary>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Repl.Server.Core.DataStructures;

[DebuggerDisplay("Head = {Head}, Tail = {Tail}")]
[StructLayout(LayoutKind.Explicit, Size = 3 * CACHE_LINE_SIZE)] // padding before/between/after fields
internal struct PaddedHeadAndTail
{
#if TARGET_ARM64
        internal const int CACHE_LINE_SIZE = 128;
#else
    internal const int CACHE_LINE_SIZE = 64;
#endif
    [FieldOffset(1 * CACHE_LINE_SIZE)] public int Head;
    [FieldOffset(2 * CACHE_LINE_SIZE)] public int Tail;
}

public class MpscQueue<T>
{
    private Slot[] slots;
    private PaddedHeadAndTail headAndTail;
    private readonly int slotsMask;

    public int Capacity => this.slots.Length;

    public MpscQueue(int capacity)
    {
        capacity = (int)BitOperations.RoundUpToPowerOf2((uint)capacity);
        this.slots = new Slot[capacity];
        this.slotsMask = this.slots.Length - 1;
    }

    public bool TryEnqueue(T item)
    {
        Slot[] localSlots = this.slots;

        while (true)
        {
            // Get the tail at which to try to return.
            int currentTail = Volatile.Read(ref headAndTail.Tail);
            int slotsIndex = currentTail & slotsMask;

            // Read the sequence number for the tail position.
            int sequenceNumber = Volatile.Read(ref localSlots[slotsIndex].SequenceNumber);

            // The slot is empty and ready for us to enqueue into it if its sequence
            // number matches the slot.
            int diff = sequenceNumber - currentTail;
            if (diff == 0)
            {
                // We may be racing with other enqueuers.  Try to reserve the slot by incrementing
                // the tail.  Once we've done that, no one else will be able to write to this slot,
                // and no dequeuer will be able to read from this slot until we've written the new
                // sequence number. WARNING: The next few lines are not reliable on a runtime that
                // supports thread aborts. If a thread abort were to sneak in after the CompareExchange
                // but before the Volatile.Write, other threads will spin trying to access this slot.
                // If this implementation is ever used on such a platform, this if block should be
                // wrapped in a finally / prepared region.
                if (Interlocked.CompareExchange(ref headAndTail.Tail, currentTail + 1, currentTail) == currentTail)
                {
                    // Successfully reserved the slot.  Note that after the above CompareExchange, other threads
                    // trying to return will end up spinning until we do the subsequent Write.
                    localSlots[slotsIndex].Item = item;
                    Volatile.Write(ref localSlots[slotsIndex].SequenceNumber, currentTail + 1);
                    return true;
                }

                // The tail was already advanced by another thread. A newer tail has already been observed and the next
                // iteration would make forward progress, so there's no need to spin-wait before trying again.
            }
            else if (diff < 0)
            {
                // The sequence number was less than what we needed, which means this slot still
                // contains a value, i.e. the segment is full.  Technically it's possible that multiple
                // dequeuers could have read concurrently, with those getting later slots actually
                // finishing first, so there could be spaces after this one that are available, but
                // we need to enqueue in order.
                return false;
            }
            else
            {
                // Either the slot contains an item, or it is empty but because the slot was filled and dequeued. In either
                // case, the tail has already been updated beyond what was observed above, and the sequence number observed
                // above as a volatile load is more recent than the update to the tail. So, the next iteration of the loop
                // is guaranteed to see a new tail. Since this is an always-forward-progressing situation, there's no need
                // to spin-wait before trying again.
            }
        }
    }

    public bool TryDequeue([MaybeNullWhen(false)] out T item)
    {
        Slot[] localSlots = this.slots;

        // Get the head at which to try to dequeue.
        int currentHead = Volatile.Read(ref this.headAndTail.Head);
        int slotsIndex = currentHead & this.slotsMask;

        // Read the sequence number for the head position.
        int sequenceNumber = Volatile.Read(ref localSlots[slotsIndex].SequenceNumber);

        // We can dequeue from this slot if it's been filled by an enqueuer, which
        // would have left the sequence number at pos+1.
        int diff = sequenceNumber - (currentHead + 1);
        if (diff == 0)
        {

            // update head. it works because there is always single thread accessing Queue.
            // original code tries to Interlocked.CompareExchange(ref this.headAndTail.Head, currentHead + 1, currentHead) for guard against race-condition, but we don't have to!.
            Volatile.Write(ref this.headAndTail.Head, currentHead + 1);
            item = slots[slotsIndex].Item!;

            // clear item at slot
            slots[slotsIndex].Item = default;

            Volatile.Write(ref slots[slotsIndex].SequenceNumber, currentHead + slots.Length);
            return true;
        }


        item = default;
        return false;
    }

    public bool TryPeek([MaybeNullWhen(false)] out T item)
    {
        Slot[] localSlots = this.slots;

        int head = Volatile.Read(ref this.headAndTail.Head);
        int index = head & this.slotsMask;

        int seq = Volatile.Read(ref localSlots[index].SequenceNumber);
        int dif = seq - (head + 1);

        if (dif == 0)
        {
            item = localSlots[index].Item!;
            return true;
        }

        item = default;
        return false;
    }

    [StructLayout(LayoutKind.Auto)]
    [DebuggerDisplay("Item = {Item}, SequenceNumber = {SequenceNumber}")]
    private struct Slot
    {
        /// <summary>The item.</summary>
        public T? Item; // SOS's ThreadPool command depends on this being at the beginning of the struct when T is a reference type
        /// <summary>The sequence number for this slot, used to synchronize between enqueuers and dequeuers.</summary>
        public int SequenceNumber;
    }
}