using Microsoft.Extensions.Logging;
using Repl.Server.Core.Logging;
using Repl.Server.Core.NetBuffers;
using Repl.Server.Core.ReplProtocol;

namespace Repl.Server.Core.Network.NetChannel;

public class TcpSimpleChannel : ITcpNetChannel
{
    private readonly ILogger<TcpSimpleChannel> logger = Log.CreateLogger<TcpSimpleChannel>();
    
    private ReplTcpConnection connection;
    private readonly byte[] reconnectToken;
    private int disposed = 0;

    public long ChannelId { get; init; }
    public event CompleteProcessPacketDelegate? ChannelCompleteProcessPacketEvent;
    public event ChannelClosedDelegate? ChannelClosedEvent;

    public TcpSimpleChannel(long channelId, ReplTcpConnection connection, byte[]  reconnectToken)
    {
        this.ChannelId = channelId;
        this.connection = connection;
        this.reconnectToken = reconnectToken;
        connection.RegisterToChannel(this.ChannelId, 0);
        connection.CompleteProcessPacketEvent += this.OnCompleteProcessPacket;
        connection.ConnectionClosedEvent += this.OnChannelConnectionClosed;
    }
        
    public bool Send(SendBuffer sendBuffer)
    {
        connection.Send(sendBuffer);
        return true;
    }

    public bool Send(List<SendBuffer> sendBuffers)
    {
        this.connection.Send(sendBuffers);
        return true;
    }
    
    private bool ValidateReconnectToken(byte[] reconnectToken)
    {
        return this.reconnectToken == reconnectToken;
    }

    public bool HandleReconnection(ReplTcpConnection newConnection, byte[] reconnectToken)
    {
        if (this.connection.IsClosed() == false)
        {
            logger.LogDebug("[Channel:{channelId}] connection is not closed.", this.ChannelId);
            return false;
        }
        
        if (this.ValidateReconnectToken(reconnectToken) == false)
        {
            logger.LogDebug("[Channel:{channelId}] channel is full. Could not add new connection.", this.ChannelId);
            return false;
        }
        
        this.connection = newConnection;
        newConnection.RegisterToChannel(this.ChannelId, 0);
        newConnection.CompleteProcessPacketEvent += this.OnCompleteProcessPacket;
        newConnection.ConnectionClosedEvent += this.OnChannelConnectionClosed;

        logger.LogDebug("[{channelId}] New connection added to channel", this.ChannelId);
        return true;
    }

    public int ConnectionCount => 1;

    public bool Start()
    {
        return true;
    }

    private void OnCompleteProcessPacket(ReplTcpConnection conn, ushort opCode, ReadOnlySpan<byte> content)
    {
        ChannelCompleteProcessPacketEvent?.Invoke(opCode, content);
    }

    public void ForceClose()
    {
        this.ChannelClosedEvent?.Invoke();
        this.Dispose();
    }

    private void Dispose(bool disposing)
    {
        if (Interlocked.CompareExchange(ref this.disposed, 1, 0) == 0)
        {
            // MANAGED RESOURCE CLEAN UP
            // 1. Managed objects that implement IDisposable
            // 2. Managed objects that consume large amounts of memory or consume limited resources
            this.ChannelCompleteProcessPacketEvent = null;
            this.ChannelClosedEvent = null;
            
            if (disposing)
            {
                connection.Dispose();
            }
            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
        }
    }

    public void Dispose()
    {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void OnChannelConnectionClosed(long connectionId)
    {
        this.connection.Dispose();
        this.connection = null;
    }
    
    private void OnConnectionClosed(long connectionId)
    {
        ChannelClosedEvent?.Invoke();
    }
}