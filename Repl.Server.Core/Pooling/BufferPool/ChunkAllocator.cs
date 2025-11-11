using System.Collections.Concurrent;
using System.Diagnostics;

namespace Repl.Server.Core.Pooling.BufferPool;

internal sealed class ChunkAllocator : IDisposable
{
    private readonly int bufferSize;
    private readonly byte[] chunkBuffer;
    private readonly ConcurrentStack<ArraySegment<byte>> freeSegments;
        
    public ChunkAllocator(int bufferSize, int totalChunkSize)
    {
        this.bufferSize = bufferSize;
        int totalSegments = totalChunkSize / bufferSize;
            
        chunkBuffer = GC.AllocateArray<byte>(totalChunkSize);
        Debug.Assert(GC.GetGeneration(chunkBuffer) == GC.MaxGeneration, "Chunk must be in LOH");
            
        this.freeSegments = new ConcurrentStack<ArraySegment<byte>>();
        for (int i = 0; i < totalSegments; i++)
        {
            var segment = new ArraySegment<byte>(chunkBuffer, i * this.bufferSize, this.bufferSize);
            this.freeSegments.Push(segment);
        }
    }
        
    public ArraySegment<byte> Rent()
    {
        if (freeSegments.TryPop(out var segment) == true)
        {
            return segment;
        }

        // Pool is exhausted - fallback to SOH allocation
        // TODO better to make chunkBuffer byte[] -> List<byte[]> so additional chunk can be added at runtime?`
        return new ArraySegment<byte>(new byte[bufferSize], 0, bufferSize);
    }
        
    public void Return(ArraySegment<byte> segment)
    {
        if (segment.Array == this.chunkBuffer && segment.Count == this.bufferSize)
        {
            this.freeSegments.Push(segment);
        }
    }
        
    public void Dispose()
    {
        freeSegments.Clear();
    }
}