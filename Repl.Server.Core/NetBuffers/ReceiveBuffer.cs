using Microsoft.Extensions.Logging;
using Repl.Server.Core.Logging;
using Repl.Server.Core.Pooling.BufferPool;

namespace Repl.Server.Core.NetBuffers;

public class ReceiveBuffer : IDisposable
{
    private readonly ILogger<ReceiveBuffer> logger = Log.CreateLogger<ReceiveBuffer>();

    private readonly LohSegment bufferSegment;
    private int readPosition;
    private int writePosition;
    
    public int DataSize => this.writePosition - this.readPosition;
    public int FreeSize => this.bufferSegment.Count - this.writePosition;
    public ArraySegment<byte> ReadSegment => bufferSegment.Segment.Slice(readPosition, DataSize);
    public ArraySegment<byte> WriteSegment => bufferSegment.Segment.Slice(writePosition, FreeSize);

    public ReceiveBuffer(int bufferSize)
    {
        this.bufferSegment = LohBufferPool.Shared.Rent(bufferSize);
    }
    
    public void Reset()
    {
        int dataSize = this.DataSize;
        if (dataSize == 0)
        {
            this.readPosition = 0;
            this.writePosition = 0;
            return;
        }

        var segment = this.bufferSegment.Segment;
        // this.logger.LogWarning("ReceiveBuffer HotPath entered {datasize}", dataSize);
        // TODO: HotPath - check if it is executed every Clean() call from BeginReceive.
        // Array.Copy(this.bufferSegment.Array, this.readPosition, this.bufferSegment.Array, 0, dataSize);
        Array.Copy(segment.Array!, segment.Offset + this.readPosition, segment.Array!, segment.Offset, dataSize);
        
        this.readPosition = 0;
        this.writePosition = dataSize;
        
    }

    public bool CommitRead(int bytesRead)
    {
        if (bytesRead > this.DataSize)
        {
            return false;
        }
        this.readPosition += bytesRead;
        return true;
    }

    public bool CommitWrite(int bytesWrite)
    {
        if (bytesWrite > this.FreeSize)
        {
            return false;
        }
        this.writePosition += bytesWrite;
        return true;
    }

    public void Dispose()
    {
        this.bufferSegment.Dispose();
        GC.SuppressFinalize(this);
    }
}