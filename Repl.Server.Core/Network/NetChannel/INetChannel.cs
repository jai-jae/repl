using Repl.Server.Core.NetBuffers;
using Repl.Server.Core.ReplProtocol;

namespace Repl.Server.Core.Network.NetChannel;

/// <summary>
/// Packet process is completed, ready to be consumed by GameSession
/// </summary>
public delegate void CompleteProcessPacketDelegate(ushort opCode, ReadOnlySpan<byte> content);

/// <summary>
/// All backend connection has been closed. Propagate up to GameSession.
/// </summary>
public delegate void ChannelClosedDelegate();

public interface INetChannel<in T> : IDisposable where T : IChannelConnection<T>
{
    public event CompleteProcessPacketDelegate? ChannelCompleteProcessPacketEvent;
    public event ChannelClosedDelegate? ChannelClosedEvent;
    public bool Start();
    public bool Send(SendBuffer sendBuffer);
    public bool Send(List<SendBuffer> sendBuffer);
    
    bool HandleReconnection(ReplTcpConnection connection, byte[] reconnectToken);
    int ConnectionCount { get; }
}

public interface ITcpNetChannel : INetChannel<ReplTcpConnection>
{
}