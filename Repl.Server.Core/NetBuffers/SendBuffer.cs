using Repl.Server.Core.Network.Tcp;
using Repl.Server.Core.Pooling.BufferPool;

namespace Repl.Server.Core.NetBuffers;

public sealed class SendBuffer : IDisposable
{
    private readonly LohSegment bufferSegment;
    private readonly int requiredSize;
    private volatile bool disposed;
    private volatile int refCount = 1;

    public ArraySegment<byte> Data => this.bufferSegment.Segment.Slice(0, requiredSize);
    public int RefCount => this.refCount;
        
    private SendBuffer(LohSegment bufferSegment, int requiredSize)
    {
        this.bufferSegment = bufferSegment;
        this.requiredSize = requiredSize;
    }

    public static SendBuffer Rent(int bufferSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(bufferSize, 0, nameof(bufferSize));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bufferSize, TcpConstant.TCP_MAX_SEGMENT_SIZE, nameof(bufferSize));
            
        var bufferSegment = LohBufferPool.Shared.Rent(bufferSize);
        return new SendBuffer(bufferSegment, bufferSize);
    }

    public Span<byte> WriteSegment => this.bufferSegment.Segment.AsSpan(0, this.requiredSize);
        
    public void AddRef()
    {
        Interlocked.Increment(ref this.refCount);
    }

    public void Dispose()
    {
        if (Interlocked.Decrement(ref this.refCount) == 0)
        {
            if (this.disposed == false)
            {
                this.disposed = true;
                this.bufferSegment.Dispose();
                GC.SuppressFinalize(this);
            }
        }
    }
}

