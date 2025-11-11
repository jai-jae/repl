using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Repl.Server.Core.Logging;
using Repl.Server.Core.NetBuffers;
using Repl.Server.Core.ReplProtocol;

namespace Repl.Server.Core.Network.NetChannel;

public sealed class TcpMultiplexedChannel: ITcpNetChannel
{
    public const ushort APP_LEVEL_ACK = 65534;
    private readonly ILogger logger = Log.CreateLogger<TcpMultiplexedChannel>();
    private const int MAX_CONNECTION_COUNT = 8;
    private const int MIN_CONNECTION_COUNT = 4;
    private readonly ConcurrentDictionary<long, ReplTcpConnection> connections = [];
    private int disposed = 0;

    public long ChannelId { get; init; }
    public byte[] ReconnectToken { get; init; }
    public int ConnectionCount => this.connections.Count;

    // INetTransport event
    public event CompleteProcessPacketDelegate? ChannelCompleteProcessPacketEvent;
    public event ChannelClosedDelegate? ChannelClosedEvent;
        
    public TcpMultiplexedChannel(long channelId, byte[] reconnectToken, params ReplTcpConnection[] connections)
    {
        this.ChannelId = channelId;
        this.ReconnectToken = reconnectToken;
        foreach (ReplTcpConnection connection in connections)
        {
            this.connections[connection.ConnectionId] = connection;
            connection.CompleteProcessPacketEvent += this.OnCompleteProcessPacket;
            connection.ConnectionClosedEvent += this.OnChannelConnectionClosed;
        }
    }

    public bool Start()
    {
        return true;
    }

    public bool HandleReconnection(ReplTcpConnection newConnection, byte[] reconnectToken)
    {
        if (this.ValidateReconnectToken(reconnectToken) == false)
        {
            logger.LogDebug("[Channel:{channelId}] channel is full. Could not add new connection.", this.ChannelId);
            return false;
        }
            
        for (int i = 0; i < MAX_CONNECTION_COUNT; i++)
        {
            if (connections.TryAdd(i, newConnection) == true)
            {
                newConnection.RegisterToChannel(this.ChannelId, i);
                newConnection.CompleteProcessPacketEvent += this.OnCompleteProcessPacket;
                newConnection.ConnectionClosedEvent += this.OnChannelConnectionClosed;

                logger.LogDebug("[{channelId}] New connection added to channel at index {index}.", this.ChannelId, i);
                return true;
            }
        }

        logger.LogDebug("[Channel:{channelId}] channel is full. Could not add new connection.", this.ChannelId);
        return false;
    }
        
    private bool ValidateReconnectToken(byte[] reconnectToken)
    {
        return this.ReconnectToken == reconnectToken;
    }

    public bool Send(SendBuffer sendBuffer)
    {
        foreach (var connection in this.connections.Values)
        {
            if (connection.TrySetConnectionBusy() == false)
            {
                continue;
            }
            connection.Send(sendBuffer);
            logger.LogDebug("Index:{index} Message Sent.", connection.Index);
            return true;
        }
        logger.LogWarning($"There is no connection ready");
        // what todo if failed.
        return false;
    }

    public bool Send(List<SendBuffer> sendBufferList)
    {
        foreach (var connection in this.connections.Values)
        {
            if (connection.TrySetConnectionBusy() == true)
            {
                connection.Send(sendBufferList);
                logger.LogDebug("Index:{index} Message Sent.", connection.Index);
                return true;
            }
        }
        logger.LogWarning($"There is no connection ready");
        return false;
    }

    public void OnCompleteProcessPacket(ReplTcpConnection connection, ushort opCode, ReadOnlySpan<byte> content)
    {
        if (opCode == APP_LEVEL_ACK)
        {
            connection.ClearConnectionBusy();
            logger.LogDebug("Index:{index} ACK arrived.", connection.Index);
            return;
        }

        this.ChannelCompleteProcessPacketEvent?.Invoke(opCode, content);
    }

    public void OnChannelConnectionClosed(long connectionId)
    {
        if (this.connections.TryRemove(connectionId, out var connection) == true)
        {
            connection.Dispose();
            logger.LogDebug($"[Channel:{this.ChannelId}] Connection is closed. ConnectionId:{connectionId}");
        }
        // TODO: if no connections are available, should it invoke TransportClosedEvent?
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
            if (disposing)
            {
                this.ChannelCompleteProcessPacketEvent = null;
                this.ChannelClosedEvent = null;
                
                foreach (var connection in this.connections.Values)
                {
                    connection.Dispose();
                }
                connections.Clear();
            }
            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~TcpMultiplexedChannel()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}