using Repl.Server.Core.NetBuffers;

namespace Repl.Server.Core.Network.NetChannel;

/// <summary>
/// PacketReceive event in intermediary delegate to be handled on INetChannel layer.
/// </summary>
public delegate void ConnectionPacketReceivedDelegate<in T>(T connection, ushort opCode, ReadOnlySpan<byte> content);

/// <summary>
/// triggered when the connetion closes. it must be handled by INetChannel layer.
/// </summary>
public delegate void ConnectionClosedDelegate(long connectionId);

public interface IChannelConnection<out T> : IDisposable where T : IChannelConnection<T>
{   
    public string RemoteEndpoint { get; }
    public long ChannelId { get; }
    public long ConnectionId { get; }
    
    public event ConnectionPacketReceivedDelegate<T> CompleteProcessPacketEvent;
    public event ConnectionClosedDelegate ConnectionClosedEvent;
    
    public void Send(SendBuffer sendBuffer);
    public void Send(List<SendBuffer> sendBufferList);
    public void ForceClose();
    public abstract int ProcessPacket(ReadOnlySpan<byte> buffer);
}