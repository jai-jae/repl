using System.Net.Sockets;

namespace Repl.Server.Core.Pooling.BufferPool;

public class LohBufferPool : IDisposable
{
    private const int MaxBufferSize = 32768; // 8KB
    private const int DefaultChunkSize = 16 * 1024 * 1024; // 16MB
    private static readonly int[] BufferSizeClasses = 
    {
        16,
        32,
        64,
        128,
        256,
        512,
        1024,
        2048,
        4096,
        8192,
        16384,
        32768,
    };

    private readonly ChunkAllocator[] allocators;
    private bool disposed;
        
    public static LohBufferPool Shared { get; } = new LohBufferPool();
        
    public LohBufferPool(int chunkSize = DefaultChunkSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(chunkSize, 85000, nameof(chunkSize));
            
        this.allocators = new ChunkAllocator[BufferSizeClasses.Length];

        for (int i = 0; i < BufferSizeClasses.Length; i++)
        {
            this.allocators[i] = new ChunkAllocator(BufferSizeClasses[i], chunkSize);
        }
    }
        
    public LohSegment Rent(int minimumSize)
    {
        ObjectDisposedException.ThrowIf(this.disposed, nameof(LohBufferPool));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(minimumSize,0, nameof(minimumSize)); 
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minimumSize, MaxBufferSize, nameof(minimumSize));
            
        ChunkAllocator allocator = this.GetAllocator(minimumSize);
        var segment = allocator.Rent();

        return new LohSegment(this, segment, allocator);
    }
        
    internal void Return(ArraySegment<byte> segment, ChunkAllocator allocator, bool clearBuffer)
    {
        if (segment.Array == null) return;

        if (clearBuffer && segment.Count > 0)
        {
            Array.Clear(segment.Array, segment.Offset, segment.Count);
        }

        allocator.Return(segment);
    }
        
    private ChunkAllocator GetAllocator(int size)
    {
        for (int i = 0; i < LohBufferPool.BufferSizeClasses.Length; i++)
        {
            if (LohBufferPool.BufferSizeClasses[i] >= size)
            {
                return this.allocators[i];
            }
        }
        
        return this.allocators[^1];
    }
        
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }
        
        this.disposed = true;
            
        foreach (var allocator in allocators)
        {
            allocator.Dispose();
        }
    }
}