using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Repl.Server.Core.Network.NetChannel;
using Repl.Server.Core.Network.Tcp;

namespace Repl.Server.Core.ReplProtocol;

public sealed class ReplTcpConnection : TcpConnectionBase, IChannelConnection<ReplTcpConnection>
{
    private static long connectionId = 0;
    private static long GenerateId() => Interlocked.Increment(ref connectionId);
        
    private const int INVALID_CHANNEL_ID = -1;
    private const int INVALID_INDEX = -1;
    private int isBusy = 0;

    public long ConnectionId { get;}
    public long ChannelId { get; private set; } = INVALID_CHANNEL_ID;
    public int Index { get; private set; } = INVALID_INDEX;
        
    public event ConnectionPacketReceivedDelegate<ReplTcpConnection>? CompleteProcessPacketEvent;
    public event ConnectionClosedDelegate? ConnectionClosedEvent;

    public ReplTcpConnection(Socket socket, int maxReceiveBufferSize = 1024)
        : base(socket, maxReceiveBufferSize)
    {
        this.ConnectionId = GenerateId();
        base.ClosedEvent += this.OnConnectionBaseClosed;
    }

    private void CompleteProcessPacket(ushort opCode, ReadOnlySpan<byte> bytes)
    {
        this.CompleteProcessPacketEvent?.Invoke(this, opCode, bytes);
    }
        
    public override int ProcessPacket(ReadOnlySpan<byte> buffer)
    {
        int bytesProcessed = 0;
        while (true)
        {
            if (buffer.Length < ReplPacketHeader.HEADER_SIZE)
            {
                break;
            }

            ushort totalPacketSize = ReplPacketHeader.ParsePacketSize(buffer);

            if (buffer.Length < totalPacketSize)
            {
                break;
            }
            
            var opCode = ReplPacketHeader.ParseOpCode(buffer);

            if (totalPacketSize < ReplPacketHeader.HEADER_SIZE)
            {
                this.logger.LogError(
                    $"invalid serialized content. content is short. opCode: {opCode}, size:{totalPacketSize}");
                return -1;
            }
            this.CompleteProcessPacket(opCode, buffer.Slice(ReplPacketHeader.HEADER_SIZE, totalPacketSize - ReplPacketHeader.HEADER_SIZE));

            bytesProcessed += totalPacketSize;
            buffer = buffer.Slice(totalPacketSize);
        }
        return bytesProcessed;
    }

    // UdpOverTcp Channel related functionalities
    public void RegisterToChannel(long channelId, int index)
    {
        this.ChannelId = channelId;
        this.Index = index;
    }

    public bool TrySetConnectionBusy(int numberOfPacket = 0)
    {
        return Interlocked.CompareExchange(ref this.isBusy, 1, 0) == 0;
    }

    public void ClearConnectionBusy()
    {
        Interlocked.Exchange(ref this.isBusy, 0);
    }

    private void OnConnectionBaseClosed()
    {
        ConnectionClosedEvent?.Invoke(this.ConnectionId);
    }

    protected override void Dispose(bool disposing)
    {
        CompleteProcessPacketEvent = null; 
        ConnectionClosedEvent = null;
        base.Dispose(disposing);
    }
}