using Repl.Server.Core.ReplProtocol;

namespace Repl.Server.Game.ConnectionHandshake.States;

public class UnboundConnectionInfo
{
    public ReplTcpConnection Connection { get; }
    public DateTime EstablishedAt { get;  }
    public DateTime ExpiresAt { get; }

    public UnboundConnectionInfo(ReplTcpConnection connection, DateTime establishedAt, DateTime expiresAt)
    {
        Connection = connection;
        EstablishedAt = establishedAt;
        ExpiresAt = expiresAt;
    }
}
    
public class BoundConnectionInfo
{
    public ReplTcpConnection Connection { get; }
    public bool HasAcknowledged { get; set; } = false;
        
    public BoundConnectionInfo(ReplTcpConnection connection)
    {
        this.Connection = connection;
    }
}

public enum ConnectionLocation
{
    Unbound,
    BoundToChannel,
    Active
}

public struct ConnectionInfo
{
    public ConnectionLocation Location { get; init; }
    public ReplTcpConnection Connection { get; init; }
    public long? ChannelId { get; init; }
    public bool HasProcessedHandshake { get; init; }
    public bool HasChannelReadyAcknowledged { get; init; }
}