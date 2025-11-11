namespace Repl.Server.Core.Pooling.BufferPool;

public readonly struct LohSegment : IDisposable
{
    private readonly LohBufferPool pool;
    private readonly ChunkAllocator allocator;
        
    public ArraySegment<byte> Segment { get; }
    public byte[] Array => this.Segment.Array!;
    public int Count => this.Segment.Count;
        
    internal LohSegment(LohBufferPool pool, ArraySegment<byte> segment, ChunkAllocator allocator)
    {
        this.pool = pool;
        this.Segment = segment;
        this.allocator = allocator;
    }
        
    public void Dispose()
    {
        this.pool.Return(Segment, allocator, false);
    }
        
    public void Return(bool clearBuffer = false)
    {
        this.pool.Return(Segment, allocator, clearBuffer);
    }
}